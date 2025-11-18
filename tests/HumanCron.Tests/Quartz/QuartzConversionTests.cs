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
}
