using HumanCron.Models.Internal;
using HumanCron.Validation;

namespace HumanCron.Tests.Validation;

[TestFixture]
public class TightestGapCalculatorTests
{
    private static ScheduleSpec PlainInterval(int interval, IntervalUnit unit) =>
        new() { Interval = interval, Unit = unit };

    [TestCase(30, (int)IntervalUnit.Seconds, 30)]
    [TestCase(15, (int)IntervalUnit.Minutes, 15 * 60)]
    [TestCase(6, (int)IntervalUnit.Hours, 6 * 3600)]
    [TestCase(1, (int)IntervalUnit.Days, 24 * 3600)]
    [TestCase(2, (int)IntervalUnit.Weeks, 2 * 7 * 24 * 3600)]
    [TestCase(3, (int)IntervalUnit.Months, 3 * 28 * 24 * 3600)]
    [TestCase(1, (int)IntervalUnit.Years, 365 * 24 * 3600)]
    public void Calculate_PlainInterval_ReturnsIntervalTimesUnitLength(int interval, int unitValue, int expectedSeconds)
    {
        var unit = (IntervalUnit)unitValue;
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

    [Test]
    public void Calculate_LastDayOffset_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Months, LastDayOffset = 3 };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_MinuteListEvenlySpaced_ReturnsMinAdjacentDifference()
    {
        // "at minutes 0,15,30,45" - evenly spaced, tightest gap is 15 minutes both
        // forward and via wraparound (45 -> 60/0 is also 15).
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [0, 15, 30, 45]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }

    [Test]
    public void Calculate_MinuteListUnevenlySpaced_ReturnsSmallestGap()
    {
        // "at minutes 0,10,20,30,40,50" - every 10 minutes
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [0, 10, 20, 30, 40, 50]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Calculate_MinuteListSingleEntry_ReturnsFullHourCycle()
    {
        // "at minutes 30" - fires once per hour. Min-adjacent-difference is undefined
        // on a single-element list; this must not fall through to a gap of zero.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [30]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(60)));
    }

    [Test]
    public void Calculate_MinuteListWraparound_ReturnsWraparoundGap()
    {
        // "at minutes 5,55" - 55 -> 5 wrapping past the hour is 10 minutes, tighter
        // than the forward gap of 50 minutes.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [5, 55]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Calculate_MinuteListKnownPessimisticWraparound_OverRejectsContrivedPattern()
    {
        // Documented, accepted imprecision from the design: "at minutes 0,59 at hours 9
        // and 15" computes a 1-minute wraparound gap (0 and 59 are adjacent mod 60) even
        // though hours 9 and 15 are not themselves adjacent, so the true tightest gap is
        // 59 minutes. This is safe (over-rejects, never under-rejects) but is worth a
        // test that pins the current, intentionally conservative behavior.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteList = [0, 59],
            HourList = [9, 15]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Calculate_MinuteRangeWithStep_ReturnsStepValue()
    {
        // "every 5 minutes between 0 and 30 of each hour"
        var spec = new ScheduleSpec
        {
            Interval = 5,
            Unit = IntervalUnit.Minutes,
            MinuteStart = 0,
            MinuteEnd = 30,
            MinuteStep = 5
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void Calculate_MinuteRangeWithoutExplicitStep_DefaultsToOneMinute()
    {
        // "every day between minutes 0 and 30" - a bare cron minute range with no
        // step fires every minute in the window (cron "0-30 * * * *").
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteStart = 0,
            MinuteEnd = 30
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Calculate_HourListEvenlySpaced_ReturnsMinAdjacentDifference()
    {
        // "at hours 0,6,12,18"
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourList = [0, 6, 12, 18]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(6)));
    }

    [Test]
    public void Calculate_HourListSingleEntry_ReturnsFullDayCycle()
    {
        // "at hours 9" - fires once per day
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourList = [9]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_HourRangeWithStep_ReturnsStepValue()
    {
        // "every 2 hours between 9am and 5pm of each day"
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Hours,
            HourStart = 9,
            HourEnd = 17,
            HourStep = 2
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void Calculate_HourRangeWithoutExplicitStep_DefaultsToOneHour()
    {
        // "every day between hours 9 and 17" - fires once per hour in the window
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourStart = 9,
            HourEnd = 17
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void Calculate_MinuteListTakesPrecedenceOverHourList()
    {
        // A minute-level list is a finer-grained signal than an hour list; the
        // calculator must pick the tightest (most sub-day-capable) evidence present.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteList = [0, 20, 40],
            HourList = [9]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }
}
