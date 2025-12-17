using Quartz;
using System;

namespace HumanCron.Quartz.Helpers;

/// <summary>
/// Helper for applying Quartz misfire instructions to schedule builders
/// </summary>
internal static class MisfireInstructionHelper
{
    /// <summary>
    /// Apply Quartz misfire instruction to a CronScheduleBuilder
    /// </summary>
    /// <param name="builder">The Cron schedule builder</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (0 = default/SmartPolicy, no action taken).
    /// Use constants from Quartz.MisfireInstruction.CronTrigger
    /// </param>
    /// <returns>The builder with misfire instruction applied</returns>
    public static CronScheduleBuilder ApplyMisfireInstruction(
        CronScheduleBuilder builder,
        int misfireInstruction = 0)
    {
        // Map Quartz misfire instruction constants to builder methods
        // 0 = SmartPolicy (default), -1 = IgnoreMisfirePolicy
        // CronTrigger: 1 = FireOnceNow (method: FireAndProceed), 2 = DoNothing
        return misfireInstruction switch
        {
            0 => builder, // SmartPolicy - don't call any method, use Quartz default
            -1 => builder.WithMisfireHandlingInstructionIgnoreMisfires(), // IgnoreMisfirePolicy
            1 => builder.WithMisfireHandlingInstructionFireAndProceed(), // FireOnceNow constant
            2 => builder.WithMisfireHandlingInstructionDoNothing(), // DoNothing
            _ => throw new ArgumentOutOfRangeException(
                nameof(misfireInstruction),
                misfireInstruction,
                $"Unknown misfire instruction value: {misfireInstruction}. " +
                $"Use constants from Quartz.MisfireInstruction.CronTrigger")
        };
    }

    /// <summary>
    /// Apply Quartz misfire instruction to a CalendarIntervalScheduleBuilder
    /// </summary>
    /// <param name="builder">The calendar interval schedule builder</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (0 = default/SmartPolicy, no action taken).
    /// Use constants from Quartz.MisfireInstruction.CalendarIntervalTrigger
    /// </param>
    /// <returns>The builder with misfire instruction applied</returns>
    public static CalendarIntervalScheduleBuilder ApplyMisfireInstruction(
        CalendarIntervalScheduleBuilder builder,
        int misfireInstruction = 0)
    {
        // Map Quartz misfire instruction constants to builder methods
        // 0 = SmartPolicy (default), -1 = IgnoreMisfirePolicy
        // CalendarIntervalTrigger: 1 = FireOnceNow (method: FireAndProceed), 2 = DoNothing
        return misfireInstruction switch
        {
            0 => builder, // SmartPolicy - don't call any method, use Quartz default
            -1 => builder.WithMisfireHandlingInstructionIgnoreMisfires(), // IgnoreMisfirePolicy
            1 => builder.WithMisfireHandlingInstructionFireAndProceed(), // FireOnceNow constant
            2 => builder.WithMisfireHandlingInstructionDoNothing(), // DoNothing
            _ => throw new ArgumentOutOfRangeException(
                nameof(misfireInstruction),
                misfireInstruction,
                $"Unknown misfire instruction value: {misfireInstruction}. " +
                $"Use constants from Quartz.MisfireInstruction.CalendarIntervalTrigger")
        };
    }

    /// <summary>
    /// Apply Quartz misfire instruction to any IScheduleBuilder (type-safe dispatch)
    /// </summary>
    /// <param name="builder">The schedule builder</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (0 = default/SmartPolicy, no action taken)
    /// </param>
    /// <returns>The builder with misfire instruction applied</returns>
    /// <exception cref="NotSupportedException">Thrown when builder type is not supported</exception>
    public static IScheduleBuilder ApplyMisfireInstruction(
        IScheduleBuilder builder,
        int misfireInstruction = 0)
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
