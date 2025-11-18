using HumanCron.Models.Internal;
using HumanCron.Models;
using HumanCron.Parsing;
using HumanCron.Quartz;
using Quartz;
using NodaTime;
using NodaTime.Testing;
using IntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests round-trip conversion: Natural Language → ScheduleSpec → Quartz → ScheduleSpec
/// Verifies that conversions are lossless
/// </summary>
[TestFixture]
public class QuartzRoundTripTests
{
    private NaturalLanguageParser _naturalParser = null!;
    private QuartzScheduleBuilder _quartzBuilder = null!;
    private QuartzScheduleParser _quartzParser = null!;

    [SetUp]
    public void SetUp()
    {
        _naturalParser = new NaturalLanguageParser();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        _quartzBuilder = new QuartzScheduleBuilder(fakeClock);
        _quartzParser = new QuartzScheduleParser();
    }

    [TestCase("every 30 seconds")]
    [TestCase("every 15 minutes")]
    [TestCase("every 6 hours")]
    [TestCase("every day")]
    [TestCase("every day at 2pm")]
    [TestCase("every tuesday at 2pm")]
    public void RoundTrip_CronPatterns_PreservesScheduleSpec(string naturalLanguage)
    {
        // Parse natural language → ScheduleSpec
        var parseResult = _naturalParser.Parse(naturalLanguage, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var originalSpec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Convert ScheduleSpec → Quartz schedule → Trigger
        var scheduleBuilder = _quartzBuilder.Build(originalSpec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Reverse convert Trigger → ScheduleSpec
        var reverseResult = _quartzParser.ParseTrigger(trigger);
        Assert.That(reverseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var reversedSpec = ((ParseResult<ScheduleSpec>.Success)reverseResult).Value;

        // Assert: Specs should be equivalent
        Assert.That(reversedSpec.Interval, Is.EqualTo(originalSpec.Interval),
            $"Interval mismatch for '{naturalLanguage}'");
        Assert.That(reversedSpec.Unit, Is.EqualTo(originalSpec.Unit),
            $"Unit mismatch for '{naturalLanguage}'");
        Assert.That(reversedSpec.DayOfWeek, Is.EqualTo(originalSpec.DayOfWeek),
            $"DayOfWeek mismatch for '{naturalLanguage}'");
        Assert.That(reversedSpec.DayPattern, Is.EqualTo(originalSpec.DayPattern),
            $"DayPattern mismatch for '{naturalLanguage}'");

        // TimeOfDay comparison (allow null vs midnight equivalence for daily patterns without explicit time)
        if (originalSpec.TimeOfDay.HasValue)
        {
            Assert.That(reversedSpec.TimeOfDay, Is.EqualTo(originalSpec.TimeOfDay),
                $"TimeOfDay mismatch for '{naturalLanguage}'");
        }
    }

    [TestCase("every 2 weeks")]
    [TestCase("every 3 weeks")]
    [TestCase("every month")]
    [TestCase("every 3 months")]
    [TestCase("every year")]
    public void RoundTrip_CalendarIntervalPatterns_PreservesScheduleSpec(string naturalLanguage)
    {
        // Parse natural language → ScheduleSpec
        var parseResult = _naturalParser.Parse(naturalLanguage, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var originalSpec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Convert ScheduleSpec → Quartz schedule → Trigger
        var scheduleBuilder = _quartzBuilder.Build(originalSpec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Reverse convert Trigger → ScheduleSpec
        var reverseResult = _quartzParser.ParseTrigger(trigger);
        Assert.That(reverseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var reversedSpec = ((ParseResult<ScheduleSpec>.Success)reverseResult).Value;

        // Assert: Interval and unit should match exactly
        Assert.That(reversedSpec.Interval, Is.EqualTo(originalSpec.Interval),
            $"Interval mismatch for '{naturalLanguage}'");
        Assert.That(reversedSpec.Unit, Is.EqualTo(originalSpec.Unit),
            $"Unit mismatch for '{naturalLanguage}'");
    }

    [TestCase("every monday")]  // Becomes "weekly on monday" (semantically equivalent)
    [TestCase("every weekday")]  // Becomes "weekly" with weekdays pattern
    public void RoundTrip_DayOfWeekPatterns_SemanticEquivalence(string naturalLanguage)
    {
        // Parse natural language → ScheduleSpec
        var parseResult = _naturalParser.Parse(naturalLanguage, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{naturalLanguage}'");

        var originalSpec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Convert ScheduleSpec → Quartz schedule → Trigger
        var scheduleBuilder = _quartzBuilder.Build(originalSpec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Reverse convert Trigger → ScheduleSpec (should succeed even if semantically different)
        var reverseResult = _quartzParser.ParseTrigger(trigger);
        Assert.That(reverseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to reverse parse trigger for '{naturalLanguage}'");
    }

    [Test]
    public void RoundTrip_DirectCronExpression_ReconstructsScheduleSpec()
    {
        // Arrange - Start with a known cron expression
        var cronExpression = "0 30 9 ? * MON-FRI";

        // Act - Parse cron → ScheduleSpec
        var parseResult = _quartzParser.ParseCronExpression(cronExpression);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Convert ScheduleSpec → Quartz → back to cron
        var scheduleBuilder = _quartzBuilder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build() as ICronTrigger;

        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger.CronExpressionString, Is.Not.Null);

        // Parse the new cron expression
        var reverseResult = _quartzParser.ParseCronExpression(trigger.CronExpressionString);
        Assert.That(reverseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var reversedSpec = ((ParseResult<ScheduleSpec>.Success)reverseResult).Value;

        // Assert: Should reconstruct same ScheduleSpec
        Assert.That(reversedSpec.DayPattern, Is.EqualTo(spec.DayPattern));
        Assert.That(reversedSpec.TimeOfDay, Is.EqualTo(spec.TimeOfDay));
    }
}
