using HumanCron.Models.Internal;
using HumanCron.Validation;

namespace HumanCron.Tests.Validation;

[TestFixture]
public class TightestGapCalculatorTests
{
    private static ScheduleSpec PlainInterval(int interval, IntervalUnit unit) =>
        new() { Interval = interval, Unit = unit };

    [TestCase(0, 30)]
    [TestCase(1, 15 * 60)]
    [TestCase(2, 6 * 3600)]
    [TestCase(3, 24 * 3600)]
    [TestCase(4, 2 * 7 * 24 * 3600)]
    [TestCase(5, 3 * 28 * 24 * 3600)]
    [TestCase(6, 365 * 24 * 3600)]
    public void Calculate_PlainInterval_ReturnsIntervalTimesUnitLength(int unitIndex, int expectedSeconds)
    {
        var units = new[] { IntervalUnit.Seconds, IntervalUnit.Minutes, IntervalUnit.Hours, IntervalUnit.Days, IntervalUnit.Weeks, IntervalUnit.Months, IntervalUnit.Years };
        var intervals = new[] { 30, 15, 6, 1, 2, 3, 1 };
        var unit = units[unitIndex];
        var interval = intervals[unitIndex];

        var spec = PlainInterval(interval, unit);

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }

    [Test]
    public void Calculate_DayOfWeek_ReturnsFlat24Hours()
    {
        // "1w on monday" - Interval/Unit alone would say 7 days, but the DayOfWeek
        // constraint is what actually governs an at-most-daily gap.
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Weeks, DayOfWeek = DayOfWeek.Monday };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_WeekdayPattern_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Days, DayPattern = DayPattern.Weekdays };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_DayOfMonth_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Months, DayOfMonth = 15 };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_LastDayOfMonth_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Months, IsLastDay = true };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }
}
