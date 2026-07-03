using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class MinIntervalFloorTests
{
    private readonly NaturalLanguageParser _parser = new();

    [Test]
    public void Parse_NoMinInterval_AlwaysSucceedsRegardlessOfSpeed()
    {
        var result = _parser.Parse("every 1 seconds", new ScheduleParserOptions());

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_GapExactlyAtFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 15 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A gap exactly equal to the floor must be allowed (inclusive boundary)");
    }

    [Test]
    public void Parse_GapOneUnitBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 14 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
    }

    [Test]
    public void Parse_GapOneUnitAboveFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 16 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_ViolatingFloor_ReturnsExactErrorMessage()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 5 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message,
            Is.EqualTo("'every 5 minutes' runs more often than the minimum allowed interval of 15 minutes"));
    }

    [Test]
    public void Parse_MinuteListAtFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every hour at minutes 0,15,30,45", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_MinuteListBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every hour at minutes 0,10,20,30,40,50", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
    }

    [Test]
    public void Parse_RangeStepBurstBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 5 minutes between 0 and 30 of each hour", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>(),
            "Bursts every 5 minutes within the window must be rejected even though the overall pattern is not a plain 5-minute interval");
    }

    [Test]
    public void Parse_WeekdayPatternAtTwentyFourHourFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromHours(24) };

        var result = _parser.Parse("every weekday at 9am", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "At-most-daily patterns must be allowed at exactly a 24-hour floor (inclusive boundary)");
    }

    [Test]
    public void Parse_ZeroMinInterval_AlwaysSucceeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.Zero };

        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A zero floor forbids nothing");
    }

    [Test]
    public void Parse_NegativeMinInterval_AlwaysSucceeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(-5) };

        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A negative floor is inert - gap < negative is never true - not validated against");
    }

    [TestCase(30, "30 seconds")]
    [TestCase(60, "1 minute")]
    [TestCase(6 * 3600, "6 hours")]
    [TestCase(2 * 24 * 3600, "2 days")]
    public void Parse_ErrorMessage_FormatsFloorInLargestWholeUnit(int floorSeconds, string expectedFloorPhrase)
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromSeconds(floorSeconds) };

        // "every 1 seconds" is faster than every floor value under test, so this is
        // guaranteed to fail and exercise FormatFloor's seconds/minutes/hours/days branches.
        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Does.Contain(expectedFloorPhrase));
    }
}
