using HumanCron.Models;
using HumanCron.Quartz;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz.Helpers;
using Quartz;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests for Quartz misfire instruction support.
/// Recurring schedules accept <see cref="CronTriggerMisfireInstruction"/> and apply it to whichever
/// trigger family (cron or calendar-interval) the parse produces. One-time triggers take
/// <see cref="SimpleTriggerMisfireInstruction"/> directly because that family is fixed.
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

    private static ITrigger BuildTrigger(IScheduleBuilder scheduleBuilder) =>
        TriggerBuilder.Create().WithIdentity("test").WithSchedule(scheduleBuilder).Build();

    private static IScheduleBuilder SuccessSchedule(ParseResult<IScheduleBuilder> result)
    {
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        return ((ParseResult<IScheduleBuilder>.Success)result).Value;
    }

    private static ITrigger SuccessTrigger(ParseResult<TriggerBuilder<IJob>> result)
    {
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Success>());
        return ((ParseResult<TriggerBuilder<IJob>>.Success)result).Value.Build();
    }

    #region ToQuartzSchedule - cron family

    [Test]
    public void ToQuartzSchedule_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule("every day at 2pm")));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(CronTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every hour", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every 30 minutes", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every day at 9am", CronTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every day at 10am", CronTriggerMisfireInstruction.SmartPolicy)]
    public void ToQuartzSchedule_CronSchedule_AppliesInstruction(string natural, CronTriggerMisfireInstruction instruction)
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule(natural, instruction)));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(instruction));
    }

    #endregion

    #region ToQuartzSchedule - calendar-interval family

    [Test]
    public void ToQuartzSchedule_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule("every 2 weeks")));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction,
            Is.EqualTo(CalendarIntervalTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every 3 months", CronTriggerMisfireInstruction.DoNothing, CalendarIntervalTriggerMisfireInstruction.DoNothing)]
    [TestCase("every year", CronTriggerMisfireInstruction.IgnoreMisfires, CalendarIntervalTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every 2 weeks on sunday", CronTriggerMisfireInstruction.FireAndProceed, CalendarIntervalTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every 2 weeks on sunday at 2pm", CronTriggerMisfireInstruction.DoNothing, CalendarIntervalTriggerMisfireInstruction.DoNothing)]
    public void ToQuartzSchedule_CalendarInterval_MapsCronInstructionToCalendarFamily(
        string natural,
        CronTriggerMisfireInstruction given,
        CalendarIntervalTriggerMisfireInstruction expected)
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule(natural, given)));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction, Is.EqualTo(expected));
        Assert.That(trigger.GetFireTimeAfter(DateTimeOffset.UtcNow), Is.Not.Null);
    }

    #endregion

    #region CreateTriggerBuilder

    [Test]
    public void CreateTriggerBuilder_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder("every day at 2pm"));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(CronTriggerMisfireInstruction.SmartPolicy));
    }

    [Test]
    public void CreateTriggerBuilder_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder("every 2 weeks on monday"));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction,
            Is.EqualTo(CalendarIntervalTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every hour", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every 15 minutes", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every day at 2pm", CronTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every week at 5pm", CronTriggerMisfireInstruction.SmartPolicy)]
    [TestCase("every 3 months", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every year", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every 2 weeks on monday", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every month at 9am", CronTriggerMisfireInstruction.IgnoreMisfires)]
    public void CreateTriggerBuilder_AppliesInstructionCodeToResultingFamily(
        string natural,
        CronTriggerMisfireInstruction instruction)
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder(natural, instruction));

        Assert.That(trigger.MisfireInstructionCode, Is.EqualTo((int)instruction));
        Assert.That(trigger.GetFireTimeAfter(DateTimeOffset.UtcNow), Is.Not.Null,
            $"Trigger for '{natural}' with {instruction} should have a next fire time");
    }

    #endregion

    #region Input validation still runs before misfire handling

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void CreateTriggerBuilder_WithInvalidInput_ReturnsError(string? invalidInput)
    {
        var result = _converter.CreateTriggerBuilder(invalidInput!, CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Contain("empty").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_WithInputExceedingMaxLength_ReturnsError()
    {
        var result = _converter.CreateTriggerBuilder(new string('a', 1001), CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Contain("maximum length").IgnoreCase);
    }

    [Test]
    public void ToQuartzSchedule_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        var result = _converter.ToQuartzSchedule("every 999 potato chips", CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        Assert.That(((ParseResult<IScheduleBuilder>.Error)result).Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        var result = _converter.CreateTriggerBuilder("every 42 bananas at midnight", CronTriggerMisfireInstruction.IgnoreMisfires);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    #endregion

    #region SimpleScheduleBuilder (one-time trigger)

    [TestCase(SimpleTriggerMisfireInstruction.SmartPolicy)]
    [TestCase(SimpleTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase(SimpleTriggerMisfireInstruction.FireNow)]
    [TestCase(SimpleTriggerMisfireInstruction.NowWithExistingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NowWithRemainingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NextWithRemainingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NextWithExistingCount)]
    public void ApplyMisfireInstruction_SimpleSchedule_AppliesInstruction(SimpleTriggerMisfireInstruction instruction)
    {
        var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

        var result = MisfireInstructionHelper.ApplyMisfireInstruction(builder, instruction);

        var trigger = (ISimpleTrigger)TriggerBuilder.Create().WithSchedule(result).Build();
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(instruction));
    }

    #endregion
}
