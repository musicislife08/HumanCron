using System;
using HumanCron.Models.Internal;

namespace HumanCron.Validation;

/// <summary>
/// Computes the tightest possible gap between consecutive firings of a parsed schedule.
/// INTERNAL: Used by NaturalLanguageParser to enforce ScheduleParserOptions.MinInterval.
/// Pessimistic by design: when a shape's true tightest gap can't be pinned down exactly,
/// this returns a value no larger than reality, since underestimating the gap is the safe
/// error direction for a floor (it only over-rejects, never lets a too-fast schedule through).
/// </summary>
internal static class TightestGapCalculator
{
    internal static TimeSpan Calculate(ScheduleSpec spec)
    {
        if (IsAtMostDaily(spec))
        {
            return TimeSpan.FromHours(24);
        }

        return PlainIntervalGap(spec.Interval, spec.Unit);
    }

    // Day-of-week/day-of-month/weekday constraints can never fire twice on the same
    // calendar day, so 24h is an exact lower bound here, not just a pessimistic guess.
    private static bool IsAtMostDaily(ScheduleSpec spec) =>
        spec.DayOfWeek.HasValue
        || spec.DayPattern.HasValue
        || spec.DayOfWeekList is { Count: > 0 }
        || spec.DayOfWeekStart.HasValue
        || spec.DayOfMonth.HasValue
        || spec.IsLastDay
        || spec.IsLastDayOfWeek
        || spec.IsNearestWeekday
        || spec.NthOccurrence.HasValue
        || spec.DayList is { Count: > 0 }
        || spec.DayStart.HasValue;

    private static TimeSpan PlainIntervalGap(int interval, IntervalUnit unit) => unit switch
    {
        IntervalUnit.Seconds => TimeSpan.FromSeconds(interval),
        IntervalUnit.Minutes => TimeSpan.FromMinutes(interval),
        IntervalUnit.Hours => TimeSpan.FromHours(interval),
        IntervalUnit.Days => TimeSpan.FromDays(interval),
        IntervalUnit.Weeks => TimeSpan.FromDays(interval * 7),
        // Months/years approximate to the shortest possible calendar length (28d/365d) -
        // underestimating is the safe direction for a floor. Only matters above ~4 weeks.
        IntervalUnit.Months => TimeSpan.FromDays(interval * 28),
        IntervalUnit.Years => TimeSpan.FromDays(interval * 365),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown interval unit"),
    };
}
