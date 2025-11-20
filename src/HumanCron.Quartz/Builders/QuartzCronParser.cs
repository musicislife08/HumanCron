using HumanCron.Models.Internal;
using HumanCron.Models;
using System;

namespace HumanCron.Quartz;

/// <summary>
/// Parses Quartz 6-part cron expressions back into ScheduleSpec
/// Format: second minute hour day month dayOfWeek
/// </summary>
internal sealed partial class QuartzCronParser
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
        var parsedDayOfWeekList = ParseDayOfWeekList(dayOfWeek);
        var (dayOfWeekStart, dayOfWeekEnd) = ParseDayOfWeekRange(dayOfWeek);

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
            DayOfWeekList = parsedDayOfWeekList,
            DayOfWeekStart = dayOfWeekStart,
            DayOfWeekEnd = dayOfWeekEnd,
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
}
