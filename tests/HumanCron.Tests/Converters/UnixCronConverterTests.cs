using HumanCron.Abstractions;
using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Converters;

/// <summary>
/// Tests for UnixCronConverter - the PRIMARY public API for natural language ↔ Unix cron conversion
/// </summary>
[TestFixture]
public class UnixCronConverterTests
{
    private IHumanCronConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        // Use real implementation, not mocks - this is an integration test
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        // Server timezone = UTC for deterministic tests (no DST complexity)
        _converter = new UnixCronConverter(parser, formatter, fakeClock, DateTimeZone.Utc);
    }

    // ========================================
    // ToCron() - Natural Language → Unix Cron
    // ========================================

    #region Simple Intervals

    [TestCase("every minute", "* * * * *")]  // Every minute
    [TestCase("every 15 minutes", "*/15 * * * *")]
    [TestCase("every hour", "0 * * * *")]  // Every hour at :00
    [TestCase("every 6 hours", "0 */6 * * *")]
    [TestCase("every day", "0 0 * * *")]  // Daily at midnight
    public void ToCron_SimpleIntervals_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    [Test]
    public void ToCron_SecondsInterval_ReturnsError()
    {
        // Unix cron doesn't support seconds - smallest unit is minutes
        var result = _converter.ToCron("every 30 seconds");

        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Unix cron should not support seconds interval");
    }

    [Test]
    public void ToCron_WeeklyInterval_DefaultsToCurrentDay()
    {
        // Act
        var result = _converter.ToCron("every week");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;

        // Should default to current day of week (UTC) - FakeClock set to Jan 15, 2025 = Wednesday (3)
        var expectedDay = "3"; // Wednesday
        var expectedCron = $"0 0 * * {expectedDay}";

        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Weekly interval without day should default to current day (Wednesday)");
    }

    [Test]
    public void ToCron_WeeklyInterval_NearMidnight_UsesServerTimezone()
    {
        // Arrange - Time after midnight UTC where day differs from MST/PST
        // 01:00 UTC Thursday (Jan 16) = 18:00 MST Wednesday (Jan 15)
        // This test verifies the bug fix: builder must use _localTimeZone, not GetSystemDefault()
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 16, 1, 0)); // 01:00 UTC Thursday Jan 16
        var converter = new UnixCronConverter(parser, formatter, fakeClock, DateTimeZone.Utc);

        // Act - "every week" without explicit day should default to server timezone's current day
        var result = converter.ToCron("every week");

        // Assert - Should use server timezone (UTC) = Thursday (4), NOT system timezone = Wednesday (3)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;

        var expectedDay = "4"; // Thursday (server timezone = UTC)
        var expectedCron = $"0 0 * * {expectedDay}";

        Assert.That(success.Value, Is.EqualTo(expectedCron),
            "Should use server timezone (UTC Thursday), not system timezone (MST Wednesday)");
    }

    #endregion

    #region Time-Based Schedules

    [TestCase("every day at 2pm", "0 14 * * *")]
    [TestCase("every day at 9am", "0 9 * * *")]
    [TestCase("every day at 12am", "0 0 * * *")]  // Midnight
    [TestCase("every day at 12pm", "0 12 * * *")]  // Noon
    [TestCase("every day at 9:30am", "30 9 * * *")]
    [TestCase("every day at 14:30", "30 14 * * *")]  // 24-hour format
    [TestCase("every day at 23:59", "59 23 * * *")]
    public void ToCron_DailyWithTime_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron));
    }

    #endregion

    #region Day-of-Week Schedules

    [TestCase("every monday", "0 0 * * 1")]
    [TestCase("every tuesday", "0 0 * * 2")]
    [TestCase("every wednesday", "0 0 * * 3")]
    [TestCase("every thursday", "0 0 * * 4")]
    [TestCase("every friday", "0 0 * * 5")]
    [TestCase("every saturday", "0 0 * * 6")]
    [TestCase("every sunday", "0 0 * * 0")]
    [TestCase("every monday at 9am", "0 9 * * 1")]
    [TestCase("every monday", "0 0 * * 1")]  // Daily on specific day = weekly
    public void ToCron_WeeklySchedules_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron));
    }

    #endregion

    #region Day Pattern Schedules

    [TestCase("every weekday", "0 0 * * 1-5")]
    [TestCase("every weekday at 9am", "0 9 * * 1-5")]
    [TestCase("every weekday at 2pm", "0 14 * * 1-5")]
    [TestCase("every weekend", "0 0 * * 0,6")]
    [TestCase("every weekend at 10am", "0 10 * * 0,6")]
    public void ToCron_DayPatterns_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron));
    }

    #endregion

    #region Month Pattern Schedules

    /// <summary>
    /// Tests for month-based schedules: single months, month ranges, month lists
    /// </summary>
    [TestCase("every day in january", "0 0 * 1 *")]
    [TestCase("every day in december", "0 0 * 12 *")]
    [TestCase("every day in june at 2pm", "0 14 * 6 *")]
    [TestCase("every day in february", "0 0 * 2 *")]
    [TestCase("every day in march", "0 0 * 3 *")]
    [TestCase("every day in april", "0 0 * 4 *")]
    [TestCase("every day in may", "0 0 * 5 *")]
    [TestCase("every day in july", "0 0 * 7 *")]
    [TestCase("every day in august", "0 0 * 8 *")]
    [TestCase("every day in september", "0 0 * 9 *")]
    [TestCase("every day in october", "0 0 * 10 *")]
    [TestCase("every day in november", "0 0 * 11 *")]
    public void ToCron_SingleMonth_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    [TestCase("every day between january and march", "0 0 * 1-3 *")]
    [TestCase("every day between june and august", "0 0 * 6-8 *")]
    [TestCase("every day between september and december at 9am", "0 9 * 9-12 *")]
    public void ToCron_MonthRange_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    [TestCase("every day in january,april,july,october", "0 0 * 1,4,7,10 *")]
    [TestCase("every day in january,july", "0 0 * 1,7 *")]
    [TestCase("every day in march,june,september,december at 2pm", "0 14 * 3,6,9,12 *")]
    public void ToCron_MonthList_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    [TestCase("every weekday in january", "0 0 * 1 1-5")]
    [TestCase("every weekday in january at 9am", "0 9 * 1 1-5")]
    [TestCase("every monday in june", "0 0 * 6 1")]
    [TestCase("every monday in june at 3pm", "0 15 * 6 1")]
    [TestCase("every weekend in december", "0 0 * 12 0,6")]
    public void ToCron_MonthWithDayOfWeek_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    #endregion

    #region Day-of-Month Schedules

    /// <summary>
    /// Tests for day-of-month schedules: specific days, day boundaries, day with months
    /// </summary>
    [TestCase("every month on 1", "0 0 1 * *")]
    [TestCase("every month on 15", "0 0 15 * *")]
    [TestCase("every month on 31", "0 0 31 * *")]
    [TestCase("every month on 1 at 9am", "0 9 1 * *")]
    [TestCase("every month on 15 at 2pm", "0 14 15 * *")]
    [TestCase("every month on 28", "0 0 28 * *")]
    [TestCase("every month on 29", "0 0 29 * *")]
    [TestCase("every month on 30", "0 0 30 * *")]
    [TestCase("every month on 31 at 11pm", "0 23 31 * *")]
    public void ToCron_DayOfMonth_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    [TestCase("every month on 1 in january", "0 0 1 1 *")]
    [TestCase("every month on 15 in january,april,july,october", "0 0 15 1,4,7,10 *")]
    [TestCase("every month on 1 in january,july at 9am", "0 9 1 1,7 *")]
    public void ToCron_DayOfMonthWithMonthConstraint_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    #endregion

    #region Special Character Combinations

    /// <summary>
    /// Tests for complex combinations of ranges, times, and constraints
    /// </summary>
    [TestCase("every weekday at 2:30pm", "30 14 * * 1-5")]
    [TestCase("every weekday at 9:15am", "15 9 * * 1-5")]
    [TestCase("every hour at 12:15am", "15 * * * *")]
    [TestCase("every hour at 12:30am", "30 * * * *")]
    [TestCase("every hour at 12:45am", "45 * * * *")]
    [TestCase("every weekday in june at 9am", "0 9 * 6 1-5")]
    [TestCase("every monday in january,april,july,october at 2pm", "0 14 * 1,4,7,10 1")]
    [TestCase("every month on 15 in june at 3pm", "0 15 15 6 *")]
    public void ToCron_SpecialCharacterCombinations_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    #endregion

    #region Field Boundary Values

    /// <summary>
    /// Tests for boundary values in each cron field: minutes (0-59), hours (0-23), days (1-31), months (1-12)
    /// </summary>
    [TestCase("every hour at 12:59am", "59 * * * *")]  // Max minute
    [TestCase("every day at 23:59", "59 23 * * *")]  // Max hour + minute
    [TestCase("every month on 1 at 12am", "0 0 1 * *")]  // Min day
    [TestCase("every month on 31 at 12am", "0 0 31 * *")]  // Max day
    [TestCase("every day in january at 12am", "0 0 * 1 *")]  // Min month (Jan)
    [TestCase("every day in december at 12am", "0 0 * 12 *")]  // Max month (Dec)
    [TestCase("every month on 31 in december at 23:59", "59 23 31 12 *")]  // All max values
    [TestCase("every month on 1 in january at 12am", "0 0 1 1 *")]  // All min values
    [TestCase("every day at 12:00am", "0 0 * * *")]  // Midnight with explicit minutes
    [TestCase("every day at 12:00pm", "0 12 * * *")]  // Noon with explicit minutes
    public void ToCron_FieldBoundaryValues_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to cron");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect cron expression for '{natural}'");
    }

    #endregion

    #region Invalid Input Tests

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ToCron_EmptyInput_ReturnsError(string? invalid)
    {
        // Act
        var result = _converter.ToCron(invalid!);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
    }

    [TestCase("invalid")]
    [TestCase("xyz")]
    [TestCase("1d")]  // Must use verbose syntax like "every day"
    [TestCase("every 999999999999999999 seconds")]  // Overflow
    public void ToCron_InvalidSyntax_ReturnsError(string invalid)
    {
        // Act
        var result = _converter.ToCron(invalid);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message.Length, Is.LessThan(500), "Error messages should be concise");
    }

    [TestCase("every day at 25pm")]  // Invalid hour
    [TestCase("every day at 13pm")]  // 24-hour with am/pm
    [TestCase("every day at 2:60am")]  // Invalid minutes
    [TestCase("every day at 99:00")]  // Invalid 24-hour
    public void ToCron_InvalidTime_ReturnsHelpfulError(string invalid)
    {
        // Act
        var result = _converter.ToCron(invalid);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;

        // Validate error message quality
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Not.Contain("Exception"), "Should be user-friendly");
        Assert.That(error.Message, Does.Not.Contain("StackTrace"), "Should not leak implementation details");
    }

    /// <summary>
    /// Tests for invalid field values: minutes > 59, hours > 23, days > 31 or < 1, etc.
    /// </summary>
    [Test]
    public void ToCron_InvalidFieldValues_ReturnsError()
    {
        // Minute > 59
        var result = _converter.ToCron("every hour at 12:60am");
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Minute value 60 should be rejected (max is 59)");

        // Hour > 23 (24-hour format)
        result = _converter.ToCron("every day at 25:00");
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Hour value 25 should be rejected (max is 23 in 24-hour format)");

        // Hour 24
        result = _converter.ToCron("every day at 24:00");
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Hour value 24 should be rejected (max is 23)");

        // Day > 31
        result = _converter.ToCron("every month on 32");
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Day value 32 should be rejected (max is 31)");

        // Day < 1
        result = _converter.ToCron("every month on 0");
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Day value 0 should be rejected (min is 1)");
    }

    [TestCase("every day at 13pm")]  // Invalid 12-hour format
    [TestCase("every day at 24:00")]  // Hour 24 invalid
    [TestCase("every month on 0")]  // Day 0 invalid
    [TestCase("every month on 32")]  // Day 32 invalid
    [TestCase("every hour at 12:60am")]  // Minute 60 invalid
    [TestCase("every day at 2:99am")]  // Minute 99 invalid
    public void ToCron_InvalidFieldValueVariations_ReturnsError(string invalid)
    {
        // Act
        var result = _converter.ToCron(invalid);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            $"Expected error for invalid input: '{invalid}'");
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
    }

    #endregion

    #region Range Support Tests

    /// <summary>
    /// Tests for minute/hour/day range support in Unix cron expressions
    /// These test round-trip parsing and building of range expressions
    /// </summary>

    // ========================================
    // Hour Range Tests
    // ========================================

    [TestCase("0 9-17 * * *", 9, 17, Description = "Business hours (9am-5pm)")]
    [TestCase("0 0-23 * * *", 0, 23, Description = "Full day (midnight-11pm)")]
    [TestCase("0 14-14 * * *", 14, 14, Description = "Single hour range")]
    [TestCase("0 22-6 * * *", 22, 6, Description = "Night hours (wraps midnight)")]
    [TestCase("0 6-18 * * *", 6, 18, Description = "Day hours (6am-6pm)")]
    public void Parse_HourRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse hour range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.HourStart, Is.EqualTo(expectedStart),
            $"Hour start should be {expectedStart}");
        Assert.That(success.Value.HourEnd, Is.EqualTo(expectedEnd),
            $"Hour end should be {expectedEnd}");
    }

    [TestCase("0 9-17 * * *", Description = "Business hours (9am-5pm)")]
    [TestCase("0 0-23 * * *", Description = "Full day (midnight-11pm)")]
    [TestCase("0 22-6 * * *", Description = "Night hours (wraps midnight)")]
    public void RoundTrip_HourRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new UnixCronParser();
        var builder = UnixCronBuilder.Create();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → cron
        var buildResult = builder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>(),
            "Failed to build cron from parsed spec");
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{cron}'");
    }

    // ========================================
    // Minute Range Tests
    // ========================================

    [TestCase("0-30 * * * *", 0, 30, Description = "First half hour")]
    [TestCase("30-59 * * * *", 30, 59, Description = "Last half hour")]
    [TestCase("15-45 * * * *", 15, 45, Description = "Quarter hours")]
    [TestCase("0-59 * * * *", 0, 59, Description = "Full hour")]
    [TestCase("0-0 * * * *", 0, 0, Description = "Single minute range")]
    public void Parse_MinuteRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse minute range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.MinuteStart, Is.EqualTo(expectedStart),
            $"Minute start should be {expectedStart}");
        Assert.That(success.Value.MinuteEnd, Is.EqualTo(expectedEnd),
            $"Minute end should be {expectedEnd}");
    }

    [TestCase("0-30 * * * *", Description = "First half hour")]
    [TestCase("30-59 * * * *", Description = "Last half hour")]
    [TestCase("15-45 * * * *", Description = "Quarter hours")]
    public void RoundTrip_MinuteRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new UnixCronParser();
        var builder = UnixCronBuilder.Create();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → cron
        var buildResult = builder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>(),
            "Failed to build cron from parsed spec");
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{cron}'");
    }

    // ========================================
    // Day Range Tests
    // ========================================

    [TestCase("0 0 1-15 * *", 1, 15, Description = "First half of month")]
    [TestCase("0 0 16-31 * *", 16, 31, Description = "Second half of month")]
    [TestCase("0 0 1-7 * *", 1, 7, Description = "First week")]
    [TestCase("0 0 15-15 * *", 15, 15, Description = "Single day range")]
    [TestCase("0 0 1-31 * *", 1, 31, Description = "Full month")]
    public void Parse_DayRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse day range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayStart, Is.EqualTo(expectedStart),
            $"Day start should be {expectedStart}");
        Assert.That(success.Value.DayEnd, Is.EqualTo(expectedEnd),
            $"Day end should be {expectedEnd}");
    }

    [TestCase("0 0 1-15 * *", Description = "First half of month")]
    [TestCase("0 0 16-31 * *", Description = "Second half of month")]
    [TestCase("0 0 1-7 * *", Description = "First week")]
    public void RoundTrip_DayRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new UnixCronParser();
        var builder = UnixCronBuilder.Create();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → cron
        var buildResult = builder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>(),
            "Failed to build cron from parsed spec");
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{cron}'");
    }

    // ========================================
    // Combined Range Tests
    // ========================================

    [TestCase("15-45 9-17 * * *", "Minutes 15-45 during business hours")]
    [TestCase("0-30 9-17 1-15 * *", "Complex multi-range (minutes, hours, days)")]
    [TestCase("0-59 0-23 1-31 * *", "Full ranges across all fields")]
    [TestCase("0-30 22-6 * * *", "Minutes 0-30 during night hours")]
    [TestCase("15-45 * 1-15 * *", "Minutes 15-45, first half of month")]
    public void Parse_CombinedRanges_ReturnsCorrectScheduleSpec(string cron, string description)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse combined ranges ({description}): {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify ranges are parsed correctly
        var parts = cron.Split(' ');
        if (parts[0].Contains('-'))
        {
            var minuteParts = parts[0].Split('-');
            Assert.That(success.Value.MinuteStart, Is.EqualTo(int.Parse(minuteParts[0])));
            Assert.That(success.Value.MinuteEnd, Is.EqualTo(int.Parse(minuteParts[1])));
        }
        if (parts[1].Contains('-'))
        {
            var hourParts = parts[1].Split('-');
            Assert.That(success.Value.HourStart, Is.EqualTo(int.Parse(hourParts[0])));
            Assert.That(success.Value.HourEnd, Is.EqualTo(int.Parse(hourParts[1])));
        }
        if (parts[2].Contains('-'))
        {
            var dayParts = parts[2].Split('-');
            Assert.That(success.Value.DayStart, Is.EqualTo(int.Parse(dayParts[0])));
            Assert.That(success.Value.DayEnd, Is.EqualTo(int.Parse(dayParts[1])));
        }
    }

    [TestCase("15-45 9-17 * * *", Description = "Minutes 15-45 during business hours")]
    [TestCase("0-30 9-17 1-15 * *", Description = "Complex multi-range")]
    public void RoundTrip_CombinedRanges_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new UnixCronParser();
        var builder = UnixCronBuilder.Create();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → cron
        var buildResult = builder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>(),
            "Failed to build cron from parsed spec");
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{cron}'");
    }

    // ========================================
    // Edge Case Tests
    // ========================================

    [TestCase("0-0 * * * *", 0, 0, Description = "Min equals max (minute)")]
    [TestCase("0 23-23 * * *", 23, 23, Description = "Min equals max (hour)")]
    [TestCase("0 0 31-31 * *", 31, 31, Description = "Min equals max (day)")]
    public void Parse_SingleValueRange_ReturnsCorrectScheduleSpec(string cron, int expectedValue, int _)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse single-value range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify the range start and end are equal
        if (cron.StartsWith("0-0"))
        {
            Assert.That(success.Value.MinuteStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.MinuteEnd, Is.EqualTo(expectedValue));
        }
        else if (cron.Contains(" 23-23 "))
        {
            Assert.That(success.Value.HourStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.HourEnd, Is.EqualTo(expectedValue));
        }
        else if (cron.Contains(" 31-31 "))
        {
            Assert.That(success.Value.DayStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.DayEnd, Is.EqualTo(expectedValue));
        }
    }

    [TestCase("0 0-23 * * *", Description = "Full hour range (0-23)")]
    [TestCase("0-59 * * * *", Description = "Full minute range (0-59)")]
    [TestCase("0 0 1-31 * *", Description = "Full day range (1-31)")]
    public void Parse_BoundaryRanges_ReturnsCorrectScheduleSpec(string cron)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse boundary range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify boundary values
        if (cron.Contains("0-23"))
        {
            Assert.That(success.Value.HourStart, Is.EqualTo(0), "Hour start should be 0");
            Assert.That(success.Value.HourEnd, Is.EqualTo(23), "Hour end should be 23");
        }
        else if (cron.StartsWith("0-59"))
        {
            Assert.That(success.Value.MinuteStart, Is.EqualTo(0), "Minute start should be 0");
            Assert.That(success.Value.MinuteEnd, Is.EqualTo(59), "Minute end should be 59");
        }
        else if (cron.Contains("1-31"))
        {
            Assert.That(success.Value.DayStart, Is.EqualTo(1), "Day start should be 1");
            Assert.That(success.Value.DayEnd, Is.EqualTo(31), "Day end should be 31");
        }
    }

    [TestCase("0 22-6 * * *", 22, 6, Description = "Hour wraparound (night)")]
    public void Parse_WraparoundRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new UnixCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert - Parser accepts wraparound ranges (validation is runtime concern)
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse wraparound range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.HourStart, Is.EqualTo(expectedStart));
        Assert.That(success.Value.HourEnd, Is.EqualTo(expectedEnd),
            "Wraparound ranges are accepted (22-6 means 10pm to 6am)");
    }

    #endregion

    // ========================================
    // ToNaturalLanguage() - Unix Cron → Natural Language
    // ========================================

    #region Cron to Natural Language

    [TestCase("*/15 * * * *", "every 15 minutes")]
    [TestCase("0 * * * *", "every hour")]
    [TestCase("0 */6 * * *", "every 6 hours")]
    [TestCase("0 0 * * *", "every day at 12am")]  // Formatter includes explicit midnight
    [TestCase("0 14 * * *", "every day at 2pm")]
    [TestCase("0 9 * * 1", "every monday at 9am")]
    [TestCase("0 0 * * 1-5", "every weekday at 12am")]  // Weekdays pattern uses weekly
    [TestCase("0 9 * * 1-5", "every weekday at 9am")]
    public void ToNaturalLanguage_ValidCron_ReturnsNaturalExpression(string cron, string expectedNatural)
    {
        // Act
        var result = _converter.ToNaturalLanguage(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert cron '{cron}' to natural language");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedNatural));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ToNaturalLanguage_EmptyInput_ReturnsError(string? invalid)
    {
        // Act
        var result = _converter.ToNaturalLanguage(invalid!);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
    }

    [TestCase("invalid")]
    [TestCase("* * *")]  // Too few parts (Unix needs 5)
    [TestCase("* * * * * *")]  // Too many parts (6-part is Quartz, not Unix)
    public void ToNaturalLanguage_InvalidCron_ReturnsError(string invalid)
    {
        // Act
        var result = _converter.ToNaturalLanguage(invalid);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
    }

    #endregion

    // ========================================
    // Round-Trip Tests (Critical for API Correctness)
    // ========================================

    #region Round-Trip Validation

    [TestCase("every 15 minutes")]
    [TestCase("every hour")]
    [TestCase("every day")]
    [TestCase("every day at 2pm")]
    [TestCase("every monday")]
    [TestCase("every weekday at 9am")]
    [TestCase("every day in january at 2pm")]
    [TestCase("every monday in june")]
    [TestCase("every weekday in december")]
    [TestCase("every day at 12am")]
    [TestCase("every weekday at 2:30pm")]
    public void RoundTrip_NaturalToCronToNatural_PreservesSemantics(string original)
    {
        // Act - Natural → Cron
        var cronResult = _converter.ToCron(original);
        Assert.That(cronResult, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{original}' to cron");
        var cron = ((ParseResult<string>.Success)cronResult).Value;

        // Act - Cron → Natural
        var naturalResult = _converter.ToNaturalLanguage(cron);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert cron '{cron}' back to natural language");
        var natural = ((ParseResult<string>.Success)naturalResult).Value;

        // Assert - May not be identical strings, but should be semantically equivalent
        // Verify by converting back to cron - should produce same result
        var verifyResult = _converter.ToCron(natural);
        Assert.That(verifyResult, Is.TypeOf<ParseResult<string>.Success>(),
            $"Round-trip produced invalid natural language: '{natural}'");
        Assert.That(((ParseResult<string>.Success)verifyResult).Value, Is.EqualTo(cron),
            $"Round-trip not semantically equivalent: '{original}' → '{cron}' → '{natural}' → different cron");
    }

    [TestCase("*/15 * * * *")]
    [TestCase("0 14 * * *")]
    [TestCase("0 9 * * 1")]
    [TestCase("0 9 * * 1-5")]
    public void RoundTrip_CronToNaturalToCron_PreservesCron(string originalCron)
    {
        // Act - Cron → Natural
        var naturalResult = _converter.ToNaturalLanguage(originalCron);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert cron '{originalCron}' to natural language");
        var natural = ((ParseResult<string>.Success)naturalResult).Value;

        // Act - Natural → Cron
        var cronResult = _converter.ToCron(natural);
        Assert.That(cronResult, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' back to cron");
        var cron = ((ParseResult<string>.Success)cronResult).Value;

        // Assert - Should produce identical cron expression
        Assert.That(cron, Is.EqualTo(originalCron),
            $"Round-trip changed cron: '{originalCron}' → '{natural}' → '{cron}'");
    }

    #endregion

    // ========================================
    // Error Message Quality Tests
    // ========================================

    #region Error Message Quality

    [Test]
    public void ToCron_AllErrors_HaveConsistentFormat()
    {
        // Arrange - Test various error scenarios
        var invalidInputs = new[]
        {
            "",
            "invalid",
            "every day at 99:00",
            "every 999999999999 seconds"
        };

        // Act & Assert
        foreach (var input in invalidInputs)
        {
            var result = _converter.ToCron(input);
            Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
                $"Expected error for input: '{input}'");

            var error = (ParseResult<string>.Error)result;

            // Error message quality checks
            Assert.That(error.Message, Is.Not.Empty, "Error message should not be empty");
            Assert.That(error.Message.Length, Is.LessThan(500), "Error messages should be concise");
            Assert.That(error.Message, Does.Not.Contain("Exception"), "Should be user-friendly");
            Assert.That(error.Message, Does.Not.Contain("StackTrace"), "Should not leak implementation");
            Assert.That(char.IsUpper(error.Message[0]), Is.True, "Should start with capital letter");
        }
    }

    [Test]
    public void ToNaturalLanguage_AllErrors_HaveConsistentFormat()
    {
        // Arrange - Test various error scenarios
        var invalidCrons = new[]
        {
            "",
            "invalid",
            "* * *"
        };

        // Act & Assert
        foreach (var cron in invalidCrons)
        {
            var result = _converter.ToNaturalLanguage(cron);
            Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
                $"Expected error for cron: '{cron}'");

            var error = (ParseResult<string>.Error)result;

            // Error message quality checks
            Assert.That(error.Message, Is.Not.Empty);
            Assert.That(error.Message.Length, Is.LessThan(500));
            Assert.That(char.IsUpper(error.Message[0]), Is.True);
        }
    }

    #endregion

    // ========================================
    // Timezone Conversion Tests
    // ========================================

    #region Timezone Conversion

    [Test]
    public void ToCron_SameTimezone_NoConversion()
    {
        // Arrange - Schedule in UTC (same as server timezone)
        var natural = "every day at 2pm";

        // Act
        var result = _converter.ToCron(natural, DateTimeZone.Utc);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 14 * * *"),
            "Should output 14:00 (2pm) when user timezone matches server timezone (both UTC)");
    }

    [Test]
    public void ToCron_DefaultTimezone_UsesLocal()
    {
        // Arrange - No timezone specified, should default to Local
        var natural = "every day at 2pm";

        // Act - Use overload without timezone parameter
        var result = _converter.ToCron(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 14 * * *"),
            "Should default to Local timezone when no timezone specified");
    }

    [Test]
    public void ToCron_CrossTimezone_ConvertsCorrectly()
    {
        // Arrange - User in Pacific timezone, server in UTC
        // 2pm Pacific (UTC-8 in winter) = 22:00 UTC (2pm + 8 hours)
        var natural = "every day at 2pm";
        var pacificZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];

        // Expected: 2pm Pacific = 22:00 UTC (server timezone)
        var expectedHour = 22;

        // Act
        var result = _converter.ToCron(natural, pacificZone);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            () => result is ParseResult<string>.Error err ? err.Message : "");
        var success = (ParseResult<string>.Success)result;

        // Parse the hour from the cron expression (format: "0 HH * * *")
        var parts = success.Value.Split(' ');
        var actualHour = int.Parse(parts[1]);

        Assert.That(actualHour, Is.EqualTo(expectedHour),
            "2pm Pacific (UTC-8) should convert to 22:00 UTC (server timezone)");
    }

    [Test]
    public void ToCron_DifferentTimezone_CalculatesOffset()
    {
        // Arrange - Test with Eastern timezone (UTC-5 in winter, no DST)
        var natural = "every day at 2pm";
        var easternZone = DateTimeZoneProviders.Tzdb["America/New_York"];

        // Server timezone is UTC
        // Eastern is UTC-5 in winter (Jan 15, 2025)
        // 2pm Eastern = 14:00 + 5 hours = 19:00 UTC
        var expectedHour = 19;

        // Act
        var result = _converter.ToCron(natural, easternZone);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            () => result is ParseResult<string>.Error err ? err.Message : "");
        var success = (ParseResult<string>.Success)result;

        var parts = success.Value.Split(' ');
        var actualHour = int.Parse(parts[1]);

        Assert.That(actualHour, Is.EqualTo(expectedHour),
            "2pm Eastern (UTC-5) should convert to 19:00 UTC (server timezone)");
    }

    [Test]
    public void ToCron_TimezoneConversion_WorksWithMinutes()
    {
        // Arrange - Test time with minutes: 2:30pm Pacific (UTC-8) = 22:30 UTC
        var natural = "every day at 14:30";
        var pacificZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];

        // Act
        var result = _converter.ToCron(natural, pacificZone);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("30 22 * * *"),
            "Minutes (:30) should be preserved during timezone conversion (2:30pm Pacific = 22:30 UTC)");
    }

    [Test]
    public void ToCron_Hourly_NoTimezoneConversion()
    {
        // Arrange - Hourly intervals don't have timezone implications
        var natural = "every 6 hours";

        // Act
        var result = _converter.ToCron(natural, DateTimeZone.Utc);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 */6 * * *"),
            "Hourly intervals without specific time shouldn't be affected by timezone");
    }

    #endregion
}
