using HumanCron.Models;
using NodaTime;

namespace HumanCron.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and Unix 5-part cron expressions
/// </summary>
/// <remarks>
/// Simple string-to-string API - no intermediate types exposed.
///
/// Examples:
/// - "1d at 2pm" → "0 14 * * *"
/// - "0 14 * * *" → "1d at 2pm"
/// - "30m" → "*/30 * * * *"
/// - "1w on sunday at 3am" → "0 3 * * 0"
///
/// Unix 5-part cron format: minute hour day month dayOfWeek
/// </remarks>
public interface INaturalCronConverter
{
    /// <summary>
    /// Convert natural language to Unix 5-part cron expression
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "1d at 2pm")</param>
    /// <returns>Unix 5-part cron expression (e.g., "0 14 * * *")</returns>
    /// <example>
    /// <code>
    /// var result = converter.ToCron("1d at 2pm");
    /// if (result is ParseResult&lt;string&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // "0 14 * * *"
    /// }
    /// </code>
    /// </example>
    ParseResult<string> ToCron(string naturalLanguage);

    /// <summary>
    /// Convert natural language to Unix 5-part cron expression with timezone support
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "1d at 2pm")</param>
    /// <param name="userTimezone">
    /// User's timezone for interpreting times (null = use system timezone)
    /// Times are converted from user timezone to server timezone for Unix cron output
    /// Use IANA timezone IDs (e.g., DateTimeZoneProviders.Tzdb["America/New_York"])
    /// </param>
    /// <returns>Unix 5-part cron expression (e.g., "0 14 * * *")</returns>
    ParseResult<string> ToCron(string naturalLanguage, DateTimeZone? userTimezone);

    /// <summary>
    /// Convert Unix 5-part cron expression back to natural language
    /// </summary>
    /// <param name="cronExpression">Unix 5-part cron (e.g., "0 14 * * *")</param>
    /// <returns>Natural language schedule (e.g., "1d at 2pm")</returns>
    /// <example>
    /// <code>
    /// var result = converter.ToNaturalLanguage("0 14 * * *");
    /// if (result is ParseResult&lt;string&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // "1d at 2pm"
    /// }
    /// </code>
    /// </example>
    ParseResult<string> ToNaturalLanguage(string cronExpression);
}
