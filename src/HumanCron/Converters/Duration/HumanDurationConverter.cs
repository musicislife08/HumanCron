using HumanCron.Abstractions;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;
using System;

namespace HumanCron.Converters.Duration;

/// <summary>
/// Converts between natural language and durations/instants
/// Provides bidirectional conversion: natural language ↔ TimeSpan/DateTimeOffset
/// </summary>
public sealed class HumanDurationConverter : IHumanDurationConverter
{
    // Maximum input length to prevent DoS attacks via extremely long strings
    private const int MaxInputLength = 1000;

    private readonly IClock _clock;

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    /// <param name="clock">Clock for date/time operations (SystemClock.Instance in production, FakeClock in tests)</param>
    internal HumanDurationConverter(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Create a new duration converter (production use)
    /// </summary>
    public static HumanDurationConverter Create() => new(SystemClock.Instance);

    /// <inheritdoc/>
    public ParseResult<TimeSpan> ParseDuration(string duration)
    {
        duration ??= "";

        if (duration.Length > MaxInputLength)
        {
            return new ParseResult<TimeSpan>.Error(
                $"Duration input exceeds maximum length of {MaxInputLength} characters");
        }

        var parseResult = DurationParser.Parse(duration);
        if (parseResult is not ParseResult<Period>.Success success)
        {
            var error = (ParseResult<Period>.Error)parseResult;
            return new ParseResult<TimeSpan>.Error(error.Message);
        }

        var period = success.Value;
        if (period.Years != 0 || period.Months != 0)
        {
            return new ParseResult<TimeSpan>.Error(
                $"'{duration}' contains a calendar unit (months/years) with no fixed length. " +
                "Use ToFutureTime/ToNaturalDuration for calendar-aware math against an anchor.");
        }

        return new ParseResult<TimeSpan>.Success(period.ToDuration().ToTimeSpan());
    }

    /// <inheritdoc/>
    public string FormatDuration(TimeSpan duration)
    {
        var period = Period.FromTicks(duration.Ticks).Normalize();
        return DurationFormatter.Format(period);
    }
}
