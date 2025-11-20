using System;
using System.Collections.Generic;

namespace HumanCron.Models.Internal;

/// <summary>
/// Day-related constraints parsed from natural language
/// Used as intermediate state during parsing
/// </summary>
internal sealed record DayConstraints
{
    public DayOfWeek? DayOfWeek { get; init; }
    public DayPattern? DayPattern { get; init; }
    public int? DayOfMonth { get; init; }
    public IReadOnlyList<int>? DayList { get; init; }
    public int? DayStart { get; init; }
    public int? DayEnd { get; init; }
    public int? DayStep { get; init; }
    public IReadOnlyList<DayOfWeek>? DayOfWeekList { get; init; }
    public DayOfWeek? DayOfWeekStart { get; init; }
    public DayOfWeek? DayOfWeekEnd { get; init; }
}

/// <summary>
/// Month-related constraints parsed from natural language
/// Used as intermediate state during parsing
/// </summary>
internal sealed record MonthConstraints
{
    public MonthSpecifier Specifier { get; init; } = new MonthSpecifier.None();
}

/// <summary>
/// Time-related constraints parsed from natural language
/// Used as intermediate state during parsing
/// </summary>
internal sealed record TimeConstraints
{
    public TimeOnly? TimeOfDay { get; init; }
    public IReadOnlyList<int>? MinuteList { get; init; }
    public int? MinuteStart { get; init; }
    public int? MinuteEnd { get; init; }
    public int? MinuteStep { get; init; }
    public IReadOnlyList<int>? HourList { get; init; }
    public int? HourStart { get; init; }
    public int? HourEnd { get; init; }
    public int? HourStep { get; init; }
}

/// <summary>
/// Advanced Quartz-specific constraints (L, W, #)
/// Used as intermediate state during parsing
/// </summary>
internal sealed record AdvancedQuartzConstraints
{
    public bool IsLastDay { get; init; }
    public bool IsLastDayOfWeek { get; init; }
    public int? LastDayOffset { get; init; }
    public bool IsNearestWeekday { get; init; }
    public int? NthOccurrence { get; init; }
}

/// <summary>
/// Year constraint parsed from natural language
/// Used as intermediate state during parsing
/// </summary>
internal sealed record YearConstraint
{
    public int? Year { get; init; }
}
