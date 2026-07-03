using HumanCron.Models;
using System;

namespace HumanCron.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and durations/instants.
/// </summary>
/// <remarks>
/// Separate grammar from schedules (IHumanCronConverter) — durations have no "every"/"on"
/// prefix, so there is no collision.
///
/// Examples:
/// - "1 day 2 hours 30 minutes" ↔ TimeSpan
/// - "1d 2h 30m" → TimeSpan
/// - TimeSpan.FromMinutes(150) → "2 hours 30 minutes"
/// </remarks>
public interface IHumanDurationConverter
{
    /// <summary>
    /// Parse a natural language duration into a fixed-length TimeSpan
    /// </summary>
    /// <param name="duration">
    /// Natural language duration (e.g., "1 day 2 hours 30 minutes", "1d 2h 30m").
    /// Empty or whitespace-only input returns TimeSpan.Zero, not an error.
    /// </param>
    /// <returns>
    /// The parsed TimeSpan, or an Error if the duration contains month/year units (no fixed
    /// length — use ToFutureTime/ToNaturalDuration for calendar-aware math) or is unparseable.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = converter.ParseDuration("2 hours 30 minutes");
    /// if (result is ParseResult&lt;TimeSpan&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // 02:30:00
    /// }
    /// </code>
    /// </example>
    ParseResult<TimeSpan> ParseDuration(string duration);

    /// <summary>
    /// Format a TimeSpan as a natural language duration
    /// </summary>
    /// <param name="duration">The duration to format. May be negative.</param>
    /// <returns>
    /// Natural language duration (e.g., "2 hours 30 minutes"), with a single leading "-" for
    /// negative values, or an empty string for a zero or entirely sub-millisecond duration.
    /// </returns>
    /// <example>
    /// <code>
    /// converter.FormatDuration(TimeSpan.FromMinutes(150)); // "2 hours 30 minutes"
    /// converter.FormatDuration(TimeSpan.Zero); // ""
    /// </code>
    /// </example>
    string FormatDuration(TimeSpan duration);
}
