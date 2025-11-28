using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.NCrontab.Abstractions;
using HumanCron.NCrontab.Converters;
using HumanCron.Parsing;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Converters;

/// <summary>
/// Tests for NCrontabConverter - natural language ↔ NCrontab 6-field cron conversion
/// </summary>
[TestFixture]
public class NCrontabConverterTests
{
    private INCrontabConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        // Server timezone = UTC for deterministic tests
        _converter = new NCrontabConverter(parser, formatter, fakeClock, DateTimeZone.Utc);
    }

    // ========================================
    // ToNCrontab() - Natural Language → NCrontab
    // These tests focus on NCrontab-specific features (seconds + 6-field format)
    // Base parsing is already tested in NaturalLanguageParserTests
    // ========================================

    #region Seconds Support (NCrontab-specific - not supported in Unix cron)

    [TestCase("every second", "* * * * * *")]
    [TestCase("every 5 seconds", "*/5 * * * * *")]
    [TestCase("every 30 seconds", "*/30 * * * * *")]
    public void ToNCrontab_SecondsInterval_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToNCrontab(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to convert '{natural}' to NCrontab");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron),
            $"Incorrect NCrontab expression for '{natural}'");
    }

    #endregion

    #region 6-Field Format Verification (seconds field prepended)

    [TestCase("every 15 minutes", "0 */15 * * * *")]  // 0 seconds prepended
    [TestCase("every hour", "0 0 * * * *")]  // 0 seconds, 0 minutes prepended
    [TestCase("every day at 2pm", "0 0 14 * * *")]  // 0 seconds, specific time
    [TestCase("every weekday at 9am", "0 0 9 * * 1-5")]  // 0 seconds, with day-of-week
    public void ToNCrontab_NonSecondsIntervals_PrependsZeroSeconds(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToNCrontab(natural);

        // Assert - Verify 6-field format with seconds field
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron));
        Assert.That(success.Value.Split(' '), Has.Length.EqualTo(6),
            "NCrontab expressions must have exactly 6 fields");
    }

    #endregion

    #region Error Cases

    [Test]
    public void ToNCrontab_EmptyInput_ReturnsError()
    {
        var result = _converter.ToNCrontab("");

        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("cannot be empty"));
    }

    #endregion

    #region Timezone Support

    [Test]
    public void ToNCrontab_WithUserTimezone_ConvertsCorrectly()
    {
        // Arrange - User in Pacific timezone (UTC-8 in winter)
        var pacificTimezone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];

        // Act - "every day at 2pm" in Pacific time
        var result = _converter.ToNCrontab("every day at 2pm", pacificTimezone);

        // Assert - Should convert to UTC (2pm Pacific = 22:00 UTC in winter)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 0 22 * * *"),
            "2pm Pacific should convert to 22:00 UTC");
    }

    #endregion

    #region Validation - Unsupported Patterns

    [Test]
    public void ToNCrontab_MultiWeekInterval_ReturnsError()
    {
        // Act
        var result = _converter.ToNCrontab("every 2 weeks");

        // Assert - Multi-week patterns are not supported in NCrontab (6-field format)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("multi-week").Or.Contain("2 weeks").Or.Contain("CalendarInterval"));
    }

    [Test]
    public void ToNCrontab_MultiMonthInterval_ReturnsError()
    {
        // Act
        var result = _converter.ToNCrontab("every 2 months");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
    }

    [Test]
    public void ToNCrontab_MultiYearInterval_ReturnsError()
    {
        // Act
        var result = _converter.ToNCrontab("every 2 years");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
    }

    #endregion

    #region Day-of-Week Lists and Ranges

    [TestCase("every monday,wednesday,friday at 9am", "0 0 9 * * 1,3,5")]
    [TestCase("every tuesday-thursday at 9am", "0 0 9 * * 2,3,4")]  // Range expanded to list
    [TestCase("every monday,wednesday,friday", "0 0 0 * * 1,3,5")]
    public void ToNCrontab_DayOfWeekListsAndRanges_ReturnsCorrectExpression(string natural, string expectedCron)
    {
        // Act
        var result = _converter.ToNCrontab(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedCron));
    }

    #endregion

    // ========================================
    // ToNaturalLanguage() - NCrontab → Natural Language
    // Tests the NCrontabParser.Parse() method indirectly
    // ========================================

    #region NCrontab to Natural Language

    [TestCase("*/30 * * * * *", "every 30 seconds")]
    [TestCase("0 */15 * * * *", "every 15 minutes")]
    [TestCase("0 0 */6 * * *", "every 6 hours")]
    [TestCase("0 0 14 * * *", "every day at 2pm")]
    [TestCase("0 0 9 * * 1", "every monday at 9am")]
    [TestCase("0 0 9 * * 1-5", "every weekday at 9am")]
    [TestCase("0 0 9 * 1 *", "every day at 9am in january")]
    public void ToNaturalLanguage_ValidNCrontab_ReturnsNaturalLanguage(string ncrontab, string expectedNatural)
    {
        // Act
        var result = _converter.ToNaturalLanguage(ncrontab);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedNatural));
    }

    [Test]
    public void ToNaturalLanguage_EmptyInput_ReturnsError()
    {
        var result = _converter.ToNaturalLanguage("");

        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("cannot be empty"));
    }

    [Test]
    public void ToNaturalLanguage_InvalidNCrontab_ReturnsError()
    {
        var result = _converter.ToNaturalLanguage("invalid cron");

        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
    }

    [Test]
    public void ToNaturalLanguage_Invalid5FieldCron_ReturnsError()
    {
        // Act - Try to parse 5-field Unix cron (missing seconds field)
        var result = _converter.ToNaturalLanguage("*/15 * * * *");

        // Assert - Should fail because NCrontab requires 6 fields
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("must have 6 fields"));
    }

    [Test]
    public void ToNaturalLanguage_Invalid7FieldCron_ReturnsError()
    {
        // Act - Try to parse 7-field Quartz cron (has year field)
        var result = _converter.ToNaturalLanguage("0 0 14 * * ? 2025");

        // Assert - Should fail because NCrontab expects exactly 6 fields
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("must have 6 fields"));
    }

    #endregion

    #region Edge Cases - Malformed Expressions

    [TestCase("* * * * *")]  // Wrong field count (5 instead of 6)
    [TestCase("* * * * * * *")]  // Wrong field count (7 instead of 6)
    [TestCase("")]  // Empty string
    [TestCase("     ")]  // Whitespace only
    [TestCase("abc def ghi jkl mno pqr")]  // Garbage input (non-numeric values)
    [TestCase("@#$% * * * * *")]  // Special characters in seconds
    [TestCase("* @#$% * * * *")]  // Special characters in minutes
    public void ToNaturalLanguage_MalformedExpression_ReturnsError(string malformedCron)
    {
        // Act
        var result = _converter.ToNaturalLanguage(malformedCron);

        // Assert - Should return error for all malformed inputs
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            $"Should return error for malformed expression: '{malformedCron}'");
    }

    [TestCase("100 * * * * *", "Second")]  // Second out of range (0-59)
    [TestCase("* 100 * * * *", "Minute")]  // Minute out of range (0-59)
    [TestCase("* * 25 * * *", "Hour")]  // Hour out of range (0-23)
    [TestCase("* * * 32 * *", "Day")]  // Day out of range (1-31)
    [TestCase("* * * * 13 *", "Month")]  // Month out of range (1-12)
    [TestCase("* * * * * 8", "Day-of-week")]  // Day-of-week out of range (0-7)
    [TestCase("* * * 0 * *", "Day")]  // Day 0 invalid (days are 1-31)
    [TestCase("* * * * 0 *", "Month")]  // Month 0 invalid (months are 1-12)
    public void ToNaturalLanguage_InvalidValues_ReturnsError(string cronExpression, string expectedFieldName)
    {
        // Act - Parser now validates ranges strictly
        var result = _converter.ToNaturalLanguage(cronExpression);

        // Assert - Should return error for invalid ranges
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            $"Should return error for invalid value in: '{cronExpression}'");
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain(expectedFieldName),
            $"Error message should mention the invalid field: {expectedFieldName}");
    }

    #endregion

    #region Edge Cases - Boundary Values

    [TestCase("59 * * * * *", "every day")]  // Second boundary (complex pattern)
    [TestCase("0 59 * * * *", "every day")]  // Minute boundary (complex pattern)
    [TestCase("0 0 23 * * *", "every day at 11pm")]  // Hour boundary (max)
    [TestCase("0 0 0 31 * *", "every day on the 31st at 12am")]  // Day boundary (max)
    [TestCase("0 0 0 1 12 *", "every day on the 1st at 12am in december")]  // Month boundary (max)
    [TestCase("0 0 0 * * 0", "every sunday at 12am")]  // Day-of-week boundary (0 = Sunday)
    [TestCase("0 0 0 * * 7", "every sunday at 12am")]  // Day-of-week boundary (7 = Sunday)
    [TestCase("0 0 0 * * 6", "every saturday at 12am")]  // Day-of-week boundary (max valid)
    public void ToNaturalLanguage_BoundaryValues_HandlesCorrectly(string cronExpression, string expectedNatural)
    {
        // Act
        var result = _converter.ToNaturalLanguage(cronExpression);

        // Assert - Should handle all boundary values correctly
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Should successfully parse boundary value: '{cronExpression}'");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedNatural),
            $"Incorrect natural language for boundary value: '{cronExpression}'");
    }

    [Test]
    public void ToNCrontab_LargestValidInterval_Seconds()
    {
        // Act - Every 59 seconds (largest valid interval)
        var result = _converter.ToNCrontab("every 59 seconds");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("*/59 * * * * *"));
    }

    [Test]
    public void ToNCrontab_LargestValidInterval_Minutes()
    {
        // Act - Every 59 minutes (largest valid interval)
        var result = _converter.ToNCrontab("every 59 minutes");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 */59 * * * *"));
    }

    [Test]
    public void ToNCrontab_LargestValidInterval_Hours()
    {
        // Act - Every 23 hours (largest valid interval)
        var result = _converter.ToNCrontab("every 23 hours");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 0 */23 * * *"));
    }

    #endregion

    #region Edge Cases - Range and List Combinations

    [TestCase("0-59 * * * * *", "every day")]  // Full second range - complex pattern defaults to daily
    [TestCase("0 0-59 * * * *", "every day between minutes 0 and 59")]  // Full minute range
    [TestCase("0 0 0-23 * * *", "every day between hours 12am and 11pm")]  // Full hour range
    [TestCase("0 0 0 * * 0-6", "every sunday-saturday at 12am")]  // Full day-of-week range
    [TestCase("0 0 9 * * 1,2,3,4,5", "every monday,tuesday,wednesday,thursday,friday at 9am")]  // All weekdays as list
    [TestCase("0,30 * * * * *", "every day")]  // Multiple seconds (complex pattern defaults to daily)
    [TestCase("0 0,30 * * * *", "every day at minutes 0,30")]  // Multiple minutes
    public void ToNaturalLanguage_RangeAndListCombinations_ParsesCorrectly(string cronExpression, string expectedNatural)
    {
        // Act
        var result = _converter.ToNaturalLanguage(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Should successfully parse range/list: '{cronExpression}'");
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo(expectedNatural),
            $"Incorrect natural language for range/list: '{cronExpression}'");
    }

    #endregion

    #region Edge Cases - DST Transitions

    [Test]
    public void ToNCrontab_DSTSpringForward_HandlesCorrectly()
    {
        // Arrange - US Pacific timezone springs forward at 2am on March 9, 2025 (2am becomes 3am)
        var pacificTimezone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];

        // Create a clock at a time BEFORE the spring forward transition
        var beforeDST = Instant.FromUtc(2025, 3, 9, 9, 0);  // 1am Pacific (before DST)
        var clockBeforeDST = new FakeClock(beforeDST);
        var converterBeforeDST = new NCrontabConverter(
            new NaturalLanguageParser(),
            new NaturalLanguageFormatter(),
            clockBeforeDST,
            DateTimeZone.Utc);

        // Act - Schedule daily job at 2:30am Pacific (time that doesn't exist on DST day)
        var result = converterBeforeDST.ToNCrontab("every day at 2:30am", pacificTimezone);

        // Assert - Should convert to UTC correctly
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        // 2:30am PST = 10:30 UTC (before DST)
        // 2:30am PDT would be 9:30 UTC (during DST, but 2:30am doesn't exist on transition day)
        Assert.That(success.Value, Is.EqualTo("0 30 10 * * *"),
            "Should convert 2:30am Pacific to UTC based on current offset");
    }

    [Test]
    public void ToNCrontab_DSTFallBack_HandlesCorrectly()
    {
        // Arrange - US Pacific timezone falls back at 2am on November 2, 2025 (2am happens twice)
        var pacificTimezone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];

        // Create a clock at a time BEFORE the fall back transition
        var beforeDST = Instant.FromUtc(2025, 11, 2, 8, 0);  // 1am Pacific (before fallback)
        var clockBeforeDST = new FakeClock(beforeDST);
        var converterBeforeDST = new NCrontabConverter(
            new NaturalLanguageParser(),
            new NaturalLanguageFormatter(),
            clockBeforeDST,
            DateTimeZone.Utc);

        // Act - Schedule daily job at 1:30am Pacific (time that occurs twice on DST day)
        var result = converterBeforeDST.ToNCrontab("every day at 1:30am", pacificTimezone);

        // Assert - Should convert to UTC correctly
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        // 1:30am PDT = 8:30 UTC (before fallback)
        Assert.That(success.Value, Is.EqualTo("0 30 8 * * *"),
            "Should convert 1:30am Pacific to UTC based on current offset");
    }

    [Test]
    public void ToNCrontab_CrossingMidnight_DifferentTimezones()
    {
        // Arrange - Tokyo is UTC+9
        var tokyoTimezone = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];

        // Act - "every day at 2am Tokyo time"
        var result = _converter.ToNCrontab("every day at 2am", tokyoTimezone);

        // Assert - 2am Tokyo = 17:00 previous day UTC
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 0 17 * * *"),
            "2am Tokyo should convert to 17:00 UTC (previous day)");
    }

    #endregion

    #region Bidirectional Conversion

    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every day at 2pm")]
    [TestCase("every monday at 9am")]
    [TestCase("every weekday at 9am")]
    public void RoundTrip_NaturalLanguage_PreservesMeaning(string original)
    {
        // Act - Convert to NCrontab and back
        var toCronResult = _converter.ToNCrontab(original);
        Assert.That(toCronResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)toCronResult).Value;

        var toNaturalResult = _converter.ToNaturalLanguage(cron);
        Assert.That(toNaturalResult, Is.TypeOf<ParseResult<string>.Success>());
        var roundTrip = ((ParseResult<string>.Success)toNaturalResult).Value;

        // Assert - Should match original meaning
        Assert.That(roundTrip, Is.EqualTo(original),
            $"Round trip conversion should preserve meaning: '{original}' → '{cron}' → '{roundTrip}'");
    }

    #endregion
}
