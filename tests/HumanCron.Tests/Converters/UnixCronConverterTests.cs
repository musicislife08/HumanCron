using HumanCron.Abstractions;
using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Models;
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
    private INaturalCronConverter _converter = null!;

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
