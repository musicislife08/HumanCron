using NodaTime;

namespace HumanCron.Parsing;

/// <summary>
/// Options for parsing natural language schedules
/// </summary>
public sealed class ScheduleParserOptions
{
    /// <summary>
    /// The timezone for interpreting times in the schedule
    /// Default: Local system timezone (via DateTimeZoneProviders.Tzdb.GetSystemDefault())
    /// Note: Default is evaluated at instance creation time
    ///
    /// For Unix cron: Times are converted to this timezone for output
    /// For Quartz: This timezone is applied via .InTimeZone() for DST-aware scheduling
    ///
    /// Examples:
    /// - Local (default): "1d at 2pm" runs at 2pm in server's timezone
    /// - User timezone: "1d at 2pm" runs at 2pm in user's timezone (converted to server time for Unix cron)
    /// - UTC: "1d at 2pm" runs at 2pm UTC
    ///
    /// Use IANA timezone IDs (e.g., "America/New_York", "Europe/London")
    /// Get timezone: DateTimeZoneProviders.Tzdb["America/New_York"]
    /// </summary>
    public DateTimeZone TimeZone { get; init; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();
}
