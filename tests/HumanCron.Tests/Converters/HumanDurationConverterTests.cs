using HumanCron.Abstractions;
using HumanCron.Converters.Duration;
using HumanCron.Models;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Converters;

[TestFixture]
public class HumanDurationConverterTests
{
    private IHumanDurationConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = HumanDurationConverter.Create();
    }

    [Test]
    public void ParseDuration_SimpleValue_ReturnsExpectedTimeSpan()
    {
        var result = _converter.ParseDuration("2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void ParseDuration_Compound_ReturnsSummedTimeSpan()
    {
        var result = _converter.ParseDuration("1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(new TimeSpan(1, 2, 30, 0)));
    }

    [Test]
    public void ParseDuration_Negative_ReturnsNegativeTimeSpan()
    {
        var result = _converter.ParseDuration("-2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.FromHours(-2)));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ParseDuration_EmptyOrWhitespace_ReturnsZero(string input)
    {
        var result = _converter.ParseDuration(input);

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void ParseDuration_ContainsMonths_ReturnsError()
    {
        var result = _converter.ParseDuration("1 month");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_ContainsYears_ReturnsError()
    {
        var result = _converter.ParseDuration("1 year");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_Garbage_ReturnsError()
    {
        var result = _converter.ParseDuration("not a duration");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_ExceedsMaxInputLength_ReturnsError()
    {
        var tooLong = new string('1', 1001) + " seconds";

        var result = _converter.ParseDuration(tooLong);

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void FormatDuration_SimpleValue_ReturnsExpectedString()
    {
        var result = _converter.FormatDuration(TimeSpan.FromMinutes(150));

        Assert.That(result, Is.EqualTo("2 hours 30 minutes"));
    }

    [Test]
    public void FormatDuration_Zero_ReturnsEmptyString()
    {
        var result = _converter.FormatDuration(TimeSpan.Zero);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void FormatDuration_EntirelySubMillisecond_ReturnsEmptyString()
    {
        var result = _converter.FormatDuration(TimeSpan.FromTicks(50)); // 5 microseconds

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void FormatDuration_Negative_ReturnsLeadingSign()
    {
        var result = _converter.FormatDuration(TimeSpan.FromHours(-2));

        Assert.That(result, Is.EqualTo("-2 hours"));
    }

    [Test]
    public void FormatDuration_IgnoresSubMillisecondRemainder()
    {
        var result = _converter.FormatDuration(TimeSpan.FromMilliseconds(500) + TimeSpan.FromTicks(50));

        Assert.That(result, Is.EqualTo("500 milliseconds"));
    }

    [Test]
    public void ToFutureTime_WithExplicitAnchor_AddsFixedDuration()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = _converter.ToFutureTime("2 hours", anchor);

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
        Assert.That(value, Is.EqualTo(anchor.AddHours(2)));
    }

    [Test]
    public void ToFutureTime_NegativeDuration_SubtractsFromAnchor()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = _converter.ToFutureTime("-2 hours", anchor);

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
        Assert.That(value, Is.EqualTo(anchor.AddHours(-2)));
    }

    [Test]
    public void ToFutureTime_NaiveMode_MonthEndRollover_ClampsLikeAddMonths()
    {
        var anchor = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var result = _converter.ToFutureTime("1 month", anchor);

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
        Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void ToFutureTime_NaiveMode_LeapYearMonthEndRollover_UsesFeb29()
    {
        var anchor = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var result = _converter.ToFutureTime("1 month", anchor);

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
        Assert.That(value, Is.EqualTo(new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void ToFutureTime_NaiveMode_KeepsOriginalOffsetAcrossDstTransition()
    {
        // US Eastern springs forward on 2026-03-08 at 2am local (EST -05:00 -> EDT -04:00).
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        var result = _converter.ToFutureTime("1 month", anchor); // no timeZone -> Mode 1, naive

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;

        // Naive mode keeps the -05:00 offset verbatim, which is WRONG for April 8th in
        // New York (should be -04:00) - this is the documented Mode 1 imprecision.
        Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-5))));
        Assert.That(value.Offset, Is.Not.EqualTo(newYork.GetUtcOffset(Instant.FromDateTimeOffset(value)).ToTimeSpan()));
    }

    [Test]
    public void ToFutureTime_PreciseMode_ResolvesCorrectOffsetAcrossDstTransition()
    {
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        var result = _converter.ToFutureTime("1 month", anchor, newYork); // Mode 2, precise

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;

        // April 8th in New York is EDT (-04:00), not EST (-05:00).
        Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-4))));
    }

    [Test]
    public void ToFutureTime_FixedUnitDuration_SameResultRegardlessOfMode()
    {
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        var naive = ((ParseResult<DateTimeOffset>.Success)_converter.ToFutureTime("90 minutes", anchor)).Value;
        var precise = ((ParseResult<DateTimeOffset>.Success)_converter.ToFutureTime("90 minutes", anchor, newYork)).Value;

        Assert.That(naive.ToUniversalTime(), Is.EqualTo(precise.ToUniversalTime()));
    }

    [Test]
    public void ToFutureTime_NullAnchor_UsesInjectedClock()
    {
        var fakeClock = new FakeClock(Instant.FromUtc(2026, 6, 1, 12, 0, 0));
        var converter = new HumanDurationConverter(fakeClock);

        var result = converter.ToFutureTime("2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
        var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
        Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void ToFutureTime_InvalidDuration_ReturnsError()
    {
        var result = _converter.ToFutureTime("not a duration");

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Error>());
    }

    [Test]
    public void ToFutureTime_ExceedsMaxInputLength_ReturnsError()
    {
        var tooLong = new string('1', 1001) + " seconds";

        var result = _converter.ToFutureTime(tooLong);

        Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Error>());
    }

    [Test]
    public void ToNaturalDuration_ExplicitAnchorInFuture_FormatsPositiveDuration()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var target = anchor.AddHours(2);

        var result = _converter.ToNaturalDuration(target, anchor);

        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("2 hours"));
    }

    [Test]
    public void ToNaturalDuration_TargetBeforeAnchor_FormatsNegativeDuration()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);
        var target = anchor.AddHours(-2);

        var result = _converter.ToNaturalDuration(target, anchor);

        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("-2 hours"));
    }

    [Test]
    public void ToNaturalDuration_SameInstantAsAnchor_ReturnsEmptyString()
    {
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = _converter.ToNaturalDuration(anchor, anchor);

        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo(""));
    }

    [Test]
    public void ToNaturalDuration_PreciseMode_CalendarUnitsAcrossDst()
    {
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));
        var target = new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-4));

        var result = _converter.ToNaturalDuration(target, anchor, newYork);

        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("1 month"));
    }

    [Test]
    public void ToNaturalDuration_NullAnchor_UsesInjectedClock()
    {
        var fakeClock = new FakeClock(Instant.FromUtc(2026, 6, 1, 12, 0, 0));
        var converter = new HumanDurationConverter(fakeClock);
        var target = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero);

        var result = converter.ToNaturalDuration(target);

        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("2 hours"));
    }
}
