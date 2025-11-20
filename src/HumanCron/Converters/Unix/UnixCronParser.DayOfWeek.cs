using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;

namespace HumanCron.Converters.Unix;

/// <summary>
/// UnixCronParser - Day-of-Week parsing methods
/// </summary>
internal sealed partial class UnixCronParser
{
    private static DayOfWeek? ParseDayOfWeek(string dayOfWeekPart)
    {
        if (dayOfWeekPart == "*")
        {
            return null;
        }

        // Handle day ranges (1-5, mon-fri, 0,6) - return null, these become DayPattern
        if (dayOfWeekPart.Contains('-') || dayOfWeekPart.Contains(','))
        {
            return null;
        }

        // Single day - use helper to parse numeric (0-7) or named (sun-sat)
        return ParseDayOfWeekValue(dayOfWeekPart);
    }

    private static DayPattern? ParseDayPattern(string dayOfWeekPart)
    {
        // Numeric patterns
        if (dayOfWeekPart == "1-5") return DayPattern.Weekdays;   // Monday-Friday
        if (dayOfWeekPart == "0,6" || dayOfWeekPart == "6,0") return DayPattern.Weekends;  // Sunday,Saturday

        // Named patterns (case-insensitive)
        var lower = dayOfWeekPart.ToLowerInvariant();
        if (lower == "mon-fri") return DayPattern.Weekdays;
        if (lower == "sat,sun" || lower == "sun,sat") return DayPattern.Weekends;

        return null;
    }

    /// <summary>
    /// Parse day-of-week list from cron expression (e.g., "1,3,5" → [Monday, Wednesday, Friday])
    /// Returns null if not a valid list (handled by DayPattern or single DayOfWeek instead)
    /// Skips consecutive sequences that should be represented as ranges (handled by ParseDayOfWeekRange)
    /// </summary>
    private static IReadOnlyList<DayOfWeek>? ParseDayOfWeekList(string dayOfWeekPart)
    {
        if (dayOfWeekPart == "*" || !dayOfWeekPart.Contains(','))
        {
            return null;  // Not a list
        }

        // Check if it's a known pattern (handled by ParseDayPattern)
        if (dayOfWeekPart == "0,6" || dayOfWeekPart == "6,0")
        {
            return null;  // Weekends pattern
        }

        // Parse comma-separated list
        var parts = dayOfWeekPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;  // Not a multi-day list
        }

        // Check if this is a consecutive sequence (should be handled as a range instead)
        List<int> dayNumbers = [];
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var dayNum) && dayNum is >= 0 and <= 7)
            {
                dayNumbers.Add(dayNum == 7 ? 0 : dayNum);  // Normalize 7 to 0
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

        List<DayOfWeek> days = [];
        foreach (var part in parts)
        {
            var day = ParseDayOfWeekValue(part);
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
    /// Parse day-of-week range from cron expression that was expanded to a list
    /// (e.g., "2,3,4" → (Tuesday, Thursday) for Tuesday-Thursday range)
    /// Returns (null, null) if not a consecutive range
    /// </summary>
    private static (DayOfWeek? Start, DayOfWeek? End) ParseDayOfWeekRange(string dayOfWeekPart)
    {
        if (dayOfWeekPart == "*" || !dayOfWeekPart.Contains(','))
        {
            return (null, null);  // Not a list
        }

        // Check if it's a known pattern (handled by ParseDayPattern)
        if (dayOfWeekPart == "1-5" || dayOfWeekPart == "0,6" || dayOfWeekPart == "6,0")
        {
            return (null, null);  // Known pattern, not a custom range
        }

        // Parse comma-separated list and check if it's consecutive
        var parts = dayOfWeekPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return (null, null);  // Not a range
        }

        List<int> days = [];
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var dayNum) && dayNum is >= 0 and <= 7)
            {
                // Normalize 7 to 0 (both are Sunday in Unix cron)
                days.Add(dayNum == 7 ? 0 : dayNum);
            }
            else
            {
                return (null, null);  // Invalid day number
            }
        }

        // Check if it's a consecutive sequence (including wraparound)
        if (IsConsecutiveDaySequence(days))
        {
            var startDay = ParseDayOfWeekValue(days[0].ToString());
            var endDay = ParseDayOfWeekValue(days[^1].ToString());
            return (startDay, endDay);
        }

        return (null, null);  // Not consecutive
    }

    /// <summary>
    /// Check if a list of day numbers is a consecutive sequence
    /// Handles wraparound: [5,6,0,1] is consecutive (Friday-Monday)
    /// </summary>
    private static bool IsConsecutiveDaySequence(List<int> days)
    {
        if (days.Count < 2) return false;

        // Check simple consecutive: [2,3,4] or [0,1,2]
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

        // Check wraparound consecutive: [5,6,0,1] or [6,0]
        // Pattern: starts high (5 or 6), increments to 6, then wraps to 0, then consecutive
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

        // Verify before wrap is consecutive and ends at 6
        for (int i = 1; i < wrapIndex; i++)
        {
            if (days[i] != days[i - 1] + 1) return false;
        }
        if (days[wrapIndex - 1] != 6) return false;  // Must end at Saturday

        // Verify after wrap starts at 0 and is consecutive
        if (days[wrapIndex] != 0) return false;  // Must start at Sunday
        for (int i = wrapIndex + 1; i < days.Count; i++)
        {
            if (days[i] != days[i - 1] + 1) return false;
        }

        return true;
    }

    /// <summary>
    /// Parse a single day-of-week value from either numeric (0-7) or named (sun-sat) format
    /// Unix cron: 0 and 7 both = Sunday
    /// </summary>
    private static DayOfWeek? ParseDayOfWeekValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try numeric first (0-7, where 0 and 7 both = Sunday)
        if (int.TryParse(value, out var numeric))
        {
            return numeric switch
            {
                0 or 7 => DayOfWeek.Sunday,
                1 => DayOfWeek.Monday,
                2 => DayOfWeek.Tuesday,
                3 => DayOfWeek.Wednesday,
                4 => DayOfWeek.Thursday,
                5 => DayOfWeek.Friday,
                6 => DayOfWeek.Saturday,
                _ => null  // Out of range
            };
        }

        // Try named (case-insensitive)
        return value.ToLowerInvariant() switch
        {
            "sun" => DayOfWeek.Sunday,
            "mon" => DayOfWeek.Monday,
            "tue" => DayOfWeek.Tuesday,
            "wed" => DayOfWeek.Wednesday,
            "thu" => DayOfWeek.Thursday,
            "fri" => DayOfWeek.Friday,
            "sat" => DayOfWeek.Saturday,
            _ => null  // Not a valid day name
        };
    }
}
