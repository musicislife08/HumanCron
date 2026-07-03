using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class ScheduleParserOptionsTests
{
    [Test]
    public void MinInterval_DefaultsToNull()
    {
        var options = new ScheduleParserOptions();

        Assert.That(options.MinInterval, Is.Null);
    }

    [Test]
    public void WithExpression_OverridesMinIntervalWithoutMutatingOriginal()
    {
        var defaults = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var perCall = defaults with { MinInterval = TimeSpan.FromMinutes(5) };

        Assert.That(defaults.MinInterval, Is.EqualTo(TimeSpan.FromMinutes(15)));
        Assert.That(perCall.MinInterval, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(perCall.TimeZone, Is.EqualTo(defaults.TimeZone));
    }
}
