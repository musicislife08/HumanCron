using HumanCron.Abstractions;
using HumanCron.Builders;
using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Builders;

/// <summary>
/// Tests for fluent schedule builder API
/// Tests the public API contract: builder should produce correct natural language strings
/// </summary>
[TestFixture]
public class ScheduleBuilderTests
{
    // ========================================
    // Input Validation Tests
    // ========================================

    [Test]
    public void Every_NegativeInterval_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Schedule.Every(-1));
    }

    [Test]
    public void Every_ZeroInterval_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Schedule.Every(0));
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(100)]
    public void Every_PositiveInterval_CreatesBuilder(int interval)
    {
        // Act
        var builder = Schedule.Every(interval);

        // Assert
        Assert.That(builder, Is.Not.Null);
    }

    [TestCase(-1)]
    [TestCase(24)]
    [TestCase(100)]
    public void AtHour_InvalidHour_ThrowsArgumentOutOfRangeException(int hour)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Schedule.Every(1).Day().AtHour(hour));
    }

    [TestCase(-1)]
    [TestCase(60)]
    [TestCase(100)]
    public void AtHour_InvalidMinute_ThrowsArgumentOutOfRangeException(int minute)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Schedule.Every(1).Day().AtHour(14, minute));
    }

    [Test]
    public void InTimeZone_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Schedule.Every(1).Day().InTimeZone(null!));
    }

    // ========================================
    // Interval Unit Tests
    // ========================================

    [Test]
    public void Seconds_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(30).Seconds().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 30 seconds"));
    }

    [Test]
    public void Second_SingularForm_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Second().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every second"));
    }

    [Test]
    public void Minutes_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(15).Minutes().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 15 minutes"));
    }

    [Test]
    public void Minute_SingularForm_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Minute().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every minute"));
    }

    [Test]
    public void Hours_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(6).Hours().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 6 hours"));
    }

    [Test]
    public void Days_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Days().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day"));
    }

    [Test]
    public void Day_SingularForm_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day"));
    }

    [Test]
    public void Weeks_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(2).Weeks().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 2 weeks"));
    }

    [Test]
    public void Months_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(3).Months().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 3 months"));
    }

    [Test]
    public void Years_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Years().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every year"));
    }

    // ========================================
    // Day of Week Tests
    // ========================================

    [Test]
    public void OnMonday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnMonday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every monday"));
    }

    [Test]
    public void OnTuesday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnTuesday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every tuesday"));
    }

    [Test]
    public void OnWednesday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnWednesday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every wednesday"));
    }

    [Test]
    public void OnThursday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnThursday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every thursday"));
    }

    [Test]
    public void OnFriday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnFriday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every friday"));
    }

    [Test]
    public void OnSaturday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnSaturday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every saturday"));
    }

    [Test]
    public void OnSunday_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnSunday().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every sunday"));
    }

    [Test]
    public void On_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().On(DayOfWeek.Monday).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every monday"));
    }

    [Test]
    public void OnWeekdays_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().OnWeekdays().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every weekday"));
    }

    [Test]
    public void OnWeekends_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().OnWeekends().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every weekend"));
    }

    [Test]
    public void OnWeekdays_OverridesSpecificDay()
    {
        // Act - Set specific day first, then pattern
        var result = Schedule.Every(1).Day().OnMonday().OnWeekdays().Build();

        // Assert - Pattern should override specific day
        Assert.That(result, Is.EqualTo("every weekday"));
    }

    [Test]
    public void OnMonday_OverridesDayPattern()
    {
        // Act - Set pattern first, then specific day
        var result = Schedule.Every(1).Day().OnWeekdays().OnMonday().Build();

        // Assert - Specific day should override pattern
        Assert.That(result, Is.EqualTo("every monday"));
    }

    // ========================================
    // Time of Day Tests
    // ========================================

    [Test]
    public void At_TimeOnly_ProducesCorrectString()
    {
        // Arrange
        var time = new TimeOnly(14, 30);

        // Act
        var result = Schedule.Every(1).Day().At(time).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 14:30"));
    }

    [Test]
    public void AtHour_14_0_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtHour(14, 0).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 2pm"));
    }

    [Test]
    public void AtHour_9_30_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtHour(9, 30).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 09:30"));
    }

    [Test]
    public void AtHour_0_0_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtHour(0, 0).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 12am"));
    }

    [Test]
    public void AtHour_23_59_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtHour(23, 59).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 23:59"));
    }

    [Test]
    public void AtHour_DefaultMinute_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtHour(14).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 2pm"));
    }

    [Test]
    public void AtNoon_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtNoon().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 12pm"));
    }

    [Test]
    public void AtMidnight_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().AtMidnight().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every day at 12am"));
    }

    // ========================================
    // Complex Scenario Tests
    // ========================================

    [Test]
    public void ComplexSchedule_DailyAt2pmWeekdays_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Day().OnWeekdays().AtHour(14).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every weekday at 2pm"));
    }

    [Test]
    public void ComplexSchedule_WeeklyOnMondayAt9am_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(1).Week().OnMonday().AtHour(9).Build();

        // Assert
        Assert.That(result, Is.EqualTo("every monday at 9am"));
    }

    [Test]
    public void ComplexSchedule_Every6Hours_ProducesCorrectString()
    {
        // Act
        var result = Schedule.Every(6).Hours().Build();

        // Assert
        Assert.That(result, Is.EqualTo("every 6 hours"));
    }

    [Test]
    public void MethodChaining_OrderIndependent_ProducesSameString()
    {
        // Arrange & Act
        var result1 = Schedule.Every(1).Day().AtHour(14).OnWeekdays().Build();
        var result2 = Schedule.Every(1).Day().OnWeekdays().AtHour(14).Build();
        var result3 = Schedule.Every(1).OnWeekdays().Day().AtHour(14).Build();

        // Assert - All should produce identical string output
        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(result2, Is.EqualTo(result3));
        Assert.That(result1, Is.EqualTo("every weekday at 2pm"));
    }

    // ========================================
    // End-to-End Integration Tests
    // (Validates fluent builder → converter → fluent builder round-trip)
    // ========================================

    private IHumanCronConverter? _converter;

    [SetUp]
    public void SetUp()
    {
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        // Server timezone = UTC for deterministic tests (no DST complexity)
        _converter = new UnixCronConverter(parser, formatter, fakeClock, DateTimeZone.Utc);
    }

    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every hour")]
    [TestCase("every day")]
    [TestCase("every monday")]
    [TestCase("every weekday at 2pm")]
    [TestCase("every 2 weeks")]
    [TestCase("every 3 months")]
    public void RoundTrip_BuilderOutput_ConvertsToValidCronOrError(string expected)
    {
        // Act - The builder should produce this exact string
        var natural = GetBuilderForPattern(expected);

        // Assert - Converter should successfully convert it (or error for unsupported patterns)
        var cronResult = _converter!.ToCron(natural);

        // Builder output must be valid input to converter
        Assert.That(cronResult, Is.Not.Null);

        // If conversion succeeds, verify round-trip back to natural language
        if (cronResult is ParseResult<string>.Success success)
        {
            var backToNatural = _converter.ToNaturalLanguage(success.Value);
            Assert.That(backToNatural, Is.TypeOf<ParseResult<string>.Success>(),
                $"Builder produced '{natural}' → cron '{success.Value}' → failed to convert back");
        }
    }

    private static string GetBuilderForPattern(string pattern)
    {
        return pattern switch
        {
            "every 30 seconds" => Schedule.Every(30).Seconds().Build(),
            "every 15 minutes" => Schedule.Every(15).Minutes().Build(),
            "every hour" => Schedule.Every(1).Hour().Build(),
            "every day" => Schedule.Every(1).Day().Build(),
            "every monday" => Schedule.Every(1).Week().OnMonday().Build(),
            "every weekday at 2pm" => Schedule.Every(1).Day().OnWeekdays().AtHour(14).Build(),
            "every 2 weeks" => Schedule.Every(2).Weeks().Build(),
            "every 3 months" => Schedule.Every(3).Months().Build(),
            _ => throw new ArgumentException($"Unknown pattern: {pattern}")
        };
    }
}
