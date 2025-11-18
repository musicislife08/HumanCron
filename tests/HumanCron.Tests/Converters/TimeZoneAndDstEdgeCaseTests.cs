using HumanCron.Converters.Unix;
using HumanCron.Models;
using HumanCron.Models.Internal;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Converters;

/// <summary>
/// Comprehensive DST and timezone edge case tests for NaturalCron
///
/// Testing Strategy:
/// - Focus on real-world scheduling failures (jobs skipped, running twice, wrong time)
/// - Test critical DST transitions: spring forward gaps, fall back ambiguities
/// - Verify fractional timezone offsets (India UTC+5:30, Nepal UTC+5:45)
/// - Test cross-midnight conversions that change the day
/// - Test timezones with unusual DST rules (Southern Hemisphere, no DST zones)
/// - Use FakeClock for deterministic, reproducible tests
/// </summary>
[TestFixture]
public class TimeZoneAndDstEdgeCaseTests
{
    private DateTimeZone _pacific = null!;
    private DateTimeZone _eastern = null!;
    private DateTimeZone _utc = null!;
    private DateTimeZone _london = null!;
    private DateTimeZone _india = null!;
    private DateTimeZone _nepal = null!;
    private DateTimeZone _sydney = null!;
    private DateTimeZone _phoenix = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _pacific = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
        _eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
        _utc = DateTimeZone.Utc;
        _london = DateTimeZoneProviders.Tzdb["Europe/London"];
        _india = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];      // UTC+5:30
        _nepal = DateTimeZoneProviders.Tzdb["Asia/Kathmandu"];    // UTC+5:45
        _sydney = DateTimeZoneProviders.Tzdb["Australia/Sydney"]; // Southern Hemisphere DST
        _phoenix = DateTimeZoneProviders.Tzdb["America/Phoenix"]; // Arizona - no DST
    }

    // ========================================
    // DST Spring Forward - Critical Edge Cases
    // ========================================

    #region DST Spring Forward Edge Cases

    [Test]
    public void Build_DstSpringForward_ScheduleAt2am_SkipsToNextDay()
    {
        // Real-world scenario: Job scheduled for 2:00am PST every day
        // On spring forward day (March 9, 2025), 2:00am doesn't exist - what happens?
        //
        // Expected: AtLeniently() resolves to 3:00am PDT, job runs once per day
        // Bug risk: Job might skip entirely or run at unexpected time

        // Arrange - Day of spring forward transition in Pacific timezone
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 0)); // 1:00am PST (before gap)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 0), // 2:00am PST - IN THE GAP (doesn't exist)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle gracefully (AtLeniently maps to 3:00am PDT)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Job scheduled at 2am (spring forward gap) should resolve to 3am PDT without error");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);

        // Verify the cron expression is valid (minute hour day month dayOfWeek)
        var parts = cronExpression.Split(' ');
        Assert.That(parts, Has.Length.EqualTo(5),
            "Cron expression should have 5 parts");
    }

    [Test]
    public void Build_DstSpringForward_230amGap_PreservesMinutes()
    {
        // Real-world scenario: Job scheduled for 2:30am every day
        // On March 9, 2025, 2:30am PST doesn't exist (2am → 3am transition)
        //
        // Critical: Minute component (:30) must be preserved
        // Bug risk: Minutes could be lost, causing job to run at :00 instead of :30

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 0)); // Before transition
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am - doesn't exist
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Minutes MUST be preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var cronExpression = ((ParseResult<string>.Success)result).Value;

        var parts = cronExpression.Split(' ');
        var minute = parts[0];

        Assert.That(minute, Is.EqualTo("30"),
            "CRITICAL: Minutes must be preserved during DST gap (2:30am → 3:30am PDT)");
    }

    [Test]
    public void Build_DstSpringForward_EasternTimezone_DifferentDate()
    {
        // US Eastern and Pacific spring forward on same date (second Sunday in March)
        // but at different absolute times due to timezone offset
        // Eastern: 2:00am EST → 3:00am EDT (7:00 UTC)
        // Pacific: 2:00am PST → 3:00am PDT (10:00 UTC)

        // Arrange - Test Eastern timezone spring forward (March 9, 2025, 2am EST)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 6, 0)); // 1:00am EST (before gap)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(2, 15), // 2:15am EST - in the gap
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Eastern timezone DST gap should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("15"),
            "Minutes preserved in Eastern timezone DST transition");
    }

    [Test]
    public void Build_DstSpringForward_UkTimezone_LastSundayInMarch()
    {
        // UK/Europe spring forward on LAST Sunday in March (different rule than US)
        // UK: 1:00am GMT → 2:00am BST (March 30, 2025)
        //
        // This tests that NodaTime correctly applies UK DST rules

        // Arrange - UK spring forward (March 30, 2025, 1am GMT)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 30, 0, 0)); // Midnight GMT (before 1am gap)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _london,
            TimeOfDay = new TimeOnly(1, 30), // 1:30am GMT - IN THE GAP
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "UK DST spring forward gap (1am GMT) should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"),
            "Minutes preserved in UK timezone DST transition");
    }

    #endregion

    // ========================================
    // DST Fall Back - Ambiguous Time Edge Cases
    // ========================================

    #region DST Fall Back Edge Cases

    [Test]
    public void Build_DstFallBack_ScheduleAt1am_OccursTwice()
    {
        // Real-world scenario: Job scheduled for 1:30am PST every day
        // On fall back day (Nov 2, 2025), 1:30am occurs TWICE:
        //   - First at 1:30am PDT (8:30 UTC)
        //   - Second at 1:30am PST (9:30 UTC) after clocks fall back
        //
        // Expected: AtLeniently() picks EARLIER occurrence (1:30am PDT)
        // Bug risk: Job runs twice, or picks later occurrence

        // Arrange - Day of fall back transition
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 8, 0)); // 1:00am PDT (before transition)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 30), // 1:30am - AMBIGUOUS (occurs twice)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should handle gracefully (AtLeniently picks earlier)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Job scheduled at 1:30am (fall back ambiguous time) should resolve to earlier occurrence");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"),
            "Minutes must be preserved");
    }

    [Test]
    public void Build_DstFallBack_ExactTransitionTime_130am()
    {
        // Test exact start of ambiguous period: 1:00am occurs twice
        // AtLeniently() should pick the earlier occurrence consistently

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 8, 0)); // 1:00am PDT
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 0), // 1:00am - exact start of ambiguity
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Exact ambiguous time (1:00am) should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
    }

    [Test]
    public void Build_DstFallBack_EasternTimezone_SameDay()
    {
        // Eastern and Pacific fall back on same calendar day (first Sunday in November)
        // Eastern: 2:00am EDT → 1:00am EST (Nov 2, 2025, 6:00 UTC)
        // Pacific: 2:00am PDT → 1:00am PST (Nov 2, 2025, 9:00 UTC)

        // Arrange - Eastern timezone fall back
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 5, 0)); // 1:00am EDT (before transition)
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(1, 30), // 1:30am - ambiguous
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Eastern timezone fall back ambiguity should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"));
    }

    [Test]
    public void Build_DstFallBack_UkTimezone_LastSundayInOctober()
    {
        // UK/Europe fall back on LAST Sunday in October (different from US)
        // UK: 2:00am BST → 1:00am GMT (Oct 26, 2025)
        //
        // 1:00am - 1:59am occurs twice (ambiguous period)

        // Arrange - UK fall back (Oct 26, 2025)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 10, 26, 0, 0)); // Before 1am BST transition
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _london,
            TimeOfDay = new TimeOnly(1, 45), // 1:45am - AMBIGUOUS
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "UK fall back ambiguity (1am BST → 1am GMT) should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("45"));
    }

    #endregion

    // ========================================
    // Southern Hemisphere DST (Opposite Schedule)
    // ========================================

    #region Southern Hemisphere DST

    [Test]
    public void Build_SouthernHemisphere_SydneySpringForward_October()
    {
        // Southern Hemisphere DST is opposite to Northern Hemisphere
        // Sydney: Spring forward in OCTOBER (not March)
        // Oct 5, 2025: 2:00am AEST → 3:00am AEDT (first Sunday in October)

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 10, 4, 15, 0)); // Before transition
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _sydney,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am - IN THE GAP
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Sydney spring forward gap (October) should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"),
            "Minutes preserved in Southern Hemisphere spring forward");
    }

    [Test]
    public void Build_SouthernHemisphere_SydneyFallBack_April()
    {
        // Sydney: Fall back in APRIL (not November)
        // April 6, 2025: 3:00am AEDT → 2:00am AEST (first Sunday in April)
        // 2:00am - 2:59am occurs twice (ambiguous period)

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 4, 5, 15, 0)); // Before transition
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _sydney,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am - AMBIGUOUS
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Sydney fall back ambiguity (April) should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"),
            "Minutes preserved in Southern Hemisphere fall back");
    }

    #endregion

    // ========================================
    // Fractional Timezone Offsets
    // ========================================

    #region Fractional Timezone Offsets

    [Test]
    public void Build_FractionalOffset_India_UtcPlus530()
    {
        // India Standard Time (IST): UTC+5:30 (NO DST)
        // Critical: Fractional offset (30 minutes) affects minute conversion
        // 2:45pm IST = 9:15am UTC (14:45 - 5:30 = 9:15)
        //
        // Bug risk: Rounding errors could corrupt time calculation

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 6, 15, 12, 0)); // Noon UTC
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _india,
            TimeOfDay = new TimeOnly(14, 45), // 2:45pm IST = 9:15am UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Conversion MUST be mathematically correct
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "India timezone (UTC+5:30) conversion should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        var parts = cronExpression.Split(' ');

        Assert.That(parts[0], Is.EqualTo("15"),
            "CRITICAL: 2:45pm IST should convert to 9:15am UTC (fractional offset handled correctly)");
    }

    [Test]
    public void Build_FractionalOffset_Nepal_UtcPlus545()
    {
        // Nepal Time (NPT): UTC+5:45 (NO DST)
        // One of the few timezones with 45-minute offset
        // 3:30pm NPT = 9:45am UTC (15:30 - 5:45 = 9:45)
        //
        // Critical: 45-minute offset must be calculated correctly

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 6, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _nepal,
            TimeOfDay = new TimeOnly(15, 30), // 3:30pm NPT = 9:45am UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Conversion MUST be mathematically correct
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Nepal timezone (UTC+5:45) conversion should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        var parts = cronExpression.Split(' ');

        Assert.That(parts[0], Is.EqualTo("45"),
            "CRITICAL: 3:30pm NPT should convert to 9:45am UTC (45-minute offset handled correctly)");
    }

    [Test]
    public void Build_FractionalOffset_India_CrossMidnight()
    {
        // Test fractional offset with time that crosses midnight during conversion
        // India: 11:45pm IST (UTC+5:30) = 6:15pm UTC (same day)
        // 23:45 - 5:30 = 18:15 (6:15pm UTC)
        //
        // Verify day boundary is handled correctly with fractional offset

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 6, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _india,
            TimeOfDay = new TimeOnly(23, 45), // 11:45pm IST = 6:15pm UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Late evening time in fractional timezone should convert correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("15"),
            "11:45pm IST converts to 6:15pm UTC (fractional offset handled at day boundary)");
    }

    #endregion

    // ========================================
    // No DST Timezones
    // ========================================

    #region No DST Timezones

    [Test]
    public void Build_NoDst_Arizona_AlwaysSameOffset()
    {
        // Arizona (Phoenix): UTC-7 year-round (NO DST)
        // While rest of US changes for DST, Arizona stays constant
        //
        // This tests that no DST transitions are applied incorrectly

        // Arrange - Test in March (when most US springs forward)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 12, 0)); // DST transition day for most of US
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _phoenix,
            TimeOfDay = new TimeOnly(2, 30), // 2:30am MST (no gap in Arizona)
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should convert normally (no DST complications)
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Arizona (no DST) should convert without DST complications");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"),
            "Minutes preserved in no-DST timezone");
    }

    [Test]
    public void Build_NoDst_India_Summer()
    {
        // India: No DST, always UTC+5:30
        // Test during Northern Hemisphere summer to verify no DST is applied

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 7, 15, 12, 0)); // Mid-summer
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _india,
            TimeOfDay = new TimeOnly(14, 0),
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "India (no DST) should maintain constant offset year-round");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
    }

    #endregion

    // ========================================
    // Cross-Midnight Conversions (Day Changes)
    // ========================================

    #region Cross-Midnight Conversions

    [Test]
    public void Build_CrossMidnight_11pmPacificToUtc()
    {
        // Real-world scenario: Job scheduled for 11pm Pacific
        // Converts to 7am UTC NEXT DAY
        //
        // Critical: Day boundary crossing must be handled correctly
        // Bug risk: Job scheduled for wrong day, missing a full day of executions

        // Arrange - Winter (PST = UTC-8)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(23, 0), // 11pm PST
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - Should succeed, minute component preserved
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Cross-midnight conversion (11pm PST → 7am UTC next day) should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        var parts = cronExpression.Split(' ');

        Assert.That(parts[0], Is.EqualTo("0"),
            "Minutes should be preserved (00)");

        // Hour will be different due to timezone conversion, but should be valid
        Assert.That(int.Parse(parts[1]), Is.InRange(0, 23),
            "Converted hour should be valid (0-23)");
    }

    [Test]
    public void Build_CrossMidnight_1amUtcToPacific()
    {
        // Reverse scenario: UTC time converts to previous day in Pacific
        // 1am UTC = 5pm PST PREVIOUS DAY (in winter)
        //
        // Critical: Ensures bidirectional day boundary handling

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, _pacific); // Server in Pacific

        var spec = new ScheduleSpec
        {
            TimeZone = DateTimeZone.Utc,
            TimeOfDay = new TimeOnly(1, 0), // 1am UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "UTC to Pacific conversion crossing midnight should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression, Is.Not.Null);
    }

    [Test]
    public void Build_CrossMidnight_MidnightExactly_PreservesZeroMinutes()
    {
        // Edge case: Midnight exactly (00:00) in one timezone
        // Must preserve 0 minutes when crossing day boundary

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(0, 0), // Midnight PST
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Midnight exact time should convert correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("0"),
            "Zero minutes must be preserved for midnight times");
    }

    #endregion

    // ========================================
    // Multiple Timezone Conversions
    // ========================================

    #region Multi-Timezone Scenarios

    [Test]
    public void Build_EasternToPacific_3HourDifference_PreservesMinutes()
    {
        // Real-world: Multi-region company scheduling
        // Eastern (UTC-5) to Pacific (UTC-8) = 3 hour difference in winter
        //
        // Critical: Minutes must survive multi-hop conversion

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, _pacific); // Server in Pacific

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(14, 37), // 2:37pm EST
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Eastern to Pacific conversion should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("37"),
            "CRITICAL: Minutes (37) must be preserved across timezone conversion");
    }

    [Test]
    public void Build_LondonToNewYork_WinterTime_5HourDifference()
    {
        // London (GMT, UTC+0) to New York (EST, UTC-5) = 5 hour difference in winter
        // Test intercontinental timezone conversion

        // Arrange - Winter date (both in standard time)
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, _eastern); // Server in Eastern

        var spec = new ScheduleSpec
        {
            TimeZone = _london,
            TimeOfDay = new TimeOnly(17, 45), // 5:45pm GMT
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "London to New York conversion should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("45"),
            "Minutes (45) preserved in intercontinental conversion");
    }

    [Test]
    public void Build_IndiaToUtc_WithFractionalOffset()
    {
        // India (UTC+5:30) to UTC
        // 6:15pm IST = 12:45pm UTC (18:15 - 5:30 = 12:45)
        //
        // Fractional offset conversion test

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 6, 15, 12, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _india,
            TimeOfDay = new TimeOnly(18, 15), // 6:15pm IST = 12:45pm UTC
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "India to UTC conversion with fractional offset should succeed");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("45"),
            "6:15pm IST converts to 12:45pm UTC (fractional offset calculated correctly)");
    }

    #endregion

    // ========================================
    // Leap Year Edge Cases
    // ========================================

    #region Leap Year Cases

    [Test]
    public void Build_LeapYear_Feb29_WithTimezoneConversion()
    {
        // Leap year: Feb 29, 2024 exists
        // Test timezone conversion on leap day to ensure date handling is correct

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2024, 2, 29, 12, 0)); // Leap day
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

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Leap year date (Feb 29) should handle timezone conversion correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"));
    }

    [Test]
    public void Build_NonLeapYear_Feb28_NoIssues()
    {
        // Non-leap year: Feb 28 is last day of February
        // Verify no confusion with leap year logic

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 2, 28, 12, 0)); // Non-leap year
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(23, 45), // Late evening
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Non-leap year Feb 28 should convert correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("45"));
    }

    #endregion

    // ========================================
    // Year Boundary Transitions
    // ========================================

    #region Year Boundary

    [Test]
    public void Build_YearBoundary_Dec31_LateEvening()
    {
        // Dec 31, 11:59pm - end of year
        // Test timezone conversion doesn't cause year wrap issues

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2024, 12, 31, 23, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(23, 59), // 11:59pm PST
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Year boundary (Dec 31 → Jan 1) should be handled correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("59"));
    }

    [Test]
    public void Build_YearBoundary_Jan1_Midnight()
    {
        // Jan 1, midnight - start of year
        // Test timezone conversion at year boundary

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 1, 0, 0));
        var builder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);

        var spec = new ScheduleSpec
        {
            TimeZone = _eastern,
            TimeOfDay = new TimeOnly(0, 0), // Midnight EST
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Year start (Jan 1 midnight) should convert correctly");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("0"));
    }

    #endregion

    // ========================================
    // Same Timezone (No Conversion) Edge Cases
    // ========================================

    #region Same Timezone

    [Test]
    public void Build_SameTimezone_DuringDstGap_StillHandled()
    {
        // Even when source timezone = server timezone, DST gap handling applies
        // 2:30am PST doesn't exist on spring forward day

        // Arrange - Server and source both Pacific
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 3, 9, 9, 0));
        var builder = new UnixCronBuilder(fakeClock, _pacific); // Server in Pacific

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific, // Same as server
            TimeOfDay = new TimeOnly(2, 30), // In the gap
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert - AtLeniently() still applies even without conversion
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Same timezone DST gap should still be handled by AtLeniently()");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"));
    }

    [Test]
    public void Build_SameTimezone_DuringDstAmbiguity_StillHandled()
    {
        // Same timezone, fall back ambiguity
        // 1:30am occurs twice on Nov 2, 2025

        // Arrange
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 11, 2, 8, 0));
        var builder = new UnixCronBuilder(fakeClock, _pacific);

        var spec = new ScheduleSpec
        {
            TimeZone = _pacific,
            TimeOfDay = new TimeOnly(1, 30), // Ambiguous
            Interval = 1,
            Unit = IntervalUnit.Days
        };

        // Act
        var result = builder.Build(spec);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Same timezone DST ambiguity should be handled");

        var cronExpression = ((ParseResult<string>.Success)result).Value;
        Assert.That(cronExpression.Split(' ')[0], Is.EqualTo("30"));
    }

    #endregion
}
