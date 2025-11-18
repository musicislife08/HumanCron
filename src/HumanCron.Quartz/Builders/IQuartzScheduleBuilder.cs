using HumanCron.Models.Internal;
using System;
using Quartz;

namespace HumanCron.Quartz;

/// <summary>
/// Builds Quartz.NET schedule builders from ScheduleSpec
/// </summary>
internal interface IQuartzScheduleBuilder
{
    /// <summary>
    /// Build a Quartz schedule builder from the parsed schedule specification
    /// </summary>
    /// <param name="spec">The parsed schedule specification</param>
    /// <returns>Quartz schedule builder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</returns>
    /// <example>
    /// <code>
    /// var spec = parser.Parse("1d at 2pm", options);
    /// var builder = quartzBuilder.Build(spec.Value);
    /// var trigger = TriggerBuilder.Create()
    ///     .WithSchedule(builder)
    ///     .Build();
    /// </code>
    /// </example>
    IScheduleBuilder Build(ScheduleSpec spec);

    /// <summary>
    /// Calculate the appropriate start time for a schedule with constraints.
    /// For CalendarInterval schedules with day-of-week or day-of-month constraints,
    /// this calculates when the schedule should first fire to satisfy those constraints.
    /// </summary>
    /// <param name="spec">The parsed schedule specification</param>
    /// <param name="referenceTime">Optional reference time (defaults to UTC now)</param>
    /// <returns>The calculated start time, or null if no special start time is needed</returns>
    /// <example>
    /// <code>
    /// var spec = parser.Parse("2w on sunday at 2pm", options);
    /// var builder = quartzBuilder.Build(spec);
    /// var startTime = quartzBuilder.CalculateStartTime(spec);
    /// var trigger = TriggerBuilder.Create()
    ///     .WithSchedule(builder)
    ///     .StartAt(startTime ?? DateTimeOffset.UtcNow)
    ///     .Build();
    /// </code>
    /// </example>
    DateTimeOffset? CalculateStartTime(ScheduleSpec spec, DateTimeOffset? referenceTime = null);
}
