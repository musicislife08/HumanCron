using System;
using HumanCron.Formatting;
using HumanCron.Parsing;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz.Converters;
using NodaTime;

namespace HumanCron.Quartz;

/// <summary>
/// Factory for creating QuartzScheduleConverter instances
/// Useful for testing and scenarios where dependency injection is not available
/// </summary>
/// <remarks>
/// This factory creates instances with default system dependencies:
/// - SystemClock.Instance for time operations
/// - System default timezone via DateTimeZoneProviders.Tzdb
///
/// For production use with DI, use AddHumanCron() instead which automatically
/// discovers and registers this extension package.
/// </remarks>
/// <example>
/// <code>
/// // For testing or non-DI scenarios
/// var converter = QuartzScheduleConverterFactory.Create();
/// var result = converter.ToQuartzSchedule("1d at 2pm");
/// </code>
/// </example>
public static class QuartzScheduleConverterFactory
{
    /// <summary>
    /// Create a new QuartzScheduleConverter with default dependencies
    /// </summary>
    /// <returns>Configured QuartzScheduleConverter instance</returns>
    public static IQuartzScheduleConverter Create()
    {
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        var localTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault()
            ?? throw new InvalidOperationException(
                "Could not determine system timezone. NodaTime TZDB data may be corrupted.");

        return new QuartzScheduleConverter(
            parser,
            formatter,
            SystemClock.Instance,
            localTimeZone
        );
    }
}
