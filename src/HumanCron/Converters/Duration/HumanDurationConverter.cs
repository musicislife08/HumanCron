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

    /// <inheritdoc/>
    public ParseResult<DateTimeOffset> ToFutureTime(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null)
    {
        duration ??= "";

        if (duration.Length > MaxInputLength)
        {
            return new ParseResult<DateTimeOffset>.Error(
                $"Duration input exceeds maximum length of {MaxInputLength} characters");
        }

        var parseResult = DurationParser.Parse(duration);
        if (parseResult is not ParseResult<Period>.Success success)
        {
            var error = (ParseResult<Period>.Error)parseResult;
            return new ParseResult<DateTimeOffset>.Error(error.Message);
        }

        var period = success.Value;
        var anchorValue = anchor ?? _clock.GetCurrentInstant().ToDateTimeOffset();

        var result = timeZone is null
            ? AddPeriodNaive(anchorValue, period)
            : AddPeriodPrecise(anchorValue, period, timeZone);

        return new ParseResult<DateTimeOffset>.Success(result);
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNaturalDuration(
        DateTimeOffset target,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null)
    {
        var anchorValue = anchor ?? _clock.GetCurrentInstant().ToDateTimeOffset();

        var period = timeZone is null
            ? PeriodBetweenNaive(anchorValue, target)
            : PeriodBetweenPrecise(anchorValue, target, timeZone);

        return new ParseResult<string>.Success(DurationFormatter.Format(period));
    }

    // Splits a Period into its calendar component (Years/Months - navigated via LocalDateTime,
    // which is where DST/month-end ambiguity actually lives) and its fixed component (Weeks/
    // Days/Hours/Minutes/Seconds/Milliseconds - added as a physical Duration, which is DST-
    // agnostic by construction). Without this split, adding the WHOLE period through
    // LocalDateTime.Plus would make even a plain "90 minutes" sensitive to DST whenever the
    // addition straddles a transition - which contradicts "fixed-unit durations produce the
    // identical result under both modes" and was caught by testing AddPeriodNaive/
    // AddPeriodPrecise against a DST boundary before this split existed.
    private static Period CalendarOnly(Period period) =>
        new PeriodBuilder { Years = period.Years, Months = period.Months }.Build();

    private static NodaTime.Duration FixedDuration(Period period) =>
        new PeriodBuilder
        {
            Weeks = period.Weeks,
            Days = period.Days,
            Hours = period.Hours,
            Minutes = period.Minutes,
            Seconds = period.Seconds,
            Milliseconds = period.Milliseconds
        }.Build().ToDuration();

    private static DateTimeOffset AddPeriodNaive(DateTimeOffset anchor, Period period)
    {
        var offsetDateTime = OffsetDateTime.FromDateTimeOffset(anchor);
        var afterCalendar = new OffsetDateTime(
            offsetDateTime.LocalDateTime.Plus(CalendarOnly(period)), offsetDateTime.Offset);
        return afterCalendar.Plus(FixedDuration(period)).ToDateTimeOffset();
    }

    private static DateTimeOffset AddPeriodPrecise(DateTimeOffset anchor, Period period, DateTimeZone timeZone)
    {
        var zonedDateTime = Instant.FromDateTimeOffset(anchor).InZone(timeZone);
        var afterCalendar = timeZone.AtLeniently(zonedDateTime.LocalDateTime.Plus(CalendarOnly(period)));
        return afterCalendar.Plus(FixedDuration(period)).ToDateTimeOffset();
    }

    private static Period PeriodBetweenNaive(DateTimeOffset anchor, DateTimeOffset target)
    {
        var anchorLocal = OffsetDateTime.FromDateTimeOffset(anchor).LocalDateTime;
        var targetLocal = OffsetDateTime.FromDateTimeOffset(target).LocalDateTime;
        return Period.Between(anchorLocal, targetLocal);
    }

    private static Period PeriodBetweenPrecise(DateTimeOffset anchor, DateTimeOffset target, DateTimeZone timeZone)
    {
        var anchorLocal = Instant.FromDateTimeOffset(anchor).InZone(timeZone).LocalDateTime;
        var targetLocal = Instant.FromDateTimeOffset(target).InZone(timeZone).LocalDateTime;
        return Period.Between(anchorLocal, targetLocal);
    }
}
