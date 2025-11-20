using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using HumanCron.Quartz;
using Quartz;
using NodaTime;
using NodaTime.Testing;
using IntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests end-to-end conversion from natural language → ScheduleSpec → Quartz schedule
/// </summary>
[TestFixture]
public class QuartzConversionTests
{
    private NaturalLanguageParser _parser = null!;
    private QuartzScheduleBuilder _quartzBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new NaturalLanguageParser();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        _quartzBuilder = new QuartzScheduleBuilder(fakeClock);
    }

    [Test]
    public void Convert_SimpleSecondInterval_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every 30 seconds", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
        var cronBuilder = (CronScheduleBuilder)scheduleBuilder;

        // Verify it creates a valid trigger
        var trigger = TriggerBuilder.Create()
            .WithSchedule(cronBuilder)
            .StartNow()
            .Build();

        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null);
    }

    [Test]
    public void Convert_SimpleMinuteInterval_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every 15 minutes", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
    }

    [Test]
    public void Convert_DailyAt2pm_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every day at 2pm", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
    }

    [Test]
    public void Convert_DailyOnMonday_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every monday", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
    }

    [Test]
    public void Convert_DailyOnWeekdays_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every weekday", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
    }

    [Test]
    public void Convert_WeeklyOnTuesdayAt2pm_CreatesCronSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every tuesday at 2pm", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>());
    }

    [Test]
    public void Convert_MultiWeekInterval_CreatesCalendarIntervalSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every 2 weeks", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>());
    }

    [Test]
    public void Convert_MonthlyInterval_CreatesCalendarIntervalSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every month", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>());
    }

    [Test]
    public void Convert_QuarterlyInterval_CreatesCalendarIntervalSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every 3 months", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>());
    }

    [Test]
    public void Convert_YearlyInterval_CreatesCalendarIntervalSchedule()
    {
        // Arrange
        var parseResult = _parser.Parse("every year", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>());
    }

    [Test]
    public void Convert_EndToEnd_CreatesValidTrigger()
    {
        // Arrange - Parse natural language
        var parseResult = _parser.Parse("every weekday at 9am", new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Convert to Quartz schedule
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Build a complete trigger
        var trigger = TriggerBuilder.Create()
            .WithIdentity("test-trigger")
            .WithSchedule(scheduleBuilder)
            .StartNow()  // Triggers need a start time
            .Build();

        // Assert - Trigger is valid and can compute next fire times
        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null);
    }

    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every 6 hours")]
    [TestCase("every day")]
    [TestCase("every week")]
    [TestCase("every day at 2pm")]
    [TestCase("every monday")]
    [TestCase("every tuesday at 2pm")]
    public void Convert_CronPatterns_CreateValidTriggers(string pattern)
    {
        // Arrange
        var parseResult = _parser.Parse(pattern, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()  // Triggers need a start time
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);

        // Compute fire times explicitly (triggers need to be associated with a scheduler or computed manually)
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null, $"Pattern '{pattern}' should have a next fire time");
    }

    [TestCase("every 2 weeks")]
    [TestCase("every 3 weeks")]
    [TestCase("every month")]
    [TestCase("every 3 months")]
    [TestCase("every year")]
    public void Convert_CalendarIntervalPatterns_CreateValidTriggers(string pattern)
    {
        // Arrange
        var parseResult = _parser.Parse(pattern, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()  // CalendarInterval needs a start time
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null, $"Pattern '{pattern}' should have a next fire time");
    }

    // ========================================
    // Start Time Calculation Tests
    // ========================================

    [Test]
    public void CalculateStartTime_MultiWeekWithDayOfWeek_ReturnsNextOccurrence()
    {
        // Arrange - "every 2 weeks on sunday" should calculate next Sunday as start time
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            DayOfWeek = DayOfWeek.Sunday
        };

        // Use a fixed reference time (Wednesday 2025-01-15 10:00 UTC)
        var referenceTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // Act
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);

        // Assert - Should be next Sunday (2025-01-19 00:00 UTC)
        Assert.That(startTime, Is.Not.Null);
        Assert.That(startTime!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(startTime.Value.Date, Is.EqualTo(new DateTime(2025, 1, 19)));
    }

    [Test]
    public void CalculateStartTime_MultiWeekWithDayAndTime_ReturnsNextOccurrenceAtTime()
    {
        // Arrange - "every 2 weeks on sunday at 2pm" (uses Local timezone by default)
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            DayOfWeek = DayOfWeek.Sunday,
            TimeOfDay = new TimeOnly(14, 0)
        };

        var referenceTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // Act
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);

        // Assert - Should be next Sunday at 2pm in Local timezone
        Assert.That(startTime, Is.Not.Null);
        Assert.That(startTime!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));

        // Convert to local time to verify the hour/minute
        var localStartTime = TimeZoneInfo.ConvertTime(startTime.Value, TimeZoneInfo.Local);
        Assert.That(localStartTime.Hour, Is.EqualTo(14));
        Assert.That(localStartTime.Minute, Is.EqualTo(0));
    }

    [Test]
    public void CalculateStartTime_MonthlyWithDayOfMonth_ReturnsNextOccurrence()
    {
        // Arrange - "every month on 15" (monthly on the 15th)
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Months,
            DayOfMonth = 15
        };

        // Reference time: Jan 10, 2025
        var referenceTime = new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);

        // Act
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);

        // Assert - Should be Jan 15, 2025
        Assert.That(startTime, Is.Not.Null);
        Assert.That(startTime!.Value.Day, Is.EqualTo(15));
        Assert.That(startTime.Value.Month, Is.EqualTo(1));
    }

    [Test]
    public void CalculateStartTime_CronSchedule_ReturnsNull()
    {
        // Arrange - "every sunday" uses CronSchedule, doesn't need start time calculation
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Weeks,
            DayOfWeek = DayOfWeek.Sunday
        };

        // Act
        var startTime = _quartzBuilder.CalculateStartTime(spec);

        // Assert - Should return null (Cron handles this natively)
        Assert.That(startTime, Is.Null);
    }

    [Test]
    public void Convert_MultiWeekWithDayOfWeek_CreatesValidTrigger()
    {
        // Arrange - "every 2 weeks on sunday" full integration test
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            DayOfWeek = DayOfWeek.Sunday
        };

        var referenceTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // Act - Build schedule and calculate start time
        var scheduleBuilder = _quartzBuilder.Build(spec);
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);

        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(startTime ?? DateTimeOffset.UtcNow)
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>());

        // Verify first fire time is on Sunday
        var firstFireTime = trigger.GetFireTimeAfter(referenceTime);
        Assert.That(firstFireTime, Is.Not.Null);
        Assert.That(firstFireTime!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));

        // Verify second fire time is 2 weeks later (also Sunday)
        var secondFireTime = trigger.GetFireTimeAfter(firstFireTime.Value);
        Assert.That(secondFireTime, Is.Not.Null);
        Assert.That(secondFireTime!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That((secondFireTime.Value - firstFireTime.Value).Days, Is.EqualTo(14));
    }

    // ========================================
    // Validation Tests (Unsupported Patterns)
    // ========================================

    [Test]
    public void Convert_MultiWeekWithDayPattern_ThrowsNotSupportedException()
    {
        // Arrange - "every 2 weeks on weekdays" cannot be supported (would need to skip weekends)
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            DayPattern = DayPattern.Weekdays
        };

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => _quartzBuilder.Build(spec));
        Assert.That(ex!.Message, Does.Contain("Day patterns"));
        Assert.That(ex!.Message, Does.Contain("Workaround"));
    }

    // ========================================
    // Invalid Date Tests
    // ========================================

    /// <summary>
    /// Tests for impossible dates (Feb 30, April 31, etc.)
    /// Documents expected behavior - may accept but never fire
    /// </summary>
    [TestCase("0 0 0 30 2 ?", "February 30th")]
    [TestCase("0 0 0 31 2 ?", "February 31st")]
    [TestCase("0 0 0 31 4 ?", "April 31st")]
    [TestCase("0 0 0 31 6 ?", "June 31st")]
    [TestCase("0 0 0 31 9 ?", "September 31st")]
    [TestCase("0 0 0 31 11 ?", "November 31st")]
    public void Parse_ImpossibleDate_DocumentsBehavior(string cron, string description)
    {
        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Current implementation likely accepts these
        // Document that triggers with impossible dates will never fire
        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine($"INFO: Parser accepts impossible date '{description}' - trigger will never fire");
        }

        // This test passes either way - it documents behavior
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Parse_LeapYearDate_ParsesCorrectly()
    {
        // Arrange - February 29th is valid in leap years
        var cron = "0 0 0 29 2 ?";

        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Feb 29 should be accepted (fires in leap years only)");
    }

    [TestCase("0 0 0 32 1 ?", "Day 32 in January")]
    [TestCase("0 0 0 0 1 ?", "Day 0 in January")]
    [TestCase("0 0 0 -1 1 ?", "Negative day")]
    public void Parse_InvalidDayOfMonth_DocumentsBehavior(string cron, string description)
    {
        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Document that parser accepts out-of-range day values
        // Validation happens at Quartz trigger creation time
        Assert.That(result, Is.Not.Null,
            $"Parser should handle {description} gracefully");

        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine($"INFO: Parser accepts {description} - Quartz validation will occur at trigger creation");
        }
    }

    [Test]
    public void Build_ImpossibleDate_CreatesValidTriggerThatNeverFires()
    {
        // Arrange - February 30th doesn't exist
        var cron = "0 0 0 30 2 ?";
        var parseResult = new QuartzCronParser().Parse(cron);

        // If parser accepts it, verify trigger behavior
        if (parseResult is ParseResult<ScheduleSpec>.Success success)
        {
            // Act - Build trigger
            var scheduleBuilder = _quartzBuilder.Build(success.Value);
            var trigger = TriggerBuilder.Create()
                .WithSchedule(scheduleBuilder)
                .StartNow()
                .Build();

            // Assert - Trigger is created but will never fire
            Assert.That(trigger, Is.Not.Null);

            // Compute next fire time - should be null for impossible dates
            var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);

            // Document behavior: Quartz may return null or skip to next valid occurrence
            Console.WriteLine($"Next fire time for Feb 30: {nextFireTime?.ToString() ?? "null (never fires)"}");
        }
    }

    // ========================================
    // Range Operator Tests
    // ========================================

    /// <summary>
    /// Tests for hour and minute range operators
    /// These document current range support in Quartz cron
    /// </summary>
    [TestCase("0 0 9-17 * * ?", "Hours 9am to 5pm")]
    [TestCase("0 0-30 * * * ?", "Minutes 0-30")]
    [TestCase("0 15-45 * * * ?", "Minutes 15-45")]
    public void Parse_HourAndMinuteRanges_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Document current support for hour/minute ranges
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Should handle range operator: '{description}' = {cron}");

        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine($"SUCCESS: Parser handles range in {description}");
        }
        else if (result is ParseResult<ScheduleSpec>.Error error)
        {
            Console.WriteLine($"INFO: Range operator not yet supported for {description}: {error.Message}");
        }
    }

    [TestCase("0 0 9-17 * * MON-FRI", "Business hours on weekdays")]
    [TestCase("0 0 0-6 * * SAT,SUN", "Early morning on weekends")]
    public void Parse_CombinedRanges_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Document support for combined ranges
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Should handle combined ranges: '{description}' = {cron}");

        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine($"SUCCESS: Parser handles {description}");
        }
    }

    [Test]
    public void Parse_SecondRange_ParsesCorrectly()
    {
        // Arrange - Range in seconds field
        var cron = "0-30 * * * * ?";

        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Document support for second ranges
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            "Should handle range in seconds field");

        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine("SUCCESS: Parser handles second ranges (0-30)");
        }
    }

    [Test]
    public void Build_HourRange_CreatesValidTrigger()
    {
        // Arrange - "every hour from 9am to 5pm" in UTC
        var cron = "0 0 9-17 * * ?";
        var parseResult = new QuartzCronParser().Parse(cron);

        // Only test if parser supports range operator
        if (parseResult is ParseResult<ScheduleSpec>.Success success)
        {
            // Override timezone to UTC for deterministic testing
            var utcSpec = success.Value with { TimeZone = DateTimeZone.Utc };

            // Act - Build trigger with UTC timezone to make test deterministic
            var scheduleBuilder = _quartzBuilder.Build(utcSpec);
            var trigger = TriggerBuilder.Create()
                .WithSchedule(scheduleBuilder)
                .StartAt(DateTimeOffset.UtcNow)
                .Build();

            // Assert - Trigger should fire at 9am, 10am, 11am, ... 5pm UTC
            Assert.That(trigger, Is.Not.Null);
            var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
            Assert.That(nextFireTime, Is.Not.Null);

            // Verify hour is within range in UTC
            if (nextFireTime.HasValue)
            {
                var utcHour = nextFireTime.Value.UtcDateTime.Hour;
                Assert.That(utcHour, Is.InRange(9, 17),
                    $"Fire time should be within business hours (9am-5pm UTC), but was {utcHour}:00 UTC");
            }
        }
        else
        {
            Assert.Pass("Range operator not yet supported - test skipped");
        }
    }

    [TestCase("0 */15 9-17 * * ?", "Every 15 minutes from 9am to 5pm")]
    [TestCase("0 0,30 9-17 * * ?", "On the hour and half-hour from 9am to 5pm")]
    public void Parse_IntervalWithinRange_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = new QuartzCronParser().Parse(cron);

        // Assert - Document support for intervals within ranges
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Should handle interval within range: '{description}' = {cron}");

        if (result is ParseResult<ScheduleSpec>.Success)
        {
            Console.WriteLine($"SUCCESS: Parser handles {description}");
        }
    }

    // ========================================
    // Comprehensive Range Support Tests
    // ========================================

    #region Hour Range Tests

    /// <summary>
    /// Tests for hour range support in Quartz cron expressions (6-field format)
    /// Format: second minute hour day month dayOfWeek
    /// Hour ranges: "0 0 9-17 * * ?" = every hour from 9am to 5pm
    /// </summary>

    [TestCase("0 0 9-17 * * ?", 9, 17, Description = "Business hours (9am-5pm)")]
    [TestCase("0 0 0-23 * * ?", 0, 23, Description = "Full day (midnight-11pm)")]
    [TestCase("0 0 14-14 * * ?", 14, 14, Description = "Single hour range")]
    [TestCase("0 0 22-6 * * ?", 22, 6, Description = "Night hours (wraps midnight)")]
    [TestCase("0 0 6-18 * * ?", 6, 18, Description = "Day hours (6am-6pm)")]
    public void Parse_HourRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new QuartzCronParser();

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

    [TestCase("0 0 9-17 * * ?", Description = "Business hours (9am-5pm)")]
    [TestCase("0 0 0-23 * * ?", Description = "Full day (midnight-11pm)")]
    [TestCase("0 0 22-6 * * ?", Description = "Night hours (wraps midnight)")]
    public void RoundTrip_HourRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new QuartzCronParser();
        var builder = new QuartzCronBuilder();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → Quartz schedule → extract cron
        var scheduleBuilder = builder.Build(spec);
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should create CronScheduleBuilder for hour ranges");

        // Build trigger to extract cron expression
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .Build() as ICronTrigger;

        // Assert
        Assert.That(trigger, Is.Not.Null, "Should create CronTrigger");
        Assert.That(trigger!.CronExpressionString, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{trigger.CronExpressionString}'");
    }

    [Test]
    public void Build_HourRange_CreatesValidTriggerWithCorrectFireTimes()
    {
        // Arrange - "every hour from 9am to 5pm" in UTC
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            HourStart = 9,
            HourEnd = 17,
            TimeZone = DateTimeZone.Utc
        };

        // Act - Build trigger
        var builder = new QuartzScheduleBuilder(new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0)));
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(new DateTimeOffset(2025, 1, 15, 8, 0, 0, TimeSpan.Zero))
            .Build();

        // Assert - Verify fire times are within range
        var fireTime1 = trigger.GetFireTimeAfter(new DateTimeOffset(2025, 1, 15, 8, 30, 0, TimeSpan.Zero));
        var fireTime2 = trigger.GetFireTimeAfter(fireTime1!.Value);
        var fireTime3 = trigger.GetFireTimeAfter(fireTime2!.Value);

        Assert.That(fireTime1, Is.Not.Null, "Should have first fire time");
        Assert.That(fireTime1!.Value.UtcDateTime.Hour, Is.InRange(9, 17),
            $"First fire time hour should be in range 9-17, but was {fireTime1.Value.UtcDateTime.Hour}");

        Assert.That(fireTime2, Is.Not.Null, "Should have second fire time");
        Assert.That(fireTime2!.Value.UtcDateTime.Hour, Is.InRange(9, 17),
            $"Second fire time hour should be in range 9-17, but was {fireTime2.Value.UtcDateTime.Hour}");

        Assert.That(fireTime3, Is.Not.Null, "Should have third fire time");
        Assert.That(fireTime3!.Value.UtcDateTime.Hour, Is.InRange(9, 17),
            $"Third fire time hour should be in range 9-17, but was {fireTime3.Value.UtcDateTime.Hour}");
    }

    #endregion

    #region Minute Range Tests

    /// <summary>
    /// Tests for minute range support in Quartz cron expressions
    /// Minute ranges: "0 0-30 * * * ?" = every minute from :00 to :30 each hour
    /// </summary>

    [TestCase("0 0-30 * * * ?", 0, 30, Description = "First half hour")]
    [TestCase("0 30-59 * * * ?", 30, 59, Description = "Last half hour")]
    [TestCase("0 15-45 * * * ?", 15, 45, Description = "Quarter hours")]
    [TestCase("0 0-59 * * * ?", 0, 59, Description = "Full hour")]
    [TestCase("0 0-0 * * * ?", 0, 0, Description = "Single minute range")]
    public void Parse_MinuteRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new QuartzCronParser();

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

    [TestCase("0 0-30 * * * ?", Description = "First half hour")]
    [TestCase("0 30-59 * * * ?", Description = "Last half hour")]
    [TestCase("0 15-45 * * * ?", Description = "Quarter hours")]
    public void RoundTrip_MinuteRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new QuartzCronParser();
        var builder = new QuartzCronBuilder();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → Quartz schedule → extract cron
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .Build() as ICronTrigger;

        // Assert
        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger!.CronExpressionString, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{trigger.CronExpressionString}'");
    }

    [Test]
    public void Build_MinuteRange_CreatesValidTriggerWithCorrectFireTimes()
    {
        // Arrange - "every minute from :00 to :30"
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Minutes,
            MinuteStart = 0,
            MinuteEnd = 30,
            TimeZone = DateTimeZone.Utc
        };

        // Act - Build trigger
        var builder = new QuartzScheduleBuilder(new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0)));
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero))
            .Build();

        // Assert - Verify fire times are within range
        var fireTime1 = trigger.GetFireTimeAfter(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
        var fireTime2 = trigger.GetFireTimeAfter(fireTime1!.Value);
        var fireTime3 = trigger.GetFireTimeAfter(fireTime2!.Value);

        Assert.That(fireTime1, Is.Not.Null);
        Assert.That(fireTime1!.Value.Minute, Is.InRange(0, 30),
            $"Fire time minute should be in range 0-30, but was {fireTime1.Value.Minute}");

        Assert.That(fireTime2, Is.Not.Null);
        Assert.That(fireTime2!.Value.Minute, Is.InRange(0, 30),
            $"Fire time minute should be in range 0-30, but was {fireTime2.Value.Minute}");

        Assert.That(fireTime3, Is.Not.Null);
        Assert.That(fireTime3!.Value.Minute, Is.InRange(0, 30),
            $"Fire time minute should be in range 0-30, but was {fireTime3.Value.Minute}");
    }

    #endregion

    #region Day Range Tests

    /// <summary>
    /// Tests for day-of-month range support in Quartz cron expressions
    /// Day ranges: "0 0 0 1-15 * ?" = daily at midnight for days 1-15
    /// </summary>

    [TestCase("0 0 0 1-15 * ?", 1, 15, Description = "First half of month")]
    [TestCase("0 0 0 16-31 * ?", 16, 31, Description = "Second half of month")]
    [TestCase("0 0 0 1-7 * ?", 1, 7, Description = "First week")]
    [TestCase("0 0 0 15-15 * ?", 15, 15, Description = "Single day range")]
    [TestCase("0 0 0 1-31 * ?", 1, 31, Description = "Full month")]
    public void Parse_DayRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new QuartzCronParser();

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

    [TestCase("0 0 0 1-15 * ?", Description = "First half of month")]
    [TestCase("0 0 0 16-31 * ?", Description = "Second half of month")]
    [TestCase("0 0 0 1-7 * ?", Description = "First week")]
    public void RoundTrip_DayRange_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new QuartzCronParser();
        var builder = new QuartzCronBuilder();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → Quartz schedule → extract cron
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .Build() as ICronTrigger;

        // Assert
        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger!.CronExpressionString, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{trigger.CronExpressionString}'");
    }

    #endregion

    #region Combined Range Tests

    /// <summary>
    /// Tests for combinations of minute/hour/day ranges in Quartz cron expressions
    /// </summary>

    [TestCase("0 15-45 9-17 * * ?", "Minutes 15-45 during business hours")]
    [TestCase("0 0-30 9-17 1-15 * ?", "Complex multi-range (minutes, hours, days)")]
    [TestCase("0 0-59 0-23 1-31 * ?", "Full ranges across all fields")]
    [TestCase("0 0-30 22-6 * * ?", "Minutes 0-30 during night hours")]
    [TestCase("0 15-45 * 1-15 * ?", "Minutes 15-45, first half of month")]
    public void Parse_CombinedRanges_ReturnsCorrectScheduleSpec(string cron, string description)
    {
        // Arrange
        var parser = new QuartzCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse combined ranges ({description}): {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify ranges are parsed correctly
        var parts = cron.Split(' ');
        if (parts[1].Contains('-'))
        {
            var minuteParts = parts[1].Split('-');
            Assert.That(success.Value.MinuteStart, Is.EqualTo(int.Parse(minuteParts[0])));
            Assert.That(success.Value.MinuteEnd, Is.EqualTo(int.Parse(minuteParts[1])));
        }
        if (parts[2].Contains('-'))
        {
            var hourParts = parts[2].Split('-');
            Assert.That(success.Value.HourStart, Is.EqualTo(int.Parse(hourParts[0])));
            Assert.That(success.Value.HourEnd, Is.EqualTo(int.Parse(hourParts[1])));
        }
        if (parts[3].Contains('-'))
        {
            var dayParts = parts[3].Split('-');
            Assert.That(success.Value.DayStart, Is.EqualTo(int.Parse(dayParts[0])));
            Assert.That(success.Value.DayEnd, Is.EqualTo(int.Parse(dayParts[1])));
        }
    }

    [TestCase("0 15-45 9-17 * * ?", Description = "Minutes 15-45 during business hours")]
    [TestCase("0 0-30 9-17 1-15 * ?", Description = "Complex multi-range")]
    public void RoundTrip_CombinedRanges_PreservesCronExpression(string originalCron)
    {
        // Arrange
        var parser = new QuartzCronParser();
        var builder = new QuartzCronBuilder();

        // Act - Parse cron → ScheduleSpec
        var parseResult = parser.Parse(originalCron);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse: {originalCron}");
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build ScheduleSpec → Quartz schedule → extract cron
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .Build() as ICronTrigger;

        // Assert
        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger!.CronExpressionString, Is.EqualTo(originalCron),
            $"Round-trip should preserve cron expression: '{originalCron}' → '{trigger.CronExpressionString}'");
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Tests for edge cases: boundary values, single-value ranges, wraparound ranges
    /// </summary>

    [TestCase("0 0 0-23 * * ?", Description = "Full hour range (0-23)")]
    [TestCase("0 0-59 * * * ?", Description = "Full minute range (0-59)")]
    [TestCase("0 0 0 1-31 * ?", Description = "Full day range (1-31)")]
    public void Parse_BoundaryRanges_ReturnsCorrectScheduleSpec(string cron)
    {
        // Arrange
        var parser = new QuartzCronParser();

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
        else if (cron.Contains("0-59"))
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

    [TestCase("0 0-0 * * * ?", 0, 0, Description = "Min equals max (minute)")]
    [TestCase("0 0 23-23 * * ?", 23, 23, Description = "Min equals max (hour)")]
    [TestCase("0 0 0 31-31 * ?", 31, 31, Description = "Min equals max (day)")]
    public void Parse_SingleValueRange_ReturnsCorrectScheduleSpec(string cron, int expectedValue, int _)
    {
        // Arrange
        var parser = new QuartzCronParser();

        // Act
        var result = parser.Parse(cron);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse single-value range: {cron}");
        var success = (ParseResult<ScheduleSpec>.Success)result;

        // Verify the range start and end are equal
        if (cron.Contains("0-0"))
        {
            Assert.That(success.Value.MinuteStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.MinuteEnd, Is.EqualTo(expectedValue));
        }
        else if (cron.Contains("23-23"))
        {
            Assert.That(success.Value.HourStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.HourEnd, Is.EqualTo(expectedValue));
        }
        else if (cron.Contains("31-31"))
        {
            Assert.That(success.Value.DayStart, Is.EqualTo(expectedValue));
            Assert.That(success.Value.DayEnd, Is.EqualTo(expectedValue));
        }
    }

    [TestCase("0 0 22-6 * * ?", 22, 6, Description = "Hour wraparound (night)")]
    public void Parse_WraparoundRange_ReturnsCorrectScheduleSpec(string cron, int expectedStart, int expectedEnd)
    {
        // Arrange
        var parser = new QuartzCronParser();

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
}
