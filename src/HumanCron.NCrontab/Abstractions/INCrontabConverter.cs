using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;

namespace HumanCron.NCrontab.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and NCrontab 6-field cron expressions
/// </summary>
/// <remarks>
/// NCrontab format: {second} {minute} {hour} {day-of-month} {month} {day-of-week}
/// Used by Hangfire, Azure Functions Timer Triggers, and other NCrontab-based systems
/// </remarks>
public interface INCrontabConverter
{
    /// <summary>
    /// Convert natural language to NCrontab expression (6-field format)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "every 30 seconds", "every day at 2pm")</param>
    /// <returns>ParseResult containing NCrontab expression or error</returns>
    ParseResult<string> ToNCrontab(string naturalLanguage);

    /// <summary>
    /// Convert natural language to NCrontab expression with timezone support
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule</param>
    /// <param name="userTimezone">User's timezone for interpreting times (null = use system timezone)</param>
    /// <returns>ParseResult containing NCrontab expression or error</returns>
    ParseResult<string> ToNCrontab(string naturalLanguage, DateTimeZone? userTimezone);

    /// <summary>
    /// Convert natural language to NCrontab expression, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule</param>
    /// <param name="options">
    /// Parser options. Unlike the DateTimeZone? overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself, matching System.Text.Json's
    /// JsonSerializerOptions.
    /// </param>
    /// <returns>ParseResult containing NCrontab expression, or an Error if MinInterval is set and violated</returns>
    /// <example>
    /// <code>
    /// var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };
    /// var result = converter.ToNCrontab("every 5 seconds", options); // Error: runs more often than 15 minutes
    /// </code>
    /// </example>
    ParseResult<string> ToNCrontab(string naturalLanguage, ScheduleParserOptions options);

    /// <summary>
    /// Convert NCrontab expression to natural language
    /// </summary>
    /// <param name="ncrontabExpression">NCrontab expression (6-field format)</param>
    /// <returns>ParseResult containing natural language or error</returns>
    ParseResult<string> ToNaturalLanguage(string ncrontabExpression);
}
