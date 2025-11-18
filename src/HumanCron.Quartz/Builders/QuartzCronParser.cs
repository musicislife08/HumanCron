using HumanCron.Models.Internal;
using HumanCron.Models;
using System;
using System.Collections.Generic;
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
        ReadOnlySpan<char> cronSpan = cronExpression.AsSpan();

        // Parse into parts without allocating (allocate 8 slots to detect invalid expressions with > 6 parts)
        Span<Range> ranges = stackalloc Range[8];
        int partCount = cronSpan.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (partCount != 6)
        {
            return new ParseResult<ScheduleSpec>.Error($"Quartz cron expressions must have 6 parts (got {partCount}). Format: second minute hour day month dayOfWeek");
        }

        var second = cronSpan[ranges[0]];
        var minute = cronSpan[ranges[1]];
        var hour = cronSpan[ranges[2]];
        var day = cronSpan[ranges[3]];
        var month = cronSpan[ranges[4]];
        var dayOfWeek = cronSpan[ranges[5]];

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

        var spec = new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = parsedDayOfWeek,
            DayPattern = parsedDayPattern,
            Month = parsedMonth,
            TimeOfDay = timeOfDay
        };

        return new ParseResult<ScheduleSpec>.Success(spec);
    }

    private static (int Interval, NaturalIntervalUnit Unit) DetermineInterval(
        ReadOnlySpan<char> second, ReadOnlySpan<char> minute, ReadOnlySpan<char> hour, ReadOnlySpan<char> day, ReadOnlySpan<char> dayOfWeek)
    {
        // Pattern: */30 * * * * ? → Every 30 seconds
        if (second.StartsWith("*/") || second.SequenceEqual("*"))
        {
            if (second.SequenceEqual("*"))
            {
                return (1, NaturalIntervalUnit.Seconds);
            }
            var interval = int.Parse(second[2..]);
            return (interval, NaturalIntervalUnit.Seconds);
        }

        // Pattern: 0 */15 * * * ? → Every 15 minutes
        if (minute.StartsWith("*/") || (minute.SequenceEqual("*") && hour.SequenceEqual("*")))
        {
            if (minute.SequenceEqual("*"))
            {
                return (1, NaturalIntervalUnit.Minutes);
            }
            var interval = int.Parse(minute[2..]);
            return (interval, NaturalIntervalUnit.Minutes);
        }

        // Pattern: 0 0 */6 * * ? → Every 6 hours
        if (hour.StartsWith("*/") || (hour.SequenceEqual("*") && day.SequenceEqual("*") && dayOfWeek.SequenceEqual("?")))
        {
            if (hour.SequenceEqual("*"))
            {
                return (1, NaturalIntervalUnit.Hours);
            }
            var interval = int.Parse(hour[2..]);
            return (interval, NaturalIntervalUnit.Hours);
        }

        // Pattern: 0 0 14 * * ? → Daily at specific time
        if (day.SequenceEqual("*") && dayOfWeek.SequenceEqual("?"))
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
        if (!dayOfWeek.SequenceEqual("?") && day.SequenceEqual("?"))
        {
            return (1, NaturalIntervalUnit.Weeks);
        }

        // Default to daily if we can't determine
        return (1, NaturalIntervalUnit.Days);
    }

    private static DayOfWeek? ParseDayOfWeek(ReadOnlySpan<char> dayOfWeekPart)
    {
        if (dayOfWeekPart.SequenceEqual("?") || dayOfWeekPart.SequenceEqual("*"))
        {
            return null;
        }

        // Handle day ranges (MON-FRI, SAT,SUN) - return null, these become DayPattern
        if (dayOfWeekPart.Contains('-') || dayOfWeekPart.Contains(','))
        {
            return null;
        }

        // Single day - use case-insensitive comparison
        if (dayOfWeekPart.Equals("SUN", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("1"))
            return DayOfWeek.Sunday;
        if (dayOfWeekPart.Equals("MON", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("2"))
            return DayOfWeek.Monday;
        if (dayOfWeekPart.Equals("TUE", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("3"))
            return DayOfWeek.Tuesday;
        if (dayOfWeekPart.Equals("WED", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("4"))
            return DayOfWeek.Wednesday;
        if (dayOfWeekPart.Equals("THU", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("5"))
            return DayOfWeek.Thursday;
        if (dayOfWeekPart.Equals("FRI", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("6"))
            return DayOfWeek.Friday;
        if (dayOfWeekPart.Equals("SAT", StringComparison.OrdinalIgnoreCase) || dayOfWeekPart.SequenceEqual("7"))
            return DayOfWeek.Saturday;

        return null;
    }

    private static DayPattern? ParseDayPattern(ReadOnlySpan<char> dayOfWeekPart)
    {
        if (dayOfWeekPart.Equals("MON-FRI", StringComparison.OrdinalIgnoreCase))
            return DayPattern.Weekdays;
        if (dayOfWeekPart.Equals("SAT,SUN", StringComparison.OrdinalIgnoreCase))
            return DayPattern.Weekends;

        return null;
    }

    private static MonthSpecifier ParseMonth(ReadOnlySpan<char> monthPart)
    {
        // "*" means all months (no constraint)
        if (monthPart.SequenceEqual("*"))
        {
            return new MonthSpecifier.None();
        }

        // Month range: "1-3" (January through March)
        int dashIndex = monthPart.IndexOf('-');
        if (dashIndex > 0)
        {
            var startSpan = monthPart[..dashIndex];
            var endSpan = monthPart[(dashIndex + 1)..];

            if (int.TryParse(startSpan, out var start) && int.TryParse(endSpan, out var end))
            {
                return new MonthSpecifier.Range(start, end);
            }
        }

        // Month list: "1,4,7,10" (January, April, July, October - quarterly)
        if (monthPart.Contains(','))
        {
            List<int> months = [];
            Span<Range> ranges = stackalloc Range[12]; // Max 12 months
            int count = monthPart.Split(ranges, ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = 0; i < count; i++)
            {
                if (int.TryParse(monthPart[ranges[i]], out var month))
                {
                    months.Add(month);
                }
            }

            if (months.Count > 0)
            {
                return new MonthSpecifier.List(months);
            }
        }

        // Single month: "1" (January only)
        if (int.TryParse(monthPart, out var singleMonth))
        {
            return new MonthSpecifier.Single(singleMonth);
        }

        // Default to None if we can't parse
        return new MonthSpecifier.None();
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
}
