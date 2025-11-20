using HumanCron.Models.Internal;
using System;
using HumanCron.Models;

namespace HumanCron.Converters.Unix;

/// <summary>
/// Parses Unix 5-part cron expressions back into ScheduleSpec
/// Format: minute hour day month dayOfWeek
/// </summary>
internal sealed partial class UnixCronParser
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
            var parsedDayOfWeekList = ParseDayOfWeekList(dayOfWeek);
            var (dayOfWeekStart, dayOfWeekEnd) = ParseDayOfWeekRange(dayOfWeek);

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
                DayOfWeekList = parsedDayOfWeekList,
                DayOfWeekStart = dayOfWeekStart,
                DayOfWeekEnd = dayOfWeekEnd,
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
}
