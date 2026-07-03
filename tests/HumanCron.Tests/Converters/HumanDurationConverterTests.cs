using HumanCron.Abstractions;
using HumanCron.Converters.Duration;
using HumanCron.Models;

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
}
