using Quartz;
using System;

namespace HumanCron.Quartz.Helpers;

/// <summary>
/// Applies Quartz misfire instructions to schedule builders.
/// </summary>
/// <remarks>
/// A recurring HumanCron schedule produces either a <see cref="CronScheduleBuilder"/> or a
/// <see cref="CalendarIntervalScheduleBuilder"/>, and the caller cannot know which in advance.
/// Both families share the same four policies with identical names and values
/// (SmartPolicy, IgnoreMisfires, FireAndProceed, DoNothing), so the public API accepts
/// <see cref="CronTriggerMisfireInstruction"/> and this helper casts it when the schedule
/// turns out to be calendar-interval based. Quartz validates the value when the trigger is built.
/// </remarks>
internal static class MisfireInstructionHelper
{
    /// <summary>
    /// Apply a misfire instruction to a <see cref="CronScheduleBuilder"/>.
    /// </summary>
    public static CronScheduleBuilder ApplyMisfireInstruction(
        CronScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction(misfireInstruction);
    }

    /// <summary>
    /// Apply a cron-family misfire instruction to a <see cref="CalendarIntervalScheduleBuilder"/>.
    /// The two families share names and values, so the cast is exact.
    /// </summary>
    public static CalendarIntervalScheduleBuilder ApplyMisfireInstruction(
        CalendarIntervalScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction((CalendarIntervalTriggerMisfireInstruction)misfireInstruction);
    }

    /// <summary>
    /// Apply a misfire instruction to a <see cref="SimpleScheduleBuilder"/> (one-time triggers).
    /// </summary>
    public static SimpleScheduleBuilder ApplyMisfireInstruction(
        SimpleScheduleBuilder builder,
        SimpleTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction(misfireInstruction);
    }

    /// <summary>
    /// Apply a recurring-schedule misfire instruction to whichever recurring builder was produced.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the builder is not a cron or calendar-interval builder.</exception>
    public static IScheduleBuilder ApplyMisfireInstruction(
        IScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder switch
        {
            CronScheduleBuilder cronBuilder => ApplyMisfireInstruction(cronBuilder, misfireInstruction),
            CalendarIntervalScheduleBuilder calendarBuilder => ApplyMisfireInstruction(calendarBuilder, misfireInstruction),
            _ => throw new NotSupportedException(
                $"Misfire instruction application is not supported for builder type: {builder.GetType().Name}")
        };
    }
}
