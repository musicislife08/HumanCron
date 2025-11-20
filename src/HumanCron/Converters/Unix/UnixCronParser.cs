using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using HumanCron.Models;

namespace HumanCron.Converters.Unix;

/// <summary>
/// Parses Unix 5-part cron expressions back into ScheduleSpec
/// Format: minute hour day month dayOfWeek
/// </summary>
internal sealed class UnixCronParser
{
    // Maximum allowed interval to prevent unreasonable values (e.g., "every 999999 minutes")
    private const int MaxInterval = 1000;

    /// <summary>
    /// Parse Unix 5-part cron expression into ScheduleSpec
    /// </summary>
    /// <param name="cronExpression">Unix cron expression (e.g., "0 14 * * *")</param>
    /// <returns>ParseResult with ScheduleSpec or error</returns>
    public ParseResult<ScheduleSpec> Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return new ParseResult<ScheduleSpec>.Error("Cron expression cannot be empty");
        }

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return new ParseResult<ScheduleSpec>.Error($"Unix cron expressions must have 5 parts (got {parts.Length}). Format: minute hour day month dayOfWeek");
        }

        try
        {
            var minute = parts[0];
            var hour = parts[1];
            var day = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            // Determine interval unit and value based on pattern
            var (interval, unit) = DetermineInterval(minute, hour, day, dayOfWeek);
            if (interval == 0)
            {
                return new ParseResult<ScheduleSpec>.Error($"Could not determine interval from cron expression: {cronExpression}");
            }

            // Parse day-of-week if specified
            var parsedDayOfWeek = ParseDayOfWeek(dayOfWeek);
            var parsedDayPattern = ParseDayPattern(dayOfWeek);

            // Parse time-of-day if specified
            var timeOfDay = ParseTimeOfDay(minute, hour, unit);

            // Parse month specifier
            var monthSpecifier = ParseMonthSpecifier(month);

            // Parse ranges and lists for minute, hour, and day fields
            var (minuteStart, minuteEnd, minuteStep) = ParseRange(minute);
            var (hourStart, hourEnd, hourStep) = ParseRange(hour);
            var (dayStart, dayEnd, dayStep) = ParseRange(day);
            var minuteList = ParseList(minute, 0, 59);
            var hourList = ParseList(hour, 0, 23);
            var dayList = ParseList(day, 1, 31);

            // Parse single day-of-month value (e.g., "1", "15", "31")
            int? dayOfMonth = null;
            if (day != "*" && !day.Contains('-') && !day.Contains(',') && !day.Contains('/'))
            {
                if (int.TryParse(day, out var dayValue) && dayValue is >= 1 and <= 31)
                {
                    dayOfMonth = dayValue;
                }
            }

            var spec = new ScheduleSpec
            {
                Interval = interval,
                Unit = unit,
                DayOfWeek = parsedDayOfWeek,
                DayPattern = parsedDayPattern,
                DayOfMonth = dayOfMonth,
                Month = monthSpecifier,
                TimeOfDay = timeOfDay,
                MinuteStart = minuteStart,
                MinuteEnd = minuteEnd,
                MinuteStep = minuteStep,
                MinuteList = minuteList,
                HourStart = hourStart,
                HourEnd = hourEnd,
                HourStep = hourStep,
                HourList = hourList,
                DayStart = dayStart,
                DayEnd = dayEnd,
                DayStep = dayStep,
                DayList = dayList
            };

            return new ParseResult<ScheduleSpec>.Success(spec);
        }
        catch (Exception ex)
        {
            return new ParseResult<ScheduleSpec>.Error($"Failed to parse cron expression: {ex.Message}");
        }
    }

    private static (int Interval, IntervalUnit Unit) DetermineInterval(
        string minute, string hour, string day, string dayOfWeek)
    {
        // Pattern: */15 * * * * → Every 15 minutes
        if (minute.StartsWith("*/") || (minute == "*" && hour == "*"))
        {
            if (minute == "*")
            {
                return (1, IntervalUnit.Minutes);
            }
            var interval = int.Parse(minute[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Minutes); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Minutes);
        }

        // Pattern: 0 */6 * * * → Every 6 hours
        if (hour.StartsWith("*/") || (hour == "*" && day == "*" && dayOfWeek == "*"))
        {
            if (hour == "*")
            {
                return (1, IntervalUnit.Hours);
            }
            var interval = int.Parse(hour[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Hours); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Hours);
        }

        // Pattern: 0 14 * * * → Daily at specific time
        if (day == "*" && dayOfWeek == "*")
        {
            return (1, IntervalUnit.Days);
        }

        // Pattern: 0 14 */2 * * → Every 2 days at specific time
        if (day.StartsWith("*/"))
        {
            var interval = int.Parse(day[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Days); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Days);
        }

        // Pattern: 0 14 * * 1 → Weekly on Monday
        if (dayOfWeek != "*" && day == "*")
        {
            return (1, IntervalUnit.Weeks);
        }

        // Pattern: 0 1 1 1 * → Monthly on specific day (with month constraint)
        // When we have a specific day-of-month value, default to monthly
        // This allows patterns like "every month on the 15th" or "every month on january 1st"
        return (1, IntervalUnit.Months);
    }

    /// <summary>
    /// Validates that an interval value is reasonable (1 to MaxInterval)
    /// </summary>
    /// <param name="interval">The interval value to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidInterval(int interval) => interval is > 0 and <= MaxInterval;

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

    private static TimeOnly? ParseTimeOfDay(string minute, string hour, IntervalUnit unit)
    {
        // Only parse time for daily/weekly intervals with fixed times
        if (unit == IntervalUnit.Minutes || unit == IntervalUnit.Hours)
        {
            return null;  // Sub-daily intervals don't have fixed time-of-day
        }

        // Parse hour and minute for daily/weekly schedules
        if (!int.TryParse(hour, out var hourValue) || !int.TryParse(minute, out var minuteValue)) return null;
        if (hourValue is >= 0 and <= 23 && minuteValue is >= 0 and <= 59)
        {
            return new TimeOnly(hourValue, minuteValue);
        }

        return null;
    }

    private static MonthSpecifier ParseMonthSpecifier(string monthPart)
    {
        // Wildcard: all months
        if (monthPart == "*")
        {
            return new MonthSpecifier.None();
        }

        // Range: 1-3 or jan-mar (january through march)
        if (monthPart.Contains('-'))
        {
            var parts = monthPart.Split('-');
            if (parts.Length == 2)
            {
                var start = ParseMonthValue(parts[0]);
                var end = ParseMonthValue(parts[1]);

                if (start.HasValue && end.HasValue && start.Value < end.Value)
                {
                    return new MonthSpecifier.Range(start.Value, end.Value);
                }
            }
            // Invalid range - fall through to None
            return new MonthSpecifier.None();
        }

        // List: 1,4,7,10 or jan,apr,jul,oct (quarterly)
        if (monthPart.Contains(','))
        {
            var parts = monthPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var months = new List<int>();

            foreach (var part in parts)
            {
                var month = ParseMonthValue(part);
                if (month.HasValue)
                {
                    months.Add(month.Value);
                }
            }

            if (months.Count >= 2)
            {
                return new MonthSpecifier.List(months);
            }
            // Invalid list - fall through to None
            return new MonthSpecifier.None();
        }

        // Single month: 1 or jan (january)
        var singleMonth = ParseMonthValue(monthPart);
        if (singleMonth.HasValue)
        {
            return new MonthSpecifier.Single(singleMonth.Value);
        }

        // Default: all months
        return new MonthSpecifier.None();
    }

    /// <summary>
    /// Parse a month value from either numeric (1-12) or named (jan-dec) format
    /// Returns null only for empty/whitespace strings - otherwise returns parsed value (valid or invalid)
    /// Invalid values are accepted to document actual cron parser behavior (validation deferred)
    /// </summary>
    private static int? ParseMonthValue(string value)
    {
        // Empty or whitespace - can't parse
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try numeric first - accept any integer (including negative, out-of-range)
        // This matches cron parser behavior: parse anything, validate later
        if (int.TryParse(value, out var numeric))
        {
            return numeric;
        }

        // Try named (case-insensitive)
        return value.ToLowerInvariant() switch
        {
            "jan" => 1,
            "feb" => 2,
            "mar" => 3,
            "apr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "aug" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            "dec" => 12,
            _ => null  // Not a number, not a valid name - can't parse
        };
    }

    /// <summary>
    /// Parse range from cron field (e.g., "9-17", "0-30", "1-15", "9-17/2")
    /// Returns (null, null, null) if not a range
    /// </summary>
    private static (int? Start, int? End, int? Step) ParseRange(string field)
    {
        if (string.IsNullOrWhiteSpace(field) || !field.Contains('-'))
        {
            return (null, null, null);
        }

        // Skip if it's a wildcard or step pattern
        if (field == "*" || field.StartsWith("*/"))
        {
            return (null, null, null);
        }

        // Check for range+step: "9-17/2"
        int? step = null;
        var rangeField = field;

        if (field.Contains('/'))
        {
            var stepParts = field.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (stepParts.Length == 2 && int.TryParse(stepParts[1], out var stepValue))
            {
                step = stepValue;
                rangeField = stepParts[0];
            }
        }

        // Parse range: "9-17" → (9, 17) or "9-17/2" → (9, 17, 2)
        var parts = rangeField.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
        {
            return (start, end, step);
        }

        return (null, null, null);
    }

    /// <summary>
    /// Parse list from cron field with support for mixed syntax:
    /// - Simple list: "0,15,30,45" → [0, 15, 30, 45]
    /// - Mixed list+range: "0-4,8-12,20" → [0, 1, 2, 3, 4, 8, 9, 10, 11, 12, 20]
    /// Returns null if not a list
    /// </summary>
    private static IReadOnlyList<int>? ParseList(string field, int minValue, int maxValue)
    {
        if (string.IsNullOrWhiteSpace(field) || !field.Contains(','))
        {
            return null;
        }

        // Skip if it's a wildcard or step pattern
        if (field == "*" || field.StartsWith("*/"))
        {
            return null;
        }

        // Parse list with possible ranges: "0-4,8-12,20" → [0,1,2,3,4,8,9,10,11,12,20]
        var parts = field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>();

        foreach (var part in parts)
        {
            // Check if this part is a range (e.g., "0-4")
            if (part.Contains('-') && !part.StartsWith("-"))
            {
                var rangeParts = part.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out var start) &&
                    int.TryParse(rangeParts[1], out var end) &&
                    start <= end &&
                    start >= minValue && end <= maxValue)
                {
                    // Expand range: 0-4 → [0, 1, 2, 3, 4]
                    for (var i = start; i <= end; i++)
                    {
                        values.Add(i);
                    }
                }
            }
            // Single value
            else if (int.TryParse(part, out var value) && value >= minValue && value <= maxValue)
            {
                values.Add(value);
            }
        }

        return values.Count >= 2 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }
}
