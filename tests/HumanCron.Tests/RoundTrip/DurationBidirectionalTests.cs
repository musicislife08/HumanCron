using HumanCron.Abstractions;
using HumanCron.Converters.Duration;
using HumanCron.Models;
using NodaTime;

namespace HumanCron.Tests.RoundTrip;

/// <summary>
/// Round-trip tests for Layer 1 (string ↔ TimeSpan), in the spirit of CompleteBidirectionalTests.
/// </summary>
[TestFixture]
public class DurationBidirectionalTests
{
    private IHumanDurationConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = HumanDurationConverter.Create();
    }

    [TestCase(150 * 60_000L)] // 2h30m in ms
    [TestCase(90 * 60_000L)]  // 1h30m in ms
    [TestCase(1_000L)]        // 1 second
    [TestCase(500L)]          // 500 milliseconds
    [TestCase(0L)]
    [TestCase(-150 * 60_000L)]
    public void RoundTrip_FormatThenParse_ReturnsOriginalValue(long milliseconds)
    {
        var original = TimeSpan.FromMilliseconds(milliseconds);

        var formatted = _converter.FormatDuration(original);
        var parseResult = _converter.ParseDuration(formatted);

        Assert.That(parseResult, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var parsed = ((ParseResult<TimeSpan>.Success)parseResult).Value;
        Assert.That(parsed, Is.EqualTo(original));
    }

    [Test]
    public void RoundTrip_ParseThenFormat_NormalizesToCanonicalForm()
    {
        // "90 minutes" is not what the formatter would ever emit (it normalizes to
        // hours+minutes), but parsing it must still succeed and round-trip through
        // the canonical form.
        var parseResult = _converter.ParseDuration("90 minutes");
        Assert.That(parseResult, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var parsed = ((ParseResult<TimeSpan>.Success)parseResult).Value;

        var formatted = _converter.FormatDuration(parsed);

        Assert.That(formatted, Is.EqualTo("1 hour 30 minutes"));
    }

    [Test]
    public void RoundTrip_AnchoredFixedUnit_ToFutureTimeThenToNaturalDuration()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var futureResult = _converter.ToFutureTime("2 hours 30 minutes", anchor);
        Assert.That(futureResult, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var target = ((ParseResult<DateTimeOffset>.Success)futureResult).Value;

        var naturalResult = _converter.ToNaturalDuration(target, anchor);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)naturalResult).Value, Is.EqualTo("2 hours 30 minutes"));
    }

    [Test]
    public void RoundTrip_AnchoredCalendarUnit_PreciseMode_RoundTripsExactly()
    {
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        var futureResult = _converter.ToFutureTime("1 month", anchor, newYork);
        Assert.That(futureResult, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var target = ((ParseResult<DateTimeOffset>.Success)futureResult).Value;

        var naturalResult = _converter.ToNaturalDuration(target, anchor, newYork);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)naturalResult).Value, Is.EqualTo("1 month"));
    }
}
