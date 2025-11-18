using System;
using System.Collections.Generic;
using NodaTime;

namespace HumanCron.Models.Internal;

/// <summary>
/// Month specifier for schedule (discriminated union pattern)
/// Enforces mutual exclusivity at compile time - can only be one type
/// </summary>
internal abstract record MonthSpecifier
{
    /// <summary>
    /// No month constraint - schedule runs in all months
    /// </summary>
    public sealed record None : MonthSpecifier;

    /// <summary>
    /// Specific month constraint (1-12): "every day in january"
    /// </summary>
    public sealed record Single(int Month) : MonthSpecifier;

    /// <summary>
    /// Month range constraint (1-12): "every day between january and march" = (1, 3)
    /// </summary>
    public sealed record Range(int Start, int End) : MonthSpecifier;

    /// <summary>
    /// Month list constraint (1-12): "on 15 in jan,apr,jul,oct" = [1,4,7,10]
    /// </summary>
    public sealed record List(IReadOnlyList<int> Months) : MonthSpecifier;

    // Prevent external inheritance - only None, Single, Range, and List are valid
    private MonthSpecifier() { }
}

/// <summary>
/// Represents a parsed schedule specification (format-agnostic)
/// This is the intermediate representation between natural language and cron
/// INTERNAL: Not exposed in public API - used internally for parsing/conversion
/// </summary>
internal sealed record ScheduleSpec
{
    /// <summary>
    /// Interval value (1, 6, 30, etc.)
    /// </summary>
    public required int Interval { get; init; }

    /// <summary>
    /// Interval unit (Minutes, Hours, Days, Weeks)
    /// </summary>
    public required IntervalUnit Unit { get; init; }

    /// <summary>
    /// Specific day of week (Monday, Sunday, etc.) - null if not specified
    /// Used with daily/weekly intervals: "1d on monday", "1w on weekdays"
    /// </summary>
    public DayOfWeek? DayOfWeek { get; init; }

    /// <summary>
    /// Day pattern (Weekdays, Weekends) - null if not specified
    /// Used with daily intervals: "1d on weekdays"
    /// </summary>
    public DayPattern? DayPattern { get; init; }

    /// <summary>
    /// Day of month (1-31) - null if not specified
    /// Used with monthly intervals: "1M on 15 at 2pm" = 15th of month
    /// Context-aware: "on" with months means day-of-month, with weeks means day-of-week
    /// </summary>
    public int? DayOfMonth { get; init; }

    /// <summary>
    /// Month specifier - determines which months the schedule runs in
    /// Defaults to None (all months) if not specified
    /// Uses discriminated union pattern for compile-time safety
    /// </summary>
    public MonthSpecifier Month { get; init; } = new MonthSpecifier.None();

    /// <summary>
    /// Time of day (14:00, 03:30) - null if not specified
    /// </summary>
    public TimeOnly? TimeOfDay { get; init; }

    /// <summary>
    /// Source timezone for interpreting TimeOfDay values
    ///
    /// For Unix cron: Times are converted from this timezone to server local timezone
    /// For Quartz: This timezone is preserved via .InTimeZone() for DST-aware scheduling
    ///
    /// Defaults to Local (system timezone via DateTimeZoneProviders.Tzdb.GetSystemDefault())
    /// Note: Default is evaluated at instance creation time
    ///
    /// CLI tools: "1d at 2pm" means 2pm local time
    /// Cross-timezone apps: Pass explicit timezone via ScheduleParserOptions or set directly
    ///
    /// Use IANA timezone IDs (e.g., "America/New_York", "Europe/London")
    /// </summary>
    public DateTimeZone TimeZone { get; init; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();
}
