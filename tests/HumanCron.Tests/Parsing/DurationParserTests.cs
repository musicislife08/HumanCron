using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class DurationParserTests
{
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsZeroPeriod(string input)
    {
        var result = DurationParser.Parse(input);

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period, Is.EqualTo(Period.Zero));
    }

    [Test]
    public void Parse_SingleFullWordUnit_ReturnsExpectedPeriod()
    {
        var result = DurationParser.Parse("2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(2));
    }

    [Test]
    public void Parse_SingleAbbreviation_ReturnsExpectedPeriod()
    {
        var result = DurationParser.Parse("30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_MillisecondsAbbreviation_DoesNotCollideWithMinutes()
    {
        var result = DurationParser.Parse("500ms");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Milliseconds, Is.EqualTo(500));
        Assert.That(period.Minutes, Is.EqualTo(0));
    }

    [Test]
    public void Parse_MonthsAbbreviationUppercaseM_DiffersFromMinutes()
    {
        var result = DurationParser.Parse("3M");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Months, Is.EqualTo(3));
        Assert.That(period.Minutes, Is.EqualTo(0));
    }

    [Test]
    public void Parse_CompoundFullWords_ReturnsAllComponents()
    {
        var result = DurationParser.Parse("1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(1));
        Assert.That(period.Hours, Is.EqualTo(2));
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_CompoundAbbreviations_ReturnsAllComponents()
    {
        var result = DurationParser.Parse("1d 2h 30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(1));
        Assert.That(period.Hours, Is.EqualTo(2));
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_SquashedCompactForm_IsRejected()
    {
        var result = DurationParser.Parse("1d2h30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [TestCase("-2 hours")]
    [TestCase("minus 2 hours")]
    [TestCase("negative 2 hours")]
    public void Parse_SignSynonyms_AllProduceNegativePeriod(string input)
    {
        var result = DurationParser.Parse(input);

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(-2));
    }

    [Test]
    public void Parse_NegativeCompound_NegatesEveryComponent()
    {
        var result = DurationParser.Parse("-1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(-1));
        Assert.That(period.Hours, Is.EqualTo(-2));
        Assert.That(period.Minutes, Is.EqualTo(-30));
    }

    [Test]
    public void Parse_MonthsAndYears_AreAcceptedAtThisLayer()
    {
        // DurationParser itself is unit-agnostic; the month/year restriction
        // is enforced by HumanDurationConverter.ParseDuration (Task 2), not here.
        var result = DurationParser.Parse("1 year 2 months");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Years, Is.EqualTo(1));
        Assert.That(period.Months, Is.EqualTo(2));
    }

    [Test]
    public void Parse_DuplicateUnitInCompound_SumsValues()
    {
        var result = DurationParser.Parse("1 hour 2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(3));
    }

    [Test]
    public void Parse_UnknownUnit_ReturnsError()
    {
        var result = DurationParser.Parse("2 fortnights");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_Garbage_ReturnsError()
    {
        var result = DurationParser.Parse("not a duration");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_SignWithNoMagnitude_ReturnsError()
    {
        var result = DurationParser.Parse("-");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_ValueExceedingIntRangeForDateUnit_ReturnsError()
    {
        var result = DurationParser.Parse("99999999999 days");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_LargeValueForTimeUnit_Succeeds()
    {
        // Hours/Minutes/Seconds/Milliseconds are long-typed in Period, so large
        // values (that would overflow the int-typed date units) are fine here.
        var result = DurationParser.Parse("99999999999 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(99999999999L));
    }

    [Test]
    public void Parse_RepeatedDateUnitSumOverflowsInt_ReturnsError()
    {
        // Each token individually passes the int.MaxValue check, but the sum overflows int.
        var result = DurationParser.Parse("2000000000 days 2000000000 days");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_RepeatedTimeUnitSumOverflowsLong_ReturnsError()
    {
        // Each token individually is a valid long, but the sum overflows long.
        var result = DurationParser.Parse("9223372036854775000 seconds 9223372036854775000 seconds");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }
}
