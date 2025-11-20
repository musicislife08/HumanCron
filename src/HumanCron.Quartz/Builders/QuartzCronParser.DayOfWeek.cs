using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Day-of-Week parsing methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    private static DayOfWeek? ParseDayOfWeek(ReadOnlySpan<char> dayOfWeekPart)
    {
        if (dayOfWeekPart is "?" || dayOfWeekPart is "*")
        {
            return null;
        }

        // Handle day ranges (MON-FRI, 2-6, SAT,SUN) - return null, these become DayPattern
        if (dayOfWeekPart.Contains('-') || dayOfWeekPart.Contains(','))
        {
            return null;
        }

        // Single day - use helper to parse numeric (1-7) or named (sun-sat)
        return ParseDayOfWeekValue(dayOfWeekPart);
    }

    private static DayPattern? ParseDayPattern(ReadOnlySpan<char> dayOfWeekPart)
    {
        // Named patterns (case-insensitive)
        if (dayOfWeekPart.Equals("MON-FRI", StringComparison.OrdinalIgnoreCase))
            return DayPattern.Weekdays;
        if (dayOfWeekPart.Equals("SAT,SUN", StringComparison.OrdinalIgnoreCase) ||
            dayOfWeekPart.Equals("SUN,SAT", StringComparison.OrdinalIgnoreCase))
            return DayPattern.Weekends;

        return dayOfWeekPart switch
        {
            // Numeric patterns
            "2-6" => DayPattern.Weekdays // Monday(2)-Friday(6)
            ,
            "1,7" or "7,1" => DayPattern.Weekends // Sunday(1),Saturday(7)
            ,
            _ => null
        };
    }

    /// <summary>
    /// Parse day-of-week list from Quartz cron expression (e.g., "MON,WED,FRI" or "2,4,6" → [Monday, Wednesday, Friday])
    /// Returns null if not a valid list (handled by DayPattern or single DayOfWeek instead)
    /// Skips consecutive sequences that should be represented as ranges (handled by ParseDayOfWeekRange)
    /// </summary>
    private static IReadOnlyList<DayOfWeek>? ParseDayOfWeekList(ReadOnlySpan<char> dayOfWeekPart)
    {
        if (dayOfWeekPart is "?" || dayOfWeekPart is "*" || !dayOfWeekPart.Contains(','))
        {
            return null;  // Not a list
        }

        // Check if it's a known pattern (handled by ParseDayPattern)
        if (dayOfWeekPart is "1,7" or "7,1")
        {
            return null;  // Weekends pattern
        }

        // Convert to string for easier parsing (Span doesn't have Split)
        var dayOfWeekString = dayOfWeekPart.ToString();
        var parts = dayOfWeekString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;  // Not a multi-day list
        }

        // Check if this is a consecutive sequence (should be handled as a range instead)
        var dayNumbers = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var dayNum) && dayNum is >= 1 and <= 7)
            {
                dayNumbers.Add(dayNum);
            }
            else
            {
                break;  // Not all numeric, continue with DayOfWeek parsing
            }
        }

        // If all parts are numeric and form a consecutive sequence, let ParseDayOfWeekRange handle it
        if (dayNumbers.Count == parts.Length && IsConsecutiveDaySequence(dayNumbers))
        {
            return null;  // Consecutive sequence - will be handled as a range
        }

        var days = new List<DayOfWeek>();
        foreach (var part in parts)
        {
            var day = ParseDayOfWeekValue(part.AsSpan());
            if (day.HasValue)
            {
                days.Add(day.Value);
            }
            else
            {
                return null;  // Invalid day in list
            }
        }

        return days.Count >= 2 ? days : null;
    }

    /// <summary>
    /// Parse day-of-week range from Quartz cron expression that was expanded to a list
    /// (e.g., "3,4,5" or "TUE,WED,THU" → (Tuesday, Thursday) for Tuesday-Thursday range)
    /// Returns (null, null) if not a consecutive range
    /// </summary>
    private static (DayOfWeek? Start, DayOfWeek? End) ParseDayOfWeekRange(ReadOnlySpan<char> dayOfWeekPart)
    {
        if (dayOfWeekPart is "?" || dayOfWeekPart is "*" || !dayOfWeekPart.Contains(','))
        {
            return (null, null);  // Not a list
        }

        // Check if it's a known pattern (handled by ParseDayPattern)
        if (dayOfWeekPart is "2-6" or "1,7" or "7,1")
        {
            return (null, null);  // Known pattern, not a custom range
        }

        // Convert to string for easier parsing
        var dayOfWeekString = dayOfWeekPart.ToString();
        var parts = dayOfWeekString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return (null, null);  // Not a range
        }

        // Parse as numeric days and check if it's consecutive
        var days = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var dayNum) && dayNum is >= 1 and <= 7)
            {
                days.Add(dayNum);
            }
            else
            {
                // Try parsing as named days (MON, TUE, etc.) and converting to Quartz numbers
                var dayValue = ParseDayOfWeekValue(part.AsSpan());
                if (dayValue.HasValue)
                {
                    // Convert .NET DayOfWeek (0=Sunday) to Quartz (1=Sunday)
                    days.Add((int)dayValue.Value == 0 ? 1 : (int)dayValue.Value + 1);
                }
                else
                {
                    return (null, null);  // Invalid day
                }
            }
        }

        // Check if it's a consecutive sequence (including wraparound)
        if (IsConsecutiveDaySequence(days))
        {
            // Convert Quartz numbers back to DayOfWeek for the start and end
            var startDay = ParseDayOfWeekValue(days[0].ToString().AsSpan());
            var endDay = ParseDayOfWeekValue(days[^1].ToString().AsSpan());
            return (startDay, endDay);
        }

        return (null, null);  // Not consecutive
    }

    /// <summary>
    /// Check if a list of Quartz day numbers (1-7) is a consecutive sequence
    /// Handles wraparound: [6,7,1,2] is consecutive (Friday-Monday in Quartz: 6=Fri, 7=Sat, 1=Sun, 2=Mon)
    /// </summary>
    private static bool IsConsecutiveDaySequence(List<int> days)
    {
        if (days.Count < 2) return false;

        // Check simple consecutive: [3,4,5] or [1,2,3]
        bool isSimpleConsecutive = true;
        for (int i = 1; i < days.Count; i++)
        {
            if (days[i] != days[i - 1] + 1)
            {
                isSimpleConsecutive = false;
                break;
            }
        }

        if (isSimpleConsecutive) return true;

        // Check wraparound consecutive: [6,7,1,2] or [7,1]
        // Pattern: starts high (6 or 7), increments to 7, then wraps to 1, then consecutive
        int wrapIndex = -1;
        for (int i = 1; i < days.Count; i++)
        {
            if (days[i] < days[i - 1])
            {
                // Found potential wrap point
                if (wrapIndex != -1) return false;  // Multiple wraps = not consecutive
                wrapIndex = i;
            }
        }

        if (wrapIndex == -1) return false;  // No wrap found

        // Verify before wrap is consecutive and ends at 7 (Saturday in Quartz)
        for (int i = 1; i < wrapIndex; i++)
        {
            if (days[i] != days[i - 1] + 1) return false;
        }
        if (days[wrapIndex - 1] != 7) return false;  // Must end at Saturday

        // Verify after wrap starts at 1 (Sunday in Quartz) and is consecutive
        if (days[wrapIndex] != 1) return false;  // Must start at Sunday
        for (int i = wrapIndex + 1; i < days.Count; i++)
        {
            if (days[i] != days[i - 1] + 1) return false;
        }

        return true;
    }

    /// <summary>
    /// Parse a single day-of-week value from either numeric (1-7) or named (sun-sat) format
    /// Quartz cron: 1=Sunday, 2=Monday, ..., 7=Saturday
    /// </summary>
    private static DayOfWeek? ParseDayOfWeekValue(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value.IsWhiteSpace())
        {
            return null;
        }

        // Try numeric first (1-7, where 1=Sunday in Quartz)
        if (int.TryParse(value, out var numeric))
        {
            return numeric switch
            {
                1 => DayOfWeek.Sunday,
                2 => DayOfWeek.Monday,
                3 => DayOfWeek.Tuesday,
                4 => DayOfWeek.Wednesday,
                5 => DayOfWeek.Thursday,
                6 => DayOfWeek.Friday,
                7 => DayOfWeek.Saturday,
                _ => null  // Out of range
            };
        }

        // Try named (case-insensitive)
        if (value.Equals("sun", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Sunday;
        if (value.Equals("mon", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Monday;
        if (value.Equals("tue", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Tuesday;
        if (value.Equals("wed", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Wednesday;
        if (value.Equals("thu", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Thursday;
        if (value.Equals("fri", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Friday;
        if (value.Equals("sat", StringComparison.OrdinalIgnoreCase)) return DayOfWeek.Saturday;

        return null;  // Not a valid day name
    }
}
