using HumanCron.Formatting;
using NodaTime;

namespace HumanCron.Tests.Formatting;

[TestFixture]
public class DurationFormatterTests
{
    [Test]
    public void Format_ZeroPeriod_ReturnsEmptyString()
    {
        var result = DurationFormatter.Format(Period.Zero);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void Format_SingleUnit_OmitsZeroComponents()
    {
        var period = Period.FromMinutes(1);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 minute"));
    }

    [Test]
    public void Format_PluralValue_UsesPluralWord()
    {
        var period = Period.FromMinutes(30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("30 minutes"));
    }

    [Test]
    public void Format_CompoundPeriod_DescendingLargestUnits()
    {
        var period = Period.FromHours(2) + Period.FromMinutes(30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("2 hours 30 minutes"));
    }

    [Test]
    public void Format_DaysFoldIntoWeeks()
    {
        var period = Period.FromDays(10);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 week 3 days"));
    }

    [Test]
    public void Format_ExactWeek_NoTrailingZeroDays()
    {
        var period = Period.FromDays(7);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 week"));
    }

    [Test]
    public void Format_NegativePeriod_SingleLeadingSign()
    {
        var period = Period.FromHours(-2) + Period.FromMinutes(-30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("-2 hours 30 minutes"));
    }

    [Test]
    public void Format_MillisecondsIncludedWhenNonzero()
    {
        var period = Period.FromSeconds(1) + Period.FromMilliseconds(500);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 second 500 milliseconds"));
    }

    [Test]
    public void Format_YearsAndMonths_IncludedAsIs()
    {
        var period = Period.FromYears(1) + Period.FromMonths(2);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 year 2 months"));
    }

    [Test]
    public void Format_AllUnitsTogether_FullDescendingOrder()
    {
        var period = Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(10)
            + Period.FromHours(3) + Period.FromMinutes(4) + Period.FromSeconds(5) + Period.FromMilliseconds(6);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 year 2 months 1 week 3 days 3 hours 4 minutes 5 seconds 6 milliseconds"));
    }
}
