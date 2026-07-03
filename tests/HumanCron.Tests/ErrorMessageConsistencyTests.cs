using HumanCron.Converters.Unix;
using HumanCron.Models;
using HumanCron.NCrontab.Converters;
using HumanCron.Quartz;
using Quartz;

namespace HumanCron.Tests;

/// <summary>
/// Regression coverage for issue #12: all three schedule converters must return
/// the identical, unprefixed parser error message for the same invalid input.
/// </summary>
[TestFixture]
public class ErrorMessageConsistencyTests
{
    [TestCase("invalid")]
    [TestCase("xyz")]
    public void ParseError_IsIdenticalAndUnprefixed_AcrossAllThreeConverters(string invalidInput)
    {
        var unixConverter = UnixCronConverter.Create();
        var ncrontabConverter = NCrontabConverter.Create();
        var quartzConverter = QuartzScheduleConverterFactory.Create();

        var unixResult = unixConverter.ToCron(invalidInput);
        var ncrontabResult = ncrontabConverter.ToNCrontab(invalidInput);
        var quartzResult = quartzConverter.ToQuartzSchedule(invalidInput);

        Assert.That(unixResult, Is.TypeOf<ParseResult<string>.Error>());
        Assert.That(ncrontabResult, Is.TypeOf<ParseResult<string>.Error>());
        Assert.That(quartzResult, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());

        var unixMessage = ((ParseResult<string>.Error)unixResult).Message;
        var ncrontabMessage = ((ParseResult<string>.Error)ncrontabResult).Message;
        var quartzMessage = ((ParseResult<IScheduleBuilder>.Error)quartzResult).Message;

        Assert.That(unixMessage, Does.Not.Contain("Failed to parse natural language"));
        Assert.That(ncrontabMessage, Does.Not.Contain("Failed to parse natural language"));

        Assert.That(ncrontabMessage, Is.EqualTo(unixMessage));
        Assert.That(quartzMessage, Is.EqualTo(unixMessage));
    }
}
