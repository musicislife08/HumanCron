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
/// Day formatting strategy (discriminated union pattern)
/// Determines how day constraints should be formatted in natural language
/// Enforces precedence rules at compile time via exhaustive pattern matching
/// </summary>
internal abstract record DayFormatStrategy
{
    /// <summary>
    /// Compact numeric notation with ranges: "on the 1-7,15-21,30"
    /// Used when day list contains sequences of 3+ consecutive values
    /// </summary>
    public sealed record CompactList(IReadOnlyList<int> Days) : DayFormatStrategy;

    /// <summary>
    /// Ordinal notation for simple lists: "on the 1st and 15th" or "on the 1st, 15th, 30th"
    /// Used when day list has no ranges (no 3+ consecutive sequences)
    /// </summary>
    public sealed record OrdinalList(IReadOnlyList<int> Days) : DayFormatStrategy;

    /// <summary>
    /// Combined month+day syntax: "on january 15th"
    /// Used for yearly schedules with single month and single day
    /// More natural than "on the 15th in january"
    /// </summary>
    public sealed record CombinedMonthDay(int Month, int Day) : DayFormatStrategy;

    /// <summary>
    /// Single ordinal day: "on the 15th"
    /// Used when only DayOfMonth is specified (no list, no combined syntax)
    /// </summary>
    public sealed record SingleOrdinal(int Day) : DayFormatStrategy;

    /// <summary>
    /// Day range with ordinals: "between the 1st and 15th"
    /// </summary>
    public sealed record DayRange(int Start, int End) : DayFormatStrategy;

    /// <summary>
    /// No day constraint formatting needed
    /// </summary>
    public sealed record None : DayFormatStrategy;

    // Prevent external inheritance - only the defined cases are valid
    private DayFormatStrategy() { }
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
    /// List of specific days of week: "every monday,wednesday,friday"
    /// Null if not specified
    /// </summary>
    public IReadOnlyList<DayOfWeek>? DayOfWeekList { get; init; }

    /// <summary>
    /// Start day of custom day-of-week range: "every tuesday-thursday"
    /// Must be paired with DayOfWeekEnd. Null if not specified.
    /// </summary>
    public DayOfWeek? DayOfWeekStart { get; init; }

    /// <summary>
    /// End day of custom day-of-week range: "every tuesday-thursday"
    /// Must be paired with DayOfWeekStart. Null if not specified.
    /// </summary>
    public DayOfWeek? DayOfWeekEnd { get; init; }

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

    // Quartz-specific advanced features (L, W, # characters)

    /// <summary>
    /// Last day of month (L in day field)
    /// "every month on last day" → IsLastDay = true
    /// Generates: "L" in day field
    /// </summary>
    public bool IsLastDay { get; init; }

    /// <summary>
    /// Last occurrence of day-of-week (L in day-of-week field)
    /// "every month on last friday" → IsLastDayOfWeek = true, DayOfWeek = Friday
    /// Generates: "6L" (6 = Friday in Quartz)
    /// </summary>
    public bool IsLastDayOfWeek { get; init; }

    /// <summary>
    /// Offset from last day (L-N in day field)
    /// "3rd to last day" or "day before last" → LastDayOffset = 3 or 1
    /// Generates: "L-3" or "L-1"
    /// </summary>
    public int? LastDayOffset { get; init; }

    /// <summary>
    /// Nearest weekday to specified day (W in day field)
    /// "every month on weekday nearest 15" → IsNearestWeekday = true, DayOfMonth = 15
    /// Generates: "15W"
    /// Special case: "last weekday" → IsLastDay = true, IsNearestWeekday = true → "LW"
    /// </summary>
    public bool IsNearestWeekday { get; init; }

    /// <summary>
    /// Nth occurrence of day-of-week (# in day-of-week field)
    /// "3rd friday" → NthOccurrence = 3, DayOfWeek = Friday
    /// Generates: "6#3" (6 = Friday in Quartz)
    /// Valid range: 1-5 (1st through 5th occurrence)
    /// </summary>
    public int? NthOccurrence { get; init; }

    // Range and list support for second, minute, hour, and day fields
    // Used for cron expressions like "0-30 9-17 1-15 * *" (ranges) and "0,15,30,45 9,12,15 1,15,30 * *" (lists)

    /// <summary>
    /// Second value (0-59) - null if not specified
    /// NCrontab: "30 * * * * *" → Second = 30 (at 30 seconds past each minute)
    /// </summary>
    public int? Second { get; init; }

    /// <summary>
    /// Second range start (0-59) - null if not specified
    /// NCrontab: "0-30 * * * * *" → SecondStart = 0, SecondEnd = 30
    /// </summary>
    public int? SecondStart { get; init; }

    /// <summary>
    /// Second range end (0-59) - null if not specified
    /// NCrontab: "0-30 * * * * *" → SecondStart = 0, SecondEnd = 30
    /// </summary>
    public int? SecondEnd { get; init; }

    /// <summary>
    /// Second range step (1-59) - null if not specified
    /// NCrontab: "0-30/5 * * * * *" → SecondStart = 0, SecondEnd = 30, SecondStep = 5
    /// </summary>
    public int? SecondStep { get; init; }

    /// <summary>
    /// Second list (0-59) - null if not specified
    /// NCrontab: "0,15,30,45 * * * * *" → SecondList = [0, 15, 30, 45]
    /// </summary>
    public IReadOnlyList<int>? SecondList { get; init; }

    /// <summary>
    /// Minute range start (0-59) - null if not specified
    /// Cron: "0-30 * * * *" → MinuteStart = 0, MinuteEnd = 30
    /// </summary>
    public int? MinuteStart { get; init; }

    /// <summary>
    /// Minute range end (0-59) - null if not specified
    /// Cron: "0-30 * * * *" → MinuteStart = 0, MinuteEnd = 30
    /// </summary>
    public int? MinuteEnd { get; init; }

    /// <summary>
    /// Minute range step (1-59) - null if not specified
    /// Cron: "0-30/5 * * * *" → MinuteStart = 0, MinuteEnd = 30, MinuteStep = 5
    /// </summary>
    public int? MinuteStep { get; init; }

    /// <summary>
    /// Minute list (0-59) - null if not specified
    /// Cron: "0,15,30,45 * * * *" → MinuteList = [0, 15, 30, 45]
    /// </summary>
    public IReadOnlyList<int>? MinuteList { get; init; }

    /// <summary>
    /// Hour range start (0-23) - null if not specified
    /// Cron: "0 9-17 * * *" → HourStart = 9, HourEnd = 17
    /// </summary>
    public int? HourStart { get; init; }

    /// <summary>
    /// Hour range end (0-23) - null if not specified
    /// Cron: "0 9-17 * * *" → HourStart = 9, HourEnd = 17
    /// </summary>
    public int? HourEnd { get; init; }

    /// <summary>
    /// Hour range step (1-23) - null if not specified
    /// Cron: "0 9-17/2 * * *" → HourStart = 9, HourEnd = 17, HourStep = 2
    /// </summary>
    public int? HourStep { get; init; }

    /// <summary>
    /// Hour list (0-23) - null if not specified
    /// Cron: "0 9,12,15,18 * * *" → HourList = [9, 12, 15, 18]
    /// </summary>
    public IReadOnlyList<int>? HourList { get; init; }

    /// <summary>
    /// Day-of-month range start (1-31) - null if not specified
    /// Cron: "0 0 1-15 * *" → DayStart = 1, DayEnd = 15
    /// </summary>
    public int? DayStart { get; init; }

    /// <summary>
    /// Day-of-month range end (1-31) - null if not specified
    /// Cron: "0 0 1-15 * *" → DayStart = 1, DayEnd = 15
    /// </summary>
    public int? DayEnd { get; init; }

    /// <summary>
    /// Day-of-month range step (1-31) - null if not specified
    /// Cron: "0 0 1-15/3 * *" → DayStart = 1, DayEnd = 15, DayStep = 3
    /// </summary>
    public int? DayStep { get; init; }

    /// <summary>
    /// Day-of-month list (1-31) - null if not specified
    /// Cron: "0 0 1,15,30 * *" → DayList = [1, 15, 30]
    /// </summary>
    public IReadOnlyList<int>? DayList { get; init; }

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

    /// <summary>
    /// Year constraint (1970-2099) - null if not specified
    /// Quartz-specific: Optional 7th field in cron expression
    /// Quartz: "0 0 12 * * ? 2025" → Year = 2025 (only run in 2025)
    /// </summary>
    public int? Year { get; init; }
}
