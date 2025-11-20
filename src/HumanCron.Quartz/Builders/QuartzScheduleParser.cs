using HumanCron.Models.Internal;
using HumanCron.Models;
using HumanCron.Quartz.Helpers;
using Quartz;
using System;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;
using QuartzIntervalUnit = Quartz.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// Parses Quartz.NET schedules back into ScheduleSpec
/// Routes to appropriate parser based on schedule type
/// </summary>
internal sealed class QuartzScheduleParser : IQuartzScheduleParser
{
    private readonly QuartzCronParser _cronParser = new();

    public ParseResult<ScheduleSpec> ParseCronExpression(string cronExpression)
    {
        return _cronParser.Parse(cronExpression);
    }

    public ParseResult<ScheduleSpec> ParseScheduleBuilder(IScheduleBuilder? scheduleBuilder)
    {
        if (scheduleBuilder == null)
        {
            return new ParseResult<ScheduleSpec>.Error("Schedule builder cannot be null");
        }

        // Build a temporary trigger to extract the schedule details
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"temp-{Guid.NewGuid()}")
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        return ParseTrigger(trigger);
    }

    public ParseResult<ScheduleSpec> ParseTrigger(ITrigger? trigger)
    {
        if (trigger == null)
        {
            return new ParseResult<ScheduleSpec>.Error("Trigger cannot be null");
        }

        return trigger switch
        {
            ICronTrigger cronTrigger => ParseCronTrigger(cronTrigger),
            ICalendarIntervalTrigger calendarTrigger => ParseCalendarIntervalTrigger(calendarTrigger),
            _ => new ParseResult<ScheduleSpec>.Error($"Unsupported trigger type: {trigger.GetType().Name}")
        };
    }

    private ParseResult<ScheduleSpec> ParseCronTrigger(ICronTrigger cronTrigger)
    {
        var cronExpression = cronTrigger.CronExpressionString;
        if (string.IsNullOrEmpty(cronExpression))
        {
            return new ParseResult<ScheduleSpec>.Error("Cron trigger has no cron expression");
        }

        var result = _cronParser.Parse(cronExpression);

        // Apply timezone (convert BCL → NodaTime)
        if (result is not ParseResult<ScheduleSpec>.Success success) return result;
        var dateTimeZone = TimeZoneConverter.ToDateTimeZone(cronTrigger.TimeZone);
        var spec = success.Value with { TimeZone = dateTimeZone };
        return new ParseResult<ScheduleSpec>.Success(spec);

    }

    private ParseResult<ScheduleSpec> ParseCalendarIntervalTrigger(ICalendarIntervalTrigger calendarTrigger)
    {
        var (interval, unit) = calendarTrigger.RepeatIntervalUnit switch
        {
            // WORKAROUND: Quartz bug #1035 - we convert weeks to days in the builder
            // When parsing back, recognize day intervals that are multiples of 7 as weeks
            QuartzIntervalUnit.Day when calendarTrigger.RepeatInterval % 7 == 0
                => (calendarTrigger.RepeatInterval / 7, NaturalIntervalUnit.Weeks),

            QuartzIntervalUnit.Week => (calendarTrigger.RepeatInterval, NaturalIntervalUnit.Weeks),
            QuartzIntervalUnit.Month => (calendarTrigger.RepeatInterval, NaturalIntervalUnit.Months),
            QuartzIntervalUnit.Year => (calendarTrigger.RepeatInterval, NaturalIntervalUnit.Years),
            _ => (0, NaturalIntervalUnit.Days)
        };

        if (interval == 0)
        {
            return new ParseResult<ScheduleSpec>.Error(
                $"Unsupported calendar interval unit: {calendarTrigger.RepeatIntervalUnit}");
        }

        var spec = new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            TimeZone = TimeZoneConverter.ToDateTimeZone(calendarTrigger.TimeZone)
        };

        return new ParseResult<ScheduleSpec>.Success(spec);
    }
}
