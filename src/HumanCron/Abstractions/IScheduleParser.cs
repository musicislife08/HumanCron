using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;

namespace HumanCron.Abstractions;

/// <summary>
/// Parses natural language text into format-agnostic ScheduleSpec
/// INTERNAL: Used internally by converters
/// </summary>
/// <example>
/// <code>
/// var parser = new NaturalLanguageParser();
/// var options = new ScheduleParserOptions { TimeZone = TimeZoneInfo.Utc };
///
/// // Simple interval
/// var result1 = parser.Parse("30m", options);  // Every 30 minutes
///
/// // Interval with time
/// var result2 = parser.Parse("1d at 2pm", options);  // Daily at 2pm UTC
///
/// // Pattern matching on result
/// var schedule = result2 switch
/// {
///     ParseResult&lt;ScheduleSpec&gt;.Success(var spec) => ConvertToCron(spec),
///     ParseResult&lt;ScheduleSpec&gt;.Error(var message) => throw new Exception(message),
///     _ => throw new InvalidOperationException()
/// };
/// </code>
/// </example>
internal interface IScheduleParser
{
    /// <summary>
    /// Parse natural language text (e.g., "1d at 2pm") into ScheduleSpec
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule description (e.g., "30m", "1d at 2pm")</param>
    /// <param name="options">Parser options including timezone</param>
    /// <returns>Success with ScheduleSpec if parsing succeeded, Error with message if parsing failed</returns>
    ParseResult<ScheduleSpec> Parse(string naturalLanguage, ScheduleParserOptions options);
}
