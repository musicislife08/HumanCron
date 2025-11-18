using HumanCron.Models;
using HumanCron.Quartz;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz.Converters;
using Quartz;

namespace HumanCron.Tests.Converters;

/// <summary>
/// Tests for QuartzScheduleConverter - the Quartz.NET extension API
/// Tests natural language ↔ Quartz IScheduleBuilder conversion
/// </summary>
[TestFixture]
public class QuartzScheduleConverterTests
{
    private IQuartzScheduleConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        // Use the factory method to create the converter
        _converter = QuartzScheduleConverterFactory.Create();
    }

    // ========================================
    // ToQuartzSchedule() - Natural Language → Quartz IScheduleBuilder
    // ========================================

    #region Simple Intervals (Cron-based)

    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every 6 hours")]
    [TestCase("every day")]
    [TestCase("every day at 2pm")]
    [TestCase("every monday at 9am")]
    public void ToQuartzSchedule_SimplePatterns_ReturnsCronScheduleBuilder(string natural)
    {
        // Act
        var result = _converter.ToQuartzSchedule(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>(),
            $"Failed to convert '{natural}' to Quartz schedule");
        var success = (ParseResult<IScheduleBuilder>.Success)result;
        Assert.That(success.Value, Is.TypeOf<CronScheduleBuilder>(),
            $"Expected CronScheduleBuilder for pattern '{natural}'");
    }

    [Test]
    public void ToQuartzSchedule_SimplePattern_CreatesValidTrigger()
    {
        // Arrange
        var natural = "every day at 2pm";

        // Act
        var result = _converter.ToQuartzSchedule(natural);
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build a trigger to verify it's valid
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null,
            "Trigger should have a valid next fire time");
    }

    #endregion

    #region Complex Intervals (CalendarInterval-based)

    [TestCase("every 2 weeks")]
    [TestCase("every 3 weeks")]
    [TestCase("every month")]
    [TestCase("every 3 months")]
    [TestCase("every year")]
    public void ToQuartzSchedule_CalendarIntervals_ReturnsCalendarIntervalScheduleBuilder(string natural)
    {
        // Act
        var result = _converter.ToQuartzSchedule(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>(),
            $"Failed to convert '{natural}' to Quartz schedule");
        var success = (ParseResult<IScheduleBuilder>.Success)result;
        Assert.That(success.Value, Is.TypeOf<CalendarIntervalScheduleBuilder>(),
            $"Expected CalendarIntervalScheduleBuilder for pattern '{natural}'");
    }

    [Test]
    public void ToQuartzSchedule_MultiWeekPattern_CreatesValidTrigger()
    {
        // Arrange
        var natural = "every 2 weeks on sunday at 3am";

        // Act
        var result = _converter.ToQuartzSchedule(natural);
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build a trigger (CalendarInterval schedules need a start time)
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null);
    }

    #endregion

    #region Invalid Input Tests

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ToQuartzSchedule_EmptyInput_ReturnsError(string? invalid)
    {
        // Act
        var result = _converter.ToQuartzSchedule(invalid!);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        var error = (ParseResult<IScheduleBuilder>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
    }

    [TestCase("invalid")]
    [TestCase("xyz")]
    [TestCase("999999999999999s")]
    public void ToQuartzSchedule_InvalidSyntax_ReturnsError(string invalid)
    {
        // Act
        var result = _converter.ToQuartzSchedule(invalid);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        var error = (ParseResult<IScheduleBuilder>.Error)result;
        Assert.That(error.Message, Is.Not.Empty);
    }

    #endregion

    // ========================================
    // ToNaturalLanguage() - Quartz IScheduleBuilder → Natural Language
    // ========================================

    #region Schedule Builder to Natural Language

    [Test]
    public void ToNaturalLanguage_CronScheduleBuilder_ReturnsNaturalExpression()
    {
        // Arrange - Create a daily at 2pm cron schedule
        var cronBuilder = CronScheduleBuilder.DailyAtHourAndMinute(14, 0);

        // Act
        var result = _converter.ToNaturalLanguage(cronBuilder);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Does.Contain("2pm"));
    }

    [Test]
    public void ToNaturalLanguage_NullScheduleBuilder_ReturnsError()
    {
        // Act
        var result = _converter.ToNaturalLanguage(null!);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        var error = (ParseResult<string>.Error)result;
        Assert.That(error.Message, Does.Contain("null").IgnoreCase);
    }

    #endregion

    // ========================================
    // Trigger Configuration Tests (Start Times)
    // ========================================

    #region Trigger Start Time Tests

    [Test]
    public void ToQuartzSchedule_BiWeeklyPattern_WorksWithStartNow()
    {
        // Arrange - Multi-week patterns
        var natural = "every 2 weeks on sunday at 3am";

        // Act
        var result = _converter.ToQuartzSchedule(natural);
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build trigger with StartNow (uses current time as reference)
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        Assert.That(scheduleBuilder, Is.TypeOf<CalendarIntervalScheduleBuilder>(),
            "Multi-week patterns should use CalendarIntervalScheduleBuilder");

        // Verify it produces valid fire times (CalendarInterval fires based on start time + interval)
        var firstFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(firstFireTime, Is.Not.Null,
            "Trigger should calculate next fire time");

        // Verify second fire is exactly 2 weeks later
        var secondFireTime = trigger.GetFireTimeAfter(firstFireTime!.Value);
        Assert.That(secondFireTime, Is.Not.Null);
        Assert.That((secondFireTime!.Value - firstFireTime.Value).Days, Is.EqualTo(14),
            "Bi-weekly pattern should fire every 14 days");
    }

    [Test]
    public void ToQuartzSchedule_BiWeeklyPattern_WorksWithManualStartTime()
    {
        // Arrange - Multi-week patterns with explicit start time
        var natural = "every 2 weeks on sunday at 3am";
        var explicitStartTime = new DateTimeOffset(2025, 1, 19, 3, 0, 0, TimeSpan.Zero); // Sunday, Jan 19, 2025 at 3am

        // Act
        var result = _converter.ToQuartzSchedule(natural);
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build trigger with explicit start time
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(explicitStartTime)
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);

        // First fire should be at or after the explicit start time
        var firstFireTime = trigger.GetFireTimeAfter(explicitStartTime.AddSeconds(-1));
        Assert.That(firstFireTime, Is.Not.Null);
        Assert.That(firstFireTime!.Value, Is.GreaterThanOrEqualTo(explicitStartTime),
            "First fire time should respect explicit start time");

        // Verify second fire is exactly 2 weeks later
        var secondFireTime = trigger.GetFireTimeAfter(firstFireTime.Value);
        Assert.That(secondFireTime, Is.Not.Null);
        Assert.That((secondFireTime!.Value - firstFireTime.Value).Days, Is.EqualTo(14),
            "Bi-weekly pattern should fire every 14 days");
    }

    [Test]
    public void ToQuartzSchedule_MonthlyPattern_WorksWithManualStartTime()
    {
        // Arrange - Monthly pattern (uses Local timezone by default)
        var natural = "every month at 9am";

        // Create start time in Local timezone (timezone-agnostic test)
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2025, 1, 15, 9, 0, 0));
        var explicitStartTime = new DateTimeOffset(2025, 1, 15, 9, 0, 0, localOffset); // Jan 15, 2025 at 9am Local

        // Act
        var result = _converter.ToQuartzSchedule(natural);
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build trigger with explicit start time
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(explicitStartTime)
            .Build();

        // Assert
        Assert.That(trigger, Is.Not.Null);
        var firstFireTime = trigger.GetFireTimeAfter(explicitStartTime.AddSeconds(-1));
        Assert.That(firstFireTime, Is.Not.Null);

        // Convert to local time to verify the hour/minute (timezone-agnostic)
        var localFireTime = TimeZoneInfo.ConvertTime(firstFireTime.Value, TimeZoneInfo.Local);
        Assert.That(localFireTime.Hour, Is.EqualTo(9));
        Assert.That(localFireTime.Minute, Is.EqualTo(0));
    }

    #endregion

    // ========================================
    // Round-Trip Tests
    // ========================================

    #region Round-Trip Validation

    [TestCase("every day at 2pm")]
    [TestCase("every monday at 9am")]
    [TestCase("every weekday at 9am")]
    public void RoundTrip_NaturalToQuartzToNatural_PreservesSemantics(string original)
    {
        // Act - Natural → Quartz
        var quartzResult = _converter.ToQuartzSchedule(original);
        Assert.That(quartzResult, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)quartzResult).Value;

        // Act - Quartz → Natural
        var naturalResult = _converter.ToNaturalLanguage(scheduleBuilder);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
        var natural = ((ParseResult<string>.Success)naturalResult).Value;

        // Assert - Verify semantic equivalence by converting back to Quartz
        var verifyResult = _converter.ToQuartzSchedule(natural);
        Assert.That(verifyResult, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>(),
            $"Round-trip produced invalid natural language: '{natural}'");
    }

    #endregion

    // ========================================
    // Integration Tests (End-to-End)
    // ========================================

    #region Integration Tests

    [Test]
    public void Integration_CompleteWorkflow_DailySchedule()
    {
        // Arrange
        var natural = "every day at 2pm";

        // Act - Convert to Quartz schedule
        var scheduleResult = _converter.ToQuartzSchedule(natural);
        Assert.That(scheduleResult, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)scheduleResult).Value;

        // Build trigger
        var trigger = TriggerBuilder.Create()
            .WithIdentity("test-trigger")
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Verify trigger is valid
        Assert.That(trigger, Is.Not.Null);
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFireTime, Is.Not.Null);

        // Convert back to natural language
        var naturalResult = _converter.ToNaturalLanguage(scheduleBuilder);
        Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
    }

    [Test]
    public void Integration_CompleteWorkflow_BiWeeklySchedule()
    {
        // Arrange - Multi-week patterns
        var natural = "every 2 weeks on sunday at 3am";

        // Act - Convert to Quartz schedule
        var scheduleResult = _converter.ToQuartzSchedule(natural);
        Assert.That(scheduleResult, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)scheduleResult).Value;

        // Build trigger with StartNow
        var trigger = TriggerBuilder.Create()
            .WithIdentity("test-biweekly-trigger")
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Verify trigger produces correct fire times
        Assert.That(trigger, Is.Not.Null);
        var firstFire = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(firstFire, Is.Not.Null,
            "Trigger should have a next fire time");

        // Verify second fire is 2 weeks later
        var secondFire = trigger.GetFireTimeAfter(firstFire!.Value);
        Assert.That(secondFire, Is.Not.Null);
        Assert.That((secondFire.Value - firstFire.Value).Days, Is.EqualTo(14),
            "Bi-weekly schedule should fire every 14 days");
    }

    #endregion
}
