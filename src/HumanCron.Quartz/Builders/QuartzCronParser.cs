using HumanCron.Models.Internal;
using HumanCron.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// Parses Quartz 6-part cron expressions back into ScheduleSpec
/// Format: second minute hour day month dayOfWeek
/// </summary>
internal sealed class QuartzCronParser
{
    public ParseResult<ScheduleSpec> Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return new ParseResult<ScheduleSpec>.Error("Cron expression cannot be empty");
        }

        // Use Span<T> to parse without allocating substring copies
        var cronSpan = cronExpression.AsSpan();

        // Parse into parts without allocating (allocate 8 slots to detect invalid expressions with > 7 parts)
        Span<Range> ranges = stackalloc Range[8];
        var partCount = cronSpan.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (partCount is < 6 or > 7)
        {
            return new ParseResult<ScheduleSpec>.Error($"Quartz cron expressions must have 6 or 7 parts (got {partCount}). Format: second minute hour day month dayOfWeek [year]");
        }

        var second = cronSpan[ranges[0]];
        var minute = cronSpan[ranges[1]];
        var hour = cronSpan[ranges[2]];
        var day = cronSpan[ranges[3]];
        var month = cronSpan[ranges[4]];
        var dayOfWeek = cronSpan[ranges[5]];
        var year = partCount == 7 ? cronSpan[ranges[6]] : ReadOnlySpan<char>.Empty;

        // Determine interval unit and value based on pattern
        var (interval, unit) = DetermineInterval(second, minute, hour, day, dayOfWeek);
        if (interval == 0)
        {
            return new ParseResult<ScheduleSpec>.Error($"Could not determine interval from cron expression: {cronExpression}");
        }

        // Parse day-of-week if specified
        var parsedDayOfWeek = ParseDayOfWeek(dayOfWeek);
        var parsedDayPattern = ParseDayPattern(dayOfWeek);

        // Parse month if specified
        var parsedMonth = ParseMonth(month);

        // Parse time-of-day if specified
        var timeOfDay = ParseTimeOfDay(second, minute, hour, unit);

        // Parse optional year field (1970-2099)
        var parsedYear = ParseYear(year);

        // Parse ranges and lists for minute, hour, and day fields
        var (minuteStart, minuteEnd, minuteStep) = ParseRange(minute);
        var (hourStart, hourEnd, hourStep) = ParseRange(hour);
        var (dayStart, dayEnd, dayStep) = ParseRange(day);
        var minuteList = ParseList(minute, 0, 59);
        var hourList = ParseList(hour, 0, 23);
        var dayList = ParseList(day, 1, 31);

        var spec = new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = parsedDayOfWeek,
            DayPattern = parsedDayPattern,
            Month = parsedMonth,
            TimeOfDay = timeOfDay,
            Year = parsedYear,
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

    private static (int Interval, NaturalIntervalUnit Unit) DetermineInterval(
        ReadOnlySpan<char> second, ReadOnlySpan<char> minute, ReadOnlySpan<char> hour, ReadOnlySpan<char> day, ReadOnlySpan<char> dayOfWeek)
    {
        // Pattern: */30 * * * * ? → Every 30 seconds
        if (second.StartsWith("*/") || second is "*")
        {
            if (second is "*")
            {
                return (1, NaturalIntervalUnit.Seconds);
            }
            var interval = int.Parse(second[2..]);
            return (interval, NaturalIntervalUnit.Seconds);
        }

        // Pattern: 0 */15 * * * ? → Every 15 minutes
        if (minute.StartsWith("*/") || (minute is "*" && hour is "*"))
        {
            if (minute is "*")
            {
                return (1, NaturalIntervalUnit.Minutes);
            }
            var interval = int.Parse(minute[2..]);
            return (interval, NaturalIntervalUnit.Minutes);
        }

        // Pattern: 0 0 */6 * * ? → Every 6 hours
        if (hour.StartsWith("*/") || (hour is "*" && day is "*" && dayOfWeek is "?"))
        {
            if (hour is "*")
            {
                return (1, NaturalIntervalUnit.Hours);
            }
            var interval = int.Parse(hour[2..]);
            return (interval, NaturalIntervalUnit.Hours);
        }

        // Pattern: 0 0 14 * * ? → Daily at specific time
        if (day is "*" && dayOfWeek is "?")
        {
            return (1, NaturalIntervalUnit.Days);
        }

        // Pattern: 0 0 14 */2 * ? → Every 2 days at specific time
        if (day.StartsWith("*/"))
        {
            var interval = int.Parse(day[2..]);
            return (interval, NaturalIntervalUnit.Days);
        }

        // Pattern: 0 0 14 ? * MON → Weekly on Monday
        if (!(dayOfWeek is "?") && day is "?")
        {
            return (1, NaturalIntervalUnit.Weeks);
        }

        // Default to daily if we can't determine
        return (1, NaturalIntervalUnit.Days);
    }

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

    private static MonthSpecifier ParseMonth(ReadOnlySpan<char> monthPart)
    {
        // "*" means all months (no constraint)
        if (monthPart is "*")
        {
            return new MonthSpecifier.None();
        }

        // Month range: "1-3" or "jan-mar" (January through March)
        var dashIndex = monthPart.IndexOf('-');
        if (dashIndex > 0)
        {
            var startSpan = monthPart[..dashIndex];
            var endSpan = monthPart[(dashIndex + 1)..];

            var start = ParseMonthValue(startSpan);
            var end = ParseMonthValue(endSpan);

            if (start.HasValue && end.HasValue)
            {
                return new MonthSpecifier.Range(start.Value, end.Value);
            }
        }

        // Month list: "1,4,7,10" or "jan,apr,jul,oct" (quarterly)
        if (monthPart.Contains(','))
        {
            List<int> months = [];
            Span<Range> ranges = stackalloc Range[12]; // Max 12 months
            var count = monthPart.Split(ranges, ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = 0; i < count; i++)
            {
                var month = ParseMonthValue(monthPart[ranges[i]]);
                if (month.HasValue)
                {
                    months.Add(month.Value);
                }
            }

            if (months.Count > 0)
            {
                return new MonthSpecifier.List(months);
            }
        }

        // Single month: "1" or "jan" (January only)
        var singleMonth = ParseMonthValue(monthPart);
        if (singleMonth.HasValue)
        {
            return new MonthSpecifier.Single(singleMonth.Value);
        }

        // Default to None if we can't parse
        return new MonthSpecifier.None();
    }

    /// <summary>
    /// Parse a month value from either numeric (1-12) or named (jan-dec) format
    /// Returns null only for empty/whitespace strings - otherwise returns parsed value (valid or invalid)
    /// Invalid values are accepted to document actual cron parser behavior (validation deferred)
    /// </summary>
    private static int? ParseMonthValue(ReadOnlySpan<char> value)
    {
        // Empty or whitespace - can't parse
        if (value.IsEmpty || value.IsWhiteSpace())
        {
            return null;
        }

        // Try numeric first - accept any integer (including negative, out-of-range)
        // This matches cron parser behavior: parse anything, validate later
        if (int.TryParse(value, out var numeric))
        {
            return numeric;
        }

        // Try named (case-insensitive) - use Equals for Span comparison
        if (value.Equals("jan", StringComparison.OrdinalIgnoreCase)) return 1;
        if (value.Equals("feb", StringComparison.OrdinalIgnoreCase)) return 2;
        if (value.Equals("mar", StringComparison.OrdinalIgnoreCase)) return 3;
        if (value.Equals("apr", StringComparison.OrdinalIgnoreCase)) return 4;
        if (value.Equals("may", StringComparison.OrdinalIgnoreCase)) return 5;
        if (value.Equals("jun", StringComparison.OrdinalIgnoreCase)) return 6;
        if (value.Equals("jul", StringComparison.OrdinalIgnoreCase)) return 7;
        if (value.Equals("aug", StringComparison.OrdinalIgnoreCase)) return 8;
        if (value.Equals("sep", StringComparison.OrdinalIgnoreCase)) return 9;
        if (value.Equals("oct", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("nov", StringComparison.OrdinalIgnoreCase)) return 11;
        if (value.Equals("dec", StringComparison.OrdinalIgnoreCase)) return 12;

        // Not a number, not a valid name - can't parse
        return null;
    }

    private static TimeOnly? ParseTimeOfDay(ReadOnlySpan<char> second, ReadOnlySpan<char> minute, ReadOnlySpan<char> hour, NaturalIntervalUnit unit)
    {
        // Only parse time for daily/weekly intervals with fixed times
        if (unit == NaturalIntervalUnit.Seconds || unit == NaturalIntervalUnit.Minutes || unit == NaturalIntervalUnit.Hours)
        {
            return null;  // Sub-daily intervals don't have fixed time-of-day
        }

        // Parse hour, minute, and second for daily/weekly schedules
        if (!int.TryParse(hour, out var hourValue) || !int.TryParse(minute, out var minuteValue))
        {
            return null;
        }

        // Parse second if it's a specific value (not "*" or "*/n")
        var secondValue = 0;  // Default to 0 seconds
        if (int.TryParse(second, out var parsedSecond))
        {
            secondValue = parsedSecond;
        }

        // Validate ranges and construct TimeOnly
        if (hourValue is >= 0 and <= 23 &&
            minuteValue is >= 0 and <= 59 &&
            secondValue is >= 0 and <= 59)
        {
            return new TimeOnly(hourValue, minuteValue, secondValue);
        }

        return null;
    }

    /// <summary>
    /// Parse optional year field (1970-2099)
    /// Returns null if not specified or wildcard (*)
    /// </summary>
    private static int? ParseYear(ReadOnlySpan<char> yearPart)
    {
        // Empty or wildcard - no year constraint
        if (yearPart.IsEmpty || yearPart is "*")
        {
            return null;
        }

        // Parse year value
        if (int.TryParse(yearPart, out var year))
        {
            // Quartz spec: year range is 1970-2099
            // We don't validate here to match parser behavior (deferred validation)
            return year;
        }

        return null;
    }

    /// <summary>
    /// Parse range from cron field (e.g., "9-17", "0-30", "1-15", "9-17/2")
    /// Returns (null, null, null) if not a range
    /// </summary>
    private static (int? Start, int? End, int? Step) ParseRange(ReadOnlySpan<char> field)
    {
        // Check if field contains a dash (for ranges like "9-17")
        var dashIndex = field.IndexOf('-');
        if (field.IsEmpty || dashIndex < 0)
        {
            return (null, null, null);
        }

        // Skip if it's a wildcard or step pattern
        if (field is "*" || field is "?" || field.StartsWith("*/"))
        {
            return (null, null, null);
        }

        // Check for range+step: "9-17/2"
        int? step = null;
        var rangeField = field;

        var slashIndex = field.IndexOf('/');
        if (slashIndex > 0)
        {
            var stepSpan = field[(slashIndex + 1)..];
            // Parse step value - validation deferred to Quartz scheduler
            // This allows parsing of any integer value (including 0, negative, or very large)
            // Invalid values will be caught by Quartz during trigger creation
            if (int.TryParse(stepSpan, out var stepValue))
            {
                step = stepValue;
                rangeField = field[..slashIndex];
            }
        }

        // Re-find dash in the range field (not the original field)
        dashIndex = rangeField.IndexOf('-');

        // Parse range: "9-17" → (9, 17) or "9-17/2" → (9, 17, 2)
        if (dashIndex <= 0 || dashIndex >= rangeField.Length - 1) return (null, null, null);
        var startSpan = rangeField[..dashIndex];
        var endSpan = rangeField[(dashIndex + 1)..];

        if (int.TryParse(startSpan, out var start) && int.TryParse(endSpan, out var end))
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
    private static IReadOnlyList<int>? ParseList(ReadOnlySpan<char> field, int minValue, int maxValue)
    {
        // Check if field contains commas (for lists like "0,15,30,45")
        var commaIndex = field.IndexOf(',');
        if (field.IsEmpty || commaIndex < 0)
        {
            return null;
        }

        // Skip if it's a wildcard or step pattern
        if (field is "*" || field is "?" || field.StartsWith("*/"))
        {
            return null;
        }

        // Parse list with possible ranges: "0-4,8-12,20" → [0,1,2,3,4,8,9,10,11,12,20]
        var values = new List<int>();
        var start = 0;

        while (start < field.Length)
        {
            var nextComma = field[start..].IndexOf(',');
            var part = nextComma >= 0
                ? field.Slice(start, nextComma)
                : field[start..];

            // Check if this part is a range (e.g., "0-4")
            var dashIndex = part.IndexOf('-');
            if (dashIndex > 0 && dashIndex < part.Length - 1)  // Not at start or end
            {
                var rangeStart = part[..dashIndex];
                var rangeEnd = part[(dashIndex + 1)..];

                if (int.TryParse(rangeStart, out var s) &&
                    int.TryParse(rangeEnd, out var e) &&
                    s <= e &&
                    s >= minValue && e <= maxValue)
                {
                    // Expand range: 0-4 → [0, 1, 2, 3, 4]
                    for (var i = s; i <= e; i++)
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

            if (nextComma < 0) break;
            start += nextComma + 1;
        }

        // Remove duplicates and sort
        return values.Count >= 2 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }
}
