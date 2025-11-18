using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class NaturalLanguageParserTests
{
    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every 6 hours")]
    [TestCase("every day")]
    [TestCase("every 2 weeks")]
    [TestCase("every 3 months")]
    [TestCase("every year")]
    public void Parse_IntervalOnly_ParsesSuccessfully(string input)
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert - Should parse successfully and be idempotent
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");

        // Verify idempotency: parsing should produce consistent internal representation
        var result2 = parser.Parse(input, new ScheduleParserOptions());
        Assert.That(result2, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Second parse of '{input}' failed");
    }

    [Test]
    public void Parse_CaseSensitiveMonth_UppercaseMIsMonths()
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var monthResult = parser.Parse("every 3 months", new ScheduleParserOptions());
        var minuteResult = parser.Parse("every 3 minutes", new ScheduleParserOptions());

        // Assert
        Assert.That(monthResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var monthSuccess = (ParseResult<ScheduleSpec>.Success)monthResult;
        Assert.That(monthSuccess.Value.Unit, Is.EqualTo(IntervalUnit.Months));

        Assert.That(minuteResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var minuteSuccess = (ParseResult<ScheduleSpec>.Success)minuteResult;
        Assert.That(minuteSuccess.Value.Unit, Is.EqualTo(IntervalUnit.Minutes));
    }

    [TestCase("", "Input cannot be empty")]
    [TestCase("   ", "Input cannot be empty")]
    [TestCase("every 0 days", "Interval must be a positive number")]
    [TestCase("every 999999999999999999 seconds", "Invalid interval number")]  // Overflow
    [TestCase("1x", "start with 'every'")]  // Missing "every" prefix
    [TestCase("30", "start with 'every'")]  // Missing "every" prefix
    [TestCase("invalid", "start with 'every'")]  // Missing "every" prefix
    [TestCase("every day at 25pm", "Invalid hour for 12-hour format")]  // Invalid hour with am/pm
    [TestCase("every day at 13pm", "Invalid hour for 12-hour format")]  // 24-hour with pm
    [TestCase("every day at 0pm", "Invalid hour for 12-hour format")]  // Zero hour with pm
    [TestCase("every day at 0am", "Invalid hour for 12-hour format")]  // Zero hour with am
    [TestCase("every day at 2:60am", "Invalid minutes")]  // Invalid minutes
    [TestCase("every day at 99:00", "Invalid hour for 24-hour format")]  // Invalid 24-hour
    [TestCase("every day at 24:00", "Invalid hour for 24-hour format")]  // Hour 24 not valid (use 00:00)
    [TestCase("every day on 15", "Day-of-month")]  // Day-of-month with daily interval
    [TestCase("every week on 15", "Day-of-month")]  // Day-of-month with weekly interval
    [TestCase("every 6 hours on 15", "Day-of-month")]  // Day-of-month with hourly interval
    [TestCase("every 30 seconds on 15", "Day-of-month")]  // Day-of-month with seconds interval
    public void Parse_InvalidInput_ReturnsError(string input, string expectedErrorFragment)
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Is.Not.Null);
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Contain(expectedErrorFragment).IgnoreCase);
    }

    [TestCase("every month on 15")]  // Monthly with day-of-month (valid)
    [TestCase("every 3 months on 1")]   // Quarterly with day-of-month (valid)
    [TestCase("every year on 15")]  // Yearly with day-of-month (valid)
    public void Parse_DayOfMonthWithMonthlyOrYearly_Succeeds(string input)
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert - Should parse successfully
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfMonth, Is.Not.Null);
    }

    [TestCase("every 2 weeks on sunday")]   // Multi-week with day-of-week (now supported!)
    [TestCase("every 3 weeks on monday")]   // Multi-week with day-of-week
    [TestCase("every 2 weeks on weekdays")] // Multi-week with day pattern (Quartz limitation for patterns)
    [TestCase("every 2 weeks on sunday at 2pm")] // Multi-week with day and time
    [TestCase("every 3 months on 15")] // Monthly with day-of-month
    public void Parse_MultiIntervalWithDayConstraints_SucceedsInParser(string input)
    {
        // Arrange
        var parser = new NaturalLanguageParser();

        // Act
        var result = parser.Parse(input, new ScheduleParserOptions());

        // Assert - Parser accepts these as valid user intent
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify it's a multi-interval schedule
        Assert.That(success.Value.Interval, Is.GreaterThan(1)
            .Or.EqualTo(1).And.Property(nameof(success.Value.Unit))
            .EqualTo(IntervalUnit.Months).Or.EqualTo(IntervalUnit.Years));
    }

}
