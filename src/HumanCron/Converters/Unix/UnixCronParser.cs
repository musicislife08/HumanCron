using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using HumanCron.Models;

namespace HumanCron.Converters.Unix;

/// <summary>
/// Parses Unix 5-part cron expressions back into ScheduleSpec
/// Format: minute hour day month dayOfWeek
/// </summary>
internal sealed class UnixCronParser
{
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

            var spec = new ScheduleSpec
            {
                Interval = interval,
                Unit = unit,
                DayOfWeek = parsedDayOfWeek,
                DayPattern = parsedDayPattern,
                Month = monthSpecifier,
                TimeOfDay = timeOfDay
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
            if (interval > 1000)
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
            if (interval > 1000)
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
            if (interval > 1000)
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

        // Default to daily if we can't determine
        return (1, IntervalUnit.Days);
    }

    private static DayOfWeek? ParseDayOfWeek(string dayOfWeekPart)
    {
        if (dayOfWeekPart == "*")
        {
            return null;
        }

        // Handle day ranges (1-5, 0,6) - return null, these become DayPattern
        if (dayOfWeekPart.Contains('-') || dayOfWeekPart.Contains(','))
        {
            return null;
        }

        // Single day (0-7, where 0 and 7 both = Sunday)
        return dayOfWeekPart switch
        {
            "0" or "7" => DayOfWeek.Sunday,
            "1" => DayOfWeek.Monday,
            "2" => DayOfWeek.Tuesday,
            "3" => DayOfWeek.Wednesday,
            "4" => DayOfWeek.Thursday,
            "5" => DayOfWeek.Friday,
            "6" => DayOfWeek.Saturday,
            _ => null
        };
    }

    private static DayPattern? ParseDayPattern(string dayOfWeekPart)
    {
        return dayOfWeekPart switch
        {
            "1-5" => DayPattern.Weekdays,   // Monday-Friday
            "0,6" or "6,0" => DayPattern.Weekends,  // Sunday,Saturday
            _ => null
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

        // Range: 1-3 (january through march)
        if (monthPart.Contains('-'))
        {
            var parts = monthPart.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var start) &&
                int.TryParse(parts[1], out var end) &&
                start is >= 1 and <= 12 &&
                end is >= 1 and <= 12 &&
                start < end)
            {
                return new MonthSpecifier.Range(start, end);
            }
            // Invalid range - fall through to None
            return new MonthSpecifier.None();
        }

        // List: 1,4,7,10 (quarterly)
        if (monthPart.Contains(','))
        {
            var parts = monthPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var months = new List<int>();

            foreach (var part in parts)
            {
                if (int.TryParse(part, out var month) && month is >= 1 and <= 12)
                {
                    months.Add(month);
                }
            }

            if (months.Count >= 2)
            {
                return new MonthSpecifier.List(months);
            }
            // Invalid list - fall through to None
            return new MonthSpecifier.None();
        }

        // Single month: 1 (january)
        if (int.TryParse(monthPart, out var singleMonth) && singleMonth is >= 1 and <= 12)
        {
            return new MonthSpecifier.Single(singleMonth);
        }

        // Default: all months
        return new MonthSpecifier.None();
    }
}
