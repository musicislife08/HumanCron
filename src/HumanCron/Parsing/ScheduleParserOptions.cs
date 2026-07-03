using System;
using NodaTime;

namespace HumanCron.Parsing;

/// <summary>
/// Options for parsing natural language schedules
/// </summary>
public sealed record ScheduleParserOptions
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

    /// <summary>
    /// Optional floor on the tightest allowed gap between consecutive firings.
    /// Null (default) = no floor, matching today's behavior.
    ///
    /// Inclusive boundary: a schedule is valid when its tightest gap is greater than or
    /// equal to MinInterval. Only schedules that fire strictly more often than this are
    /// rejected. For example, with MinInterval = 15 minutes, "every 15 minutes" is allowed
    /// and "every 5 minutes" is rejected.
    ///
    /// Reuse a shared instance across calls (like System.Text.Json's JsonSerializerOptions)
    /// rather than looking for a global/DI-configured default - there isn't one:
    /// <code>
    /// private static readonly ScheduleParserOptions Defaults = new() { MinInterval = TimeSpan.FromMinutes(15) };
    /// converter.ToCron(text, Defaults);
    /// converter.ToCron(text, Defaults with { MinInterval = TimeSpan.FromMinutes(5) }); // per-call override
    /// </code>
    /// </summary>
    public TimeSpan? MinInterval { get; init; }
}
