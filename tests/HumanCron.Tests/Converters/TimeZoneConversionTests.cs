using HumanCron.Converters.Unix;
using HumanCron.Models;
using HumanCron.Models.Internal;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Converters;

/// <summary>
/// Tests for UnixCronBuilder.ConvertTimeToLocal() - timezone conversion logic for Unix cron expressions
///
/// Testing Strategy:
/// - Test OUR conversion logic (ConvertTimeToLocal), not NodaTime's AtLeniently() implementation
/// - Use FakeClock for deterministic test execution
/// - Verify DST edge cases are handled gracefully (gap, ambiguity)
/// - Verify minutes are preserved during timezone conversion
/// - Verify same-timezone scenarios avoid unnecessary conversion
/// </summary>
[TestFixture]
public class TimeZoneConversionTests
{
    private DateTimeZone _pacific = null!;
    private DateTimeZone _eastern = null!;
    private DateTimeZone _utc = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _pacific = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
        _eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
        _utc = DateTimeZone.Utc;
    }

    // ========================================
    // DST Spring Forward (Gap Handling)
    // ========================================

    #region DST Spring Forward Tests

    [Test]
    public void Build_DstSpringForward_BeforeGap_ConvertsSuccessfully()
    {
        // Arrange - March 9, 2025, 1:30am PST (before 2am spring forward to 3am)
        // At 2:00am PST, clocks jump to 3:00am PDT (2:00-2:59am doesn't exist)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 30)); // 1:30am PST = 9:30am UTC
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am PST (in the gap - doesn't exist)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should not throw, AtLeniently() handles the gap
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "ConvertTimeToLocal should handle DST gap without throwing");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
        Assert.That(cronExpression, Does.Contain("30"), "Minutes should be preserved (30)");
    }

    [Test]
    public void Build_DstSpringForward_GapTime_MinutesPreserved()
    {
        // Arrange - FakeClock set to just before spring forward
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 0)); // 1:00am PST
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 45), // 2:45am doesn't exist (springs to 3:45am)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - AtLeniently() should handle gap, verify minutes preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        // Parse cron expression (format: "minute hour day month dayOfWeek")
        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("45"),
            "Minutes (45) should be preserved during DST gap handling");
    }

    [Test]
    public void Build_DstSpringForward_ExactGapStart_HandledGracefully()
    {
        // Arrange - Exactly at the DST transition time
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 10, 0)); // 2:00am PST = 10:00am UTC (exact gap start)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 0), // 2:00am exactly (first non-existent minute)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle gracefully via AtLeniently()
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Should handle exact DST gap start time");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
    }

    [Test]
    public void Build_DstSpringForward_BeforeTransition_NoIssue()
    {
        // Arrange - Time before the gap (1:30am exists)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 30)); // 1:30am PST
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 30), // 1:30am PST (exists, before gap)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert normally (no gap)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved");
    }

    [Test]
    public void Build_DstSpringForward_AfterTransition_NoIssue()
    {
        // Arrange - Time after the gap (3:30am exists)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 11, 30)); // 3:30am PDT
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(3, 30), // 3:30am PDT (exists, after gap)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert normally (no gap)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved");
    }

    #endregion

    // ========================================
    // DST Fall Back (Ambiguous Time)
    // ========================================

    #region DST Fall Back Tests

    [Test]
    public void Build_DstFallBack_BeforeTransition_ConvertsSuccessfully()
    {
        // Arrange - Nov 2, 2025, 1:30am PDT (before 2am fall back to 1am)
        // At 2:00am PDT, clocks fall back to 1:00am PST (1:00-1:59am occurs twice)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 8, 30)); // 1:30am PDT = 8:30am UTC
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 30), // 1:30am (occurs twice)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should not throw, AtLeniently() picks earlier occurrence
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "ConvertTimeToLocal should handle DST ambiguity without throwing");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
        Assert.That(cronExpression, Does.Contain("30"), "Minutes should be preserved (30)");
    }

    [Test]
    public void Build_DstFallBack_AmbiguousTime_MinutesPreserved()
    {
        // Arrange - FakeClock set to before fall back
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 8, 0)); // 1:00am PDT
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 45), // 1:45am (occurs twice)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - AtLeniently() should pick earlier occurrence, verify minutes preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("45"),
            "Minutes (45) should be preserved during DST ambiguity handling");
    }

    [Test]
    public void Build_DstFallBack_ExactTransitionTime_HandledGracefully()
    {
        // Arrange - Exactly at the DST transition time
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 9, 0)); // 2:00am PDT = 9:00am UTC (transition time)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 0), // 1:00am (exact start of ambiguous period)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle gracefully via AtLeniently()
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Should handle exact DST fall back transition time");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
    }

    [Test]
    public void Build_DstFallBack_BeforeAmbiguousPeriod_NoIssue()
    {
        // Arrange - Time before the ambiguous period (12:30am exists uniquely)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 7, 30)); // 12:30am PDT
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(0, 30), // 12:30am (exists uniquely)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert normally (no ambiguity)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved");
    }

    [Test]
    public void Build_DstFallBack_AfterAmbiguousPeriod_NoIssue()
    {
        // Arrange - Time after the ambiguous period (2:30am PST exists uniquely)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 10, 30)); // 2:30am PST
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am PST (exists uniquely after fall back)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert normally (no ambiguity)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved");
    }

    #endregion

    // ========================================
    // Same Timezone (No Conversion)
    // ========================================

    #region Same Timezone Tests

    [Test]
    public void Build_SameTimezoneUtc_NoConversion()
    {
        // Arrange - Source is UTC, server is UTC (no conversion needed)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = DateTimeZone.Utc,
            TimeOfDay = new TimeOnly(14, 0), // 2pm UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - No conversion, should output 14:00 unchanged
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        Assert.That(cronExpression, Is.EqualTo("0 14 * * *"),
            "UTC 14:00 to UTC server should remain 14:00 (no conversion)");
    }

    [Test]
    public void Build_SameTimezone_WithMinutes_NoConversion()
    {
        // Arrange - Source is UTC, server is UTC (no conversion needed)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = DateTimeZone.Utc,
            TimeOfDay = new TimeOnly(14, 30), // 2:30pm UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - No conversion, minutes preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        Assert.That(cronExpression, Is.EqualTo("30 14 * * *"),
            "UTC 14:30 to UTC server should remain 14:30 (no conversion)");
    }

    [Test]
    public void Build_SameTimezone_Midnight_NoConversion()
    {
        // Arrange - Midnight UTC (edge case - may shift to previous day in some timezones)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = DateTimeZone.Utc,
            TimeOfDay = new TimeOnly(0, 0), // Midnight UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - No conversion, should output 00:00 unchanged
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        Assert.That(cronExpression, Is.EqualTo("0 0 * * *"),
            "UTC midnight (00:00) to UTC server should remain 00:00 (no conversion)");
    }

    #endregion

    // ========================================
    // Cross Timezone Conversion
    // ========================================

    #region Cross Timezone Conversion Tests

    [Test]
    public void Build_CrossTimezone_PacificToUtc_ConvertsCorrectly()
    {
        // Arrange - User in Pacific (UTC-8 in winter), server in UTC
        // 2pm Pacific = 10pm UTC (14 + 8 = 22)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0)); // Winter date (no DST)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 0), // 2pm Pacific
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert to server's local timezone
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        // Verify minutes are preserved
        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("0"),
            "Minutes should be preserved during cross-timezone conversion");
    }

    [Test]
    public void Build_CrossTimezone_MinutesPreserved()
    {
        // Arrange - Test that minutes are preserved across timezones
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 30), // 2:30pm Pacific
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Minutes (30) should be preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"),
            "Minutes (30) should be preserved during timezone conversion");
    }

    [Test]
    public void Build_CrossTimezone_EasternToPacific_ConvertsCorrectly()
    {
        // Arrange - Eastern (UTC-5) to Pacific (would be UTC-8 if server were Pacific)
        // Testing the conversion logic with different timezones
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(14, 45), // 2:45pm Eastern
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert to server local time, preserving minutes
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("45"),
            "Minutes (45) should be preserved during Eastern to server conversion");
    }

    #endregion

    // ========================================
    // Edge Cases
    // ========================================

    #region Edge Cases

    [Test]
    public void Build_Midnight_HandledCorrectly()
    {
        // Arrange - Midnight edge case
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(0, 0), // Midnight
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];
        var hour = parts[1];

        Assert.That(minute, Is.EqualTo("0"), "Midnight minutes should be 0");
        Assert.That(hour, Does.Match(@"^\d{1,2}$"), "Hour should be numeric");
    }

    [Test]
    public void Build_Time2359_HandledCorrectly()
    {
        // Arrange - 11:59pm edge case
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(23, 59), // 11:59pm
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("59"), "Minutes should be preserved (59)");
    }

    [Test]
    public void Build_LeapYear_Feb29_HandledCorrectly()
    {
        // Arrange - Leap year (Feb 29, 2024)
        var fakeClock = new FakeClock(Instant.FromUtc(2024, 2, 29, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 30),
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle leap year date gracefully
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Should handle leap year date (Feb 29) correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved on leap year date");
    }

    [Test]
    public void Build_YearBoundary_Dec31ToJan1_HandledCorrectly()
    {
        // Arrange - Year boundary (Dec 31, 2024)
        var fakeClock = new FakeClock(Instant.FromUtc(2024, 12, 31, 23, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(23, 30), // 11:30pm Dec 31
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle year boundary correctly
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Should handle year boundary (Dec 31 → Jan 1) correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"), "Minutes should be preserved across year boundary");
    }

    [Test]
    public void Build_Time1430_PreservesMinutesAcrossTimezones()
    {
        // Arrange - Explicit test: 2:30pm should preserve :30 minutes
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(14, 30), // 2:30pm Eastern
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"),
            "Minutes (:30) should be preserved when converting 2:30pm across timezones");
    }

    #endregion

    // ========================================
    // Multi-Week Schedule (Validation Test)
    // ========================================

    #region Multi-Week Schedule Tests

    [Test]
    public void Build_MultiWeekSchedule_ReturnsError()
    {
        // Arrange - "Every 3 weeks on Sunday at 2pm Pacific"
        // Unix cron doesn't support multi-week intervals
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 0), // 2pm
            DayOfWeek = DayOfWeek.Sunday,
            Interval = 3,
            Unit = IntervalUnit.Weeks
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should return error (Unix cron doesn't support multi-week)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>(),
            "Multi-week intervals should return error for Unix cron");

        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("multi-week").IgnoreCase,
            "Error message should mention multi-week limitation");
    }

    [Test]
    public void Build_SingleWeekSchedule_SuccessfullyConverts()
    {
        // Arrange - "Every 1 week on Sunday at 2pm Pacific" (valid)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 0), // 2pm
            DayOfWeek = DayOfWeek.Sunday,
            Interval = 1,
            Unit = IntervalUnit.Weeks
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should succeed (1-week is valid)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Single-week intervals should be valid for Unix cron");

        var cronExpression = ((ParseResult<string>.Success)result).Value;

        // Verify minutes preserved
        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("0"), "Minutes should be preserved");
    }

    [Test]
    public void Build_MultiWeekSchedule_PreservesAllProperties()
    {
        // Arrange - Verify ScheduleSpec properties are preserved even when validation fails
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(14, 30), // 2:30pm
            DayOfWeek = DayOfWeek.Sunday,
            Interval = 3, // Multi-week
            Unit = IntervalUnit.Weeks
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should fail validation, but spec properties remain intact
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());

        // Verify original spec wasn't modified
        Assert.That(spec.Interval, Is.EqualTo(3), "Interval should remain 3");
        Assert.That(spec.Unit, Is.EqualTo(IntervalUnit.Weeks), "Unit should remain Weeks");
        Assert.That(spec.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday), "DayOfWeek should remain Sunday");
        Assert.That(spec.TimeOfDay, Is.EqualTo(new TimeOnly(14, 30)), "TimeOfDay should remain 14:30");
        Assert.That(spec.TimeZone.Id, Is.EqualTo("America/Los_Angeles"), "TimeZone should remain Pacific");
    }

    #endregion

    // ========================================
    // No Time Specified (Default to Midnight)
    // ========================================

    #region No Time Specified Tests

    [Test]
    public void Build_NoTimeSpecified_DefaultsToMidnight()
    {
        // Arrange - Daily interval without specific time
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = null, // No time specified
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should default to midnight (00:00)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];
        var hour = parts[1];

        Assert.That(minute, Is.EqualTo("0"), "Should default to 0 minutes");
        Assert.That(hour, Is.EqualTo("0"), "Should default to 0 hours (midnight)");
    }

    [Test]
    public void Build_WeeklyNoTime_DefaultsToMidnight()
    {
        // Arrange - Weekly interval without specific time
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = null, // No time specified
            DayOfWeek = DayOfWeek.Monday,
            Interval = 1,
            Unit = IntervalUnit.Weeks
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should default to midnight
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];
        var hour = parts[1];

        Assert.That(minute, Is.EqualTo("0"), "Should default to 0 minutes");
        Assert.That(hour, Is.EqualTo("0"), "Should default to 0 hours (midnight)");
    }

    #endregion
}
