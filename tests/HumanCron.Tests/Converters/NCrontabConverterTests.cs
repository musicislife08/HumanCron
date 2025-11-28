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
