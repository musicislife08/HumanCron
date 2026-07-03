using HumanCron.Models;
using NodaTime;
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

    /// <summary>
    /// Parse a natural language duration and add it to an anchor instant, producing a future
    /// (or past, if the duration is negative) DateTimeOffset
    /// </summary>
    /// <param name="duration">
    /// Natural language duration, including month/year units (e.g., "2 hours", "1 month")
    /// </param>
    /// <param name="anchor">Anchor instant (null = now, via the converter's injected clock)</param>
    /// <param name="timeZone">
    /// Governs precision for month/year components only. Null (default) uses Mode 1: naive,
    /// offset-preserving math - cheap, but can be off by up to an hour across a DST boundary
    /// when the duration includes month/year units. A non-null zone uses Mode 2: precise,
    /// DST-aware math via NodaTime's ZonedDateTime and DateTimeZone.AtLeniently. Fixed-unit-only
    /// durations (hours/minutes/seconds/days/weeks) produce the identical result either way.
    /// </param>
    /// <returns>The computed instant, or an Error if the duration is unparseable</returns>
    /// <example>
    /// <code>
    /// var result = converter.ToFutureTime("2 hours");
    /// if (result is ParseResult&lt;DateTimeOffset&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // now + 2 hours
    /// }
    /// </code>
    /// </example>
    ParseResult<DateTimeOffset> ToFutureTime(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null);

    /// <summary>
    /// Format the difference between a target instant and an anchor as a natural language duration
    /// </summary>
    /// <param name="target">The instant to describe, relative to the anchor</param>
    /// <param name="anchor">Anchor instant (null = now, via the converter's injected clock)</param>
    /// <param name="timeZone">
    /// Governs precision for calendar-unit decomposition (see ToFutureTime). Null (default) uses
    /// Mode 1: naive math on each value's own embedded offset. A non-null zone uses Mode 2:
    /// resolves both instants through that zone before diffing.
    /// </param>
    /// <returns>
    /// Natural language duration (e.g., "1 hour 23 minutes"). A target before the anchor formats
    /// with a leading "-", same as a negative TimeSpan. Never errors - target/anchor are always
    /// valid DateTimeOffset values.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = converter.ToNaturalDuration(unfreezeAt, syncCompletedAt);
    /// // ParseResult&lt;string&gt;.Success("2 hours")
    /// </code>
    /// </example>
    ParseResult<string> ToNaturalDuration(
        DateTimeOffset target,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null);
}
