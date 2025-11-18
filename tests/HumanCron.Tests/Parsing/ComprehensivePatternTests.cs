using HumanCron.Models.Internal;
using HumanCron.Models;
using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

/// <summary>
/// Comprehensive tests covering all examples from DSL-SPECIFICATION.md
/// Ensures complete coverage of supported patterns
/// </summary>
[TestFixture]
public class ComprehensivePatternTests
{
    [Test]
    public void Parse_MultiWeekInterval_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 2 weeks";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(2));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Weeks));
    }

    [Test]
    public void Parse_MultiWeekWithTime_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 2 weeks at 9am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(2));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Weeks));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_MultiWeekOnSundayAt1pm_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 2 weeks on sunday at 1pm";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(2));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Weeks));
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(13));
    }

    [Test]
    public void Parse_QuarterlyInterval_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 3 months";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(3));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Months));
    }

    [Test]
    public void Parse_QuarterlyWithTime_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 3 months at 9am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(3));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_QuarterlyOn20thAt9am_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 3 months on 20 at 9am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(3));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Months));
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(20));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_AnnualInterval_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every year";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(1));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Years));
    }

    [Test]
    public void Parse_HourlyOnMonday_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every hour on monday";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(1));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Hours));
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
    }

    [Test]
    public void Parse_30MinutesWithStartTime_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 30 minutes at 9am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(30));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Minutes));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_6HoursStartingAt2pm_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 6 hours at 2pm";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(6));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Hours));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(14));
    }

    [Test]
    public void Parse_WeeklyAt3_30am_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every week at 3:30am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(3));
        Assert.That(success.Value.TimeOfDay.Value.Minute, Is.EqualTo(30));
    }

    [Test]
    public void Parse_DailyOnWeekdaysAt9am_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every weekday at 9am";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_WeeklyOnTuesday_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every tuesday";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Tuesday));
    }

    [Test]
    public void Parse_MultiWeekOnSunday_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every 2 weeks on sunday";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(2));
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
    }

    [Test]
    public void Parse_MonthlyOn1st_DefaultsDayOfMonth()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every month on 1";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(1));
    }

    [Test]
    public void Parse_MonthlyOn15_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every month on 15";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(15));
    }

    [Test]
    public void Parse_MonthlyOn31_ParsesCorrectly()
    {
        // Arrange
        var parser = new NaturalLanguageParser();
        var input = "every month on 31";

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert - Should parse successfully even though Feb will skip
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(31));
    }

    [TestCase("every 2 weeks")]
    [TestCase("every 3 weeks")]
    [TestCase("every 4 weeks")]
    [TestCase("every 6 months")]
    [TestCase("every 12 months")]
    [TestCase("every 2 years")]
    public void Parse_CalendarIntervalPatterns_ParseCorrectly(string input)
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse calendar interval pattern '{input}'");
    }

}
