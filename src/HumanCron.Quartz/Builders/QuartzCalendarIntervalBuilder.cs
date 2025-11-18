using HumanCron.Models.Internal;
using Quartz;
using System;
using HumanCron.Quartz.Helpers;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// Builds Quartz CalendarIntervalScheduleBuilder from ScheduleSpec
/// Used for patterns that cron cannot express: multi-week, monthly, yearly intervals
/// </summary>
internal sealed class QuartzCalendarIntervalBuilder
{
    public IScheduleBuilder Build(ScheduleSpec spec)
    {
        // Note: Day-of-week, day-of-month, and time-of-day constraints are handled
        // by calculating an appropriate StartAt time via CalculateStartTime().
        // CalendarIntervalSchedule will then repeat from that aligned start point.
        //
        // IMPORTANT: We do NOT set .InTimeZone() when day-of-week or day-of-month constraints
        // are present because:
        // 1. StartAt time is already calculated with timezone conversion
        // 2. Setting both StartAt (UTC) and InTimeZone causes Quartz to incorrectly recalculate
        //    the first fire time in the specified timezone, ignoring our StartAt

        // Only day patterns (weekdays/weekends) cannot be supported with multi-interval schedules
        if (spec.DayPattern.HasValue)
        {
            throw new NotSupportedException(
                $"Day patterns (weekdays/weekends) are not supported with multi-{spec.Unit.ToString().ToLowerInvariant()} intervals. " +
                $"Pattern '{spec.Interval}{GetUnitChar(spec.Unit)} on {spec.DayPattern.Value.ToString().ToLowerInvariant()}' cannot be expressed " +
                $"because CalendarIntervalSchedule cannot skip specific days within an interval.\n" +
                $"Workaround: Use daily intervals (1d on {spec.DayPattern.Value.ToString().ToLowerInvariant()}).");
        }

        var builder = CalendarIntervalScheduleBuilder.Create();

        // Set the interval unit and value
        // WORKAROUND: Quartz bug #1035 - WithIntervalInWeeks() ignores StartAt
        // Convert weeks to days to properly respect StartAt time
        builder = spec.Unit switch
        {
            NaturalIntervalUnit.Weeks => builder.WithIntervalInDays(spec.Interval * 7),
            NaturalIntervalUnit.Months => builder.WithIntervalInMonths(spec.Interval),
            NaturalIntervalUnit.Years => builder.WithIntervalInYears(spec.Interval),
            _ => throw new InvalidOperationException($"CalendarInterval does not support unit: {spec.Unit}")
        };

        // Set timezone for interval calculations
        // This ensures "2am" stays "2am" across DST changes and future intervals
        var timeZoneInfo = TimeZoneConverter.ToTimeZoneInfo(spec.TimeZone);
        builder = builder.InTimeZone(timeZoneInfo);

        return builder;
    }

    private static string GetUnitChar(NaturalIntervalUnit unit) => unit switch
    {
        NaturalIntervalUnit.Weeks => "w",
        NaturalIntervalUnit.Months => "M",
        NaturalIntervalUnit.Years => "y",
        _ => "?"
    };
}
