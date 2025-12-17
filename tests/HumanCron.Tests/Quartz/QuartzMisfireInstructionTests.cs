using HumanCron.Models;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz;
using Quartz;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests for Quartz misfire instruction support
/// Verifies that misfire policies are correctly applied to both CronScheduleBuilder
/// and CalendarIntervalScheduleBuilder via the public API
/// </summary>
[TestFixture]
public class QuartzMisfireInstructionTests
{
    private IQuartzScheduleConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = QuartzScheduleConverterFactory.Create();
    }

    #region ToQuartzSchedule - CronScheduleBuilder Tests

    [Test]
    public void ToQuartzSchedule_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        // Arrange - Daily schedule (uses CronScheduleBuilder)
        var natural = "every day at 2pm";

        // Act - Call with default misfire (0 = SmartPolicy)
        var result = _converter.ToQuartzSchedule(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        // Build trigger to verify misfire instruction
        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // SmartPolicy is the default (value 0)
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    [Test]
    public void ToQuartzSchedule_CronSchedule_DoNothing_AppliesCorrectly()
    {
        // Arrange
        var natural = "every hour";

        // Act - Apply DoNothing misfire instruction
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.CronTrigger.DoNothing);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.CronTrigger.DoNothing));
    }

    [Test]
    public void ToQuartzSchedule_CronSchedule_IgnoreMisfirePolicy_AppliesCorrectly()
    {
        // Arrange
        var natural = "every 30 minutes";

        // Act - Apply IgnoreMisfirePolicy
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.IgnoreMisfirePolicy);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
    }

    [Test]
    public void ToQuartzSchedule_CronSchedule_FireOnceNow_AppliesCorrectly()
    {
        // Arrange
        var natural = "every day at 9am";

        // Act - Apply FireOnceNow (FireAndProceed in Quartz terms)
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.CronTrigger.FireOnceNow);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.CronTrigger.FireOnceNow));
    }

    #endregion

    #region ToQuartzSchedule - CalendarIntervalScheduleBuilder Tests

    [Test]
    public void ToQuartzSchedule_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        // Arrange - Multi-week schedule (uses CalendarIntervalScheduleBuilder)
        var natural = "every 2 weeks";

        // Act - Call with default misfire
        var result = _converter.ToQuartzSchedule(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;

        // SmartPolicy is the default
        Assert.That(calendarTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    [Test]
    public void ToQuartzSchedule_CalendarInterval_DoNothing_AppliesCorrectly()
    {
        // Arrange
        var natural = "every 3 months";

        // Act
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.CalendarIntervalTrigger.DoNothing);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction,
            Is.EqualTo(MisfireInstruction.CalendarIntervalTrigger.DoNothing));
    }

    [Test]
    public void ToQuartzSchedule_CalendarInterval_IgnoreMisfirePolicy_AppliesCorrectly()
    {
        // Arrange
        var natural = "every year";

        // Act
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.IgnoreMisfirePolicy);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
    }

    [Test]
    public void ToQuartzSchedule_CalendarInterval_FireOnceNow_AppliesCorrectly()
    {
        // Arrange
        var natural = "every 2 weeks on sunday";

        // Act
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.CalendarIntervalTrigger.FireOnceNow);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction,
            Is.EqualTo(MisfireInstruction.CalendarIntervalTrigger.FireOnceNow));
    }

    #endregion

    #region CreateTriggerBuilder - CronScheduleBuilder Tests

    [Test]
    public void CreateTriggerBuilder_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        // Arrange
        var natural = "every day at 2pm";

        // Act - Create trigger with default misfire
        var result = _converter.CreateTriggerBuilder(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var triggerBuilder = ((ParseResult<TriggerBuilder>.Success)result).Value;
        var trigger = triggerBuilder.Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    [Test]
    public void CreateTriggerBuilder_CronSchedule_DoNothing_AppliesCorrectly()
    {
        // Arrange
        var natural = "every hour";

        // Act
        var result = _converter.CreateTriggerBuilder(natural, MisfireInstruction.CronTrigger.DoNothing);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.CronTrigger.DoNothing));
    }

    [Test]
    public void CreateTriggerBuilder_CronSchedule_IgnoreMisfirePolicy_AppliesCorrectly()
    {
        // Arrange
        var natural = "every 15 minutes";

        // Act
        var result = _converter.CreateTriggerBuilder(natural, MisfireInstruction.IgnoreMisfirePolicy);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
    }

    #endregion

    #region CreateTriggerBuilder - CalendarIntervalScheduleBuilder Tests

    [Test]
    public void CreateTriggerBuilder_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        // Arrange
        var natural = "every 2 weeks";

        // Act
        var result = _converter.CreateTriggerBuilder(natural);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    [Test]
    public void CreateTriggerBuilder_CalendarInterval_DoNothing_AppliesCorrectly()
    {
        // Arrange
        var natural = "every 3 months";

        // Act
        var result = _converter.CreateTriggerBuilder(natural, MisfireInstruction.CalendarIntervalTrigger.DoNothing);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction,
            Is.EqualTo(MisfireInstruction.CalendarIntervalTrigger.DoNothing));
    }

    [Test]
    public void CreateTriggerBuilder_CalendarInterval_IgnoreMisfirePolicy_AppliesCorrectly()
    {
        // Arrange
        var natural = "every year";

        // Act
        var result = _converter.CreateTriggerBuilder(natural, MisfireInstruction.IgnoreMisfirePolicy);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
    }

    #endregion

    #region Backwards Compatibility Tests

    [Test]
    public void ToQuartzSchedule_WithoutMisfireParameter_UsesDefaultSmartPolicy()
    {
        // Arrange
        var natural = "every day at 9am";

        // Act - Call without misfire parameter (backwards compatibility)
        var result = _converter.ToQuartzSchedule(natural);

        // Assert - Should use SmartPolicy (default)
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    [Test]
    public void CreateTriggerBuilder_WithoutMisfireParameter_UsesDefaultSmartPolicy()
    {
        // Arrange
        var natural = "every 2 weeks on monday";

        // Act - Call without misfire parameter (backwards compatibility)
        var result = _converter.CreateTriggerBuilder(natural);

        // Assert - Should use SmartPolicy (default)
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
    }

    #endregion

    #region Trigger Functionality Tests

    [TestCase("every day at 2pm", MisfireInstruction.CronTrigger.DoNothing)]
    [TestCase("every 2 weeks on monday", MisfireInstruction.CalendarIntervalTrigger.DoNothing)]
    [TestCase("every month at 9am", MisfireInstruction.IgnoreMisfirePolicy)]
    [TestCase("every hour", MisfireInstruction.CronTrigger.FireOnceNow)]
    public void CreateTriggerBuilder_VariousSchedulesAndMisfires_CreatesValidTrigger(
        string natural,
        int misfireInstruction)
    {
        // Act
        var result = _converter.CreateTriggerBuilder(natural, misfireInstruction);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        // Verify trigger can calculate next fire time
        var nextFire = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFire, Is.Not.Null,
            $"Trigger with pattern '{natural}' and misfire '{misfireInstruction}' should have valid next fire time");

        // Verify misfire instruction was applied
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(misfireInstruction),
            $"Misfire instruction should be {misfireInstruction}");
    }

    [TestCase("every day at 3pm", 0)] // SmartPolicy
    [TestCase("every week", -1)] // IgnoreMisfirePolicy
    [TestCase("every month", 1)] // FireOnceNow
    [TestCase("every year", 2)] // DoNothing
    public void ToQuartzSchedule_WithRawIntValues_AppliesCorrectly(string natural, int misfireValue)
    {
        // Act
        var result = _converter.ToQuartzSchedule(natural, misfireValue);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger.MisfireInstruction, Is.EqualTo(misfireValue),
            $"Misfire instruction should match raw value {misfireValue}");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void ToQuartzSchedule_WithInvalidMisfireValue_ReturnsError()
    {
        // Arrange
        var natural = "every hour";
        var invalidMisfire = 999; // Invalid value

        // Act
        var result = _converter.ToQuartzSchedule(natural, invalidMisfire);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        var error = (ParseResult<IScheduleBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("misfire").IgnoreCase);
        Assert.That(error.Message, Does.Contain("999"));
    }

    [Test]
    public void CreateTriggerBuilder_WithInvalidMisfireValue_ReturnsError()
    {
        // Arrange
        var natural = "every day at 2pm";
        var invalidMisfire = 999; // Invalid value

        // Act
        var result = _converter.CreateTriggerBuilder(natural, invalidMisfire);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
        var error = (ParseResult<TriggerBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("misfire").IgnoreCase);
        Assert.That(error.Message, Does.Contain("999"));
    }

    [Test]
    public void ToQuartzSchedule_ComplexScheduleWithMisfire_WorksCorrectly()
    {
        // Arrange - Complex schedule with day-of-week constraint and misfire
        var natural = "every 2 weeks on sunday at 2pm";

        // Act
        var result = _converter.ToQuartzSchedule(natural, MisfireInstruction.CalendarIntervalTrigger.DoNothing);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        // Verify it's a calendar interval trigger with correct misfire
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(trigger.MisfireInstruction,
            Is.EqualTo(MisfireInstruction.CalendarIntervalTrigger.DoNothing));

        // Verify it can still calculate next fire time correctly
        var nextFire = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
        Assert.That(nextFire, Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void CreateTriggerBuilder_WithInvalidInput_ReturnsError(string? invalidInput)
    {
        // Act
        var result = _converter.CreateTriggerBuilder(invalidInput!, MisfireInstruction.CronTrigger.DoNothing);

        // Assert - Should propagate validation error from ToQuartzSchedule
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
        var error = (ParseResult<TriggerBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_WithInputExceedingMaxLength_ReturnsError()
    {
        // Arrange - Input exceeding 1000 character limit
        var tooLong = new string('a', 1001);

        // Act
        var result = _converter.CreateTriggerBuilder(tooLong, MisfireInstruction.CronTrigger.DoNothing);

        // Assert - Should propagate validation error from ToQuartzSchedule
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
        var error = (ParseResult<TriggerBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("maximum length").IgnoreCase);
    }

    [TestCase(-2)]
    [TestCase(-999)]
    [TestCase(int.MinValue)]
    public void ToQuartzSchedule_WithInvalidNegativeMisfireValue_ReturnsError(int invalidMisfire)
    {
        // Arrange
        var natural = "every day at 2pm";

        // Act
        var result = _converter.ToQuartzSchedule(natural, invalidMisfire);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        var error = (ParseResult<IScheduleBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("misfire").IgnoreCase);
        Assert.That(error.Message, Does.Contain(invalidMisfire.ToString()));
    }

    [TestCase(int.MaxValue)]
    [TestCase(1000)]
    [TestCase(100)]
    public void CreateTriggerBuilder_WithInvalidPositiveMisfireValue_ReturnsError(int invalidMisfire)
    {
        // Arrange
        var natural = "every hour";

        // Act
        var result = _converter.CreateTriggerBuilder(natural, invalidMisfire);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
        var error = (ParseResult<TriggerBuilder>.Error)result;
        Assert.That(error.Message, Does.Contain("misfire").IgnoreCase);
        Assert.That(error.Message, Does.Contain(invalidMisfire.ToString()));
    }

    [Test]
    public void ToQuartzSchedule_WithExplicitZeroMisfire_UsesSmartPolicy()
    {
        // Arrange
        var natural = "every day at 10am";

        // Act - Explicitly pass 0 (vs relying on default parameter)
        var result = _converter.ToQuartzSchedule(natural, 0);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        var scheduleBuilder = ((ParseResult<IScheduleBuilder>.Success)result).Value;

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test")
            .WithSchedule(scheduleBuilder)
            .Build();

        Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy),
            "Explicit zero value should use SmartPolicy (same as default)");
    }

    [Test]
    public void ToQuartzSchedule_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        // Arrange - Invalid schedule pattern but valid misfire value
        var invalidSchedule = "every 999 potato chips";
        var validMisfire = MisfireInstruction.CronTrigger.DoNothing;

        // Act
        var result = _converter.ToQuartzSchedule(invalidSchedule, validMisfire);

        // Assert - Should fail during schedule parsing, not misfire application
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        var error = (ParseResult<IScheduleBuilder>.Error)result;
        // Error should be about parsing, not misfire
        Assert.That(error.Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_WithExplicitZeroMisfire_UsesSmartPolicy()
    {
        // Arrange
        var natural = "every week at 5pm";

        // Act - Explicitly pass 0 (vs relying on default parameter)
        var result = _converter.CreateTriggerBuilder(natural, 0);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
        var trigger = ((ParseResult<TriggerBuilder>.Success)result).Value.Build();

        Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy),
            "Explicit zero value should use SmartPolicy (same as default)");
    }

    [Test]
    public void CreateTriggerBuilder_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        // Arrange - Invalid schedule pattern but valid misfire value
        var invalidSchedule = "every 42 bananas at midnight";
        var validMisfire = MisfireInstruction.IgnoreMisfirePolicy;

        // Act
        var result = _converter.CreateTriggerBuilder(invalidSchedule, validMisfire);

        // Assert - Should fail during schedule parsing (propagated from ToQuartzSchedule)
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
        var error = (ParseResult<TriggerBuilder>.Error)result;
        // Error should be about parsing, not misfire
        Assert.That(error.Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    #endregion
}
