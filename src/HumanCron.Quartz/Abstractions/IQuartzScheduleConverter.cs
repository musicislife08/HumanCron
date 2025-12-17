using HumanCron.Models;
using Quartz;

namespace HumanCron.Quartz.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and Quartz.NET schedule builders
/// </summary>
/// <remarks>
/// Handles both simple patterns (CronScheduleBuilder) and complex patterns (CalendarIntervalScheduleBuilder).
/// Provides string-to-schedule and schedule-to-string conversion for Quartz.NET integration.
///
/// Examples:
/// - "1d at 2pm" → CronScheduleBuilder.DailyAtHourAndMinute(14, 0)
/// - "2w on sunday at 3am" → CalendarIntervalScheduleBuilder with 2-week interval
/// - CronScheduleBuilder → "1d at 2pm"
/// - CalendarIntervalScheduleBuilder → "2w on sunday at 3am"
/// </remarks>
public interface IQuartzScheduleConverter
{
    /// <summary>
    /// Convert natural language to Quartz schedule builder
    /// Returns CronScheduleBuilder for simple patterns, CalendarIntervalScheduleBuilder for complex patterns
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "2w on sunday at 3am")</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (default: 0 = SmartPolicy).
    /// Use constants from Quartz.MisfireInstruction.CronTrigger or Quartz.MisfireInstruction.CalendarIntervalTrigger
    /// </param>
    /// <returns>ParseResult with IScheduleBuilder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</returns>
    /// <example>
    /// <code>
    /// // Use default misfire handling (SmartPolicy)
    /// var result = converter.ToQuartzSchedule("1d at 2pm");
    ///
    /// // Skip missed executions using Quartz constant
    /// var result = converter.ToQuartzSchedule("1d at 2pm", Quartz.MisfireInstruction.CronTrigger.DoNothing);
    ///
    /// if (result is ParseResult&lt;IScheduleBuilder&gt;.Success success)
    /// {
    ///     var trigger = TriggerBuilder.Create()
    ///         .WithSchedule(success.Value)
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        int misfireInstruction = 0);

    /// <summary>
    /// Convert Quartz schedule builder back to natural language
    /// Supports both CronScheduleBuilder and CalendarIntervalScheduleBuilder
    /// </summary>
    /// <param name="scheduleBuilder">Quartz schedule builder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</param>
    /// <returns>ParseResult with natural language schedule string</returns>
    /// <example>
    /// <code>
    /// var builder = CronScheduleBuilder.DailyAtHourAndMinute(14, 0);
    /// var result = converter.ToNaturalLanguage(builder);
    /// if (result is ParseResult&lt;string&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // "1d at 2pm"
    /// }
    /// </code>
    /// </example>
    ParseResult<string> ToNaturalLanguage(IScheduleBuilder? scheduleBuilder);

    /// <summary>
    /// Create a pre-configured TriggerBuilder with schedule, start time, and misfire handling already set
    /// Convenience method that handles start time calculation for CalendarInterval schedules automatically
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "3w on sunday at 2pm")</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (default: 0 = SmartPolicy).
    /// Use constants from Quartz.MisfireInstruction.CronTrigger or Quartz.MisfireInstruction.CalendarIntervalTrigger
    /// </param>
    /// <returns>ParseResult with TriggerBuilder ready for job-specific configuration</returns>
    /// <example>
    /// <code>
    /// // Use default misfire handling (SmartPolicy)
    /// var result = converter.CreateTriggerBuilder("3w on sunday at 2pm");
    ///
    /// // Skip missed executions using Quartz constant
    /// var result = converter.CreateTriggerBuilder(
    ///     "every day at 2pm",
    ///     Quartz.MisfireInstruction.CronTrigger.DoNothing
    /// );
    ///
    /// // Fire all missed runs using Quartz constant
    /// var result = converter.CreateTriggerBuilder(
    ///     "every hour",
    ///     Quartz.MisfireInstruction.IgnoreMisfirePolicy
    /// );
    ///
    /// if (result is ParseResult&lt;TriggerBuilder&gt;.Success success)
    /// {
    ///     var trigger = success.Value
    ///         .WithIdentity("myTrigger", "myGroup")
    ///         .ForJob("myJob", "myJobGroup")
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<TriggerBuilder> CreateTriggerBuilder(
        string naturalLanguage,
        int misfireInstruction = 0);
}
