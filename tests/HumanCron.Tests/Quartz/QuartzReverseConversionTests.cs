using HumanCron.Models.Internal;
using HumanCron.Models;
using HumanCron.Quartz;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests reverse conversion: Quartz cron expressions → ScheduleSpec
/// </summary>
[TestFixture]
public class QuartzReverseConversionTests
{
    private QuartzScheduleParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new QuartzScheduleParser();
    }

    [TestCase("*/30 * * * * ?")]
    [TestCase("0 */15 * * * ?")]
    [TestCase("0 0 */6 * * ?")]
    [TestCase("0 0 14 * * ?")]
    [TestCase("0 0 14 */2 * ?")]
    public void ParseCronExpression_SimpleIntervals_ParseCorrectly(string cronExpression)
    {
        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse cron expression '{cronExpression}'");
    }

    [Test]
    public void ParseCronExpression_DailyAt2pm_ParsesTimeCorrectly()
    {
        // Arrange
        var cronExpression = "0 0 14 * * ?";

        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;

        Assert.That(spec.Interval, Is.EqualTo(1));
        Assert.That(spec.Unit, Is.EqualTo(IntervalUnit.Days));
        Assert.That(spec.TimeOfDay, Is.EqualTo(new TimeOnly(14, 0)));
    }

    [Test]
    public void ParseCronExpression_WeeklyOnMonday_ParsesDayOfWeek()
    {
        // Arrange
        var cronExpression = "0 0 14 ? * MON";

        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;

        Assert.That(spec.Interval, Is.EqualTo(1));
        Assert.That(spec.Unit, Is.EqualTo(IntervalUnit.Weeks));
        Assert.That(spec.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(spec.TimeOfDay, Is.EqualTo(new TimeOnly(14, 0)));
    }

    [Test]
    public void ParseCronExpression_Weekdays_ParsesPattern()
    {
        // Arrange
        var cronExpression = "0 0 9 ? * MON-FRI";

        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;

        Assert.That(spec.DayPattern, Is.EqualTo(DayPattern.Weekdays));
        Assert.That(spec.TimeOfDay, Is.EqualTo(new TimeOnly(9, 0)));
    }

    [Test]
    public void ParseCronExpression_Weekends_ParsesPattern()
    {
        // Arrange
        var cronExpression = "0 0 10 ? * SAT,SUN";

        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;

        Assert.That(spec.DayPattern, Is.EqualTo(DayPattern.Weekends));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("0 0 14 *")]  // Only 4 parts
    [TestCase("0 0 14 * * ? *")]  // 7 parts
    public void ParseCronExpression_InvalidFormat_ReturnsError(string cronExpression)
    {
        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
    }

    [TestCase("* * * * * ?")]
    [TestCase("0 * * * * ?")]
    [TestCase("0 0 * * * ?")]
    public void ParseCronExpression_WildcardPatterns_ParsesAsInterval1(string cronExpression)
    {
        // Act
        var result = _parser.ParseCronExpression(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse wildcard cron expression '{cronExpression}'");
    }
}
