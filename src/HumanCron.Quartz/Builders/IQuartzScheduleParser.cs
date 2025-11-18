using HumanCron.Models.Internal;
using HumanCron.Models;
using Quartz;

namespace HumanCron.Quartz;

/// <summary>
/// Parses Quartz.NET schedules back into ScheduleSpec
/// Supports both cron expressions and IScheduleBuilder instances
/// </summary>
internal interface IQuartzScheduleParser
{
    /// <summary>
    /// Parse a Quartz cron expression (6-part format) back to ScheduleSpec
    /// </summary>
    /// <param name="cronExpression">6-part Quartz cron expression (e.g., "0 0 14 * * ?")</param>
    /// <returns>ParseResult with ScheduleSpec or error message</returns>
    /// <example>
    /// <code>
    /// var result = parser.ParseCronExpression("0 0 14 * * ?");
    /// if (result is ParseResult&lt;ScheduleSpec&gt;.Success success)
    /// {
    ///     var spec = success.Value;  // 1d at 2pm
    /// }
    /// </code>
    /// </example>
    ParseResult<ScheduleSpec> ParseCronExpression(string cronExpression);

    /// <summary>
    /// Parse a Quartz IScheduleBuilder back to ScheduleSpec
    /// Supports CronScheduleBuilder and CalendarIntervalScheduleBuilder
    /// </summary>
    /// <param name="scheduleBuilder">Quartz schedule builder instance</param>
    /// <returns>ParseResult with ScheduleSpec or error message</returns>
    ParseResult<ScheduleSpec> ParseScheduleBuilder(IScheduleBuilder? scheduleBuilder);

    /// <summary>
    /// Parse a Quartz ITrigger back to ScheduleSpec
    /// Examines trigger's schedule builder to determine pattern
    /// </summary>
    /// <param name="trigger">Quartz trigger instance</param>
    /// <returns>ParseResult with ScheduleSpec or error message</returns>
    ParseResult<ScheduleSpec> ParseTrigger(ITrigger? trigger);
}
