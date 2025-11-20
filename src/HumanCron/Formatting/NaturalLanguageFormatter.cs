using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using HumanCron.Abstractions;

namespace HumanCron.Formatting;

/// <summary>
/// Formats ScheduleSpec back to natural language representation
/// Provides the reverse operation of NaturalLanguageParser
/// INTERNAL: Used internally by converters and builders
/// </summary>
internal sealed class NaturalLanguageFormatter : IScheduleFormatter
{
    // Map month numbers to full names (always output full names, never abbreviations)
    private static readonly Dictionary<int, string> MonthNumberToName = new()
    {
        [1] = "january",
        [2] = "february",
        [3] = "march",
        [4] = "april",
        [5] = "may",
        [6] = "june",
        [7] = "july",
        [8] = "august",
        [9] = "september",
        [10] = "october",
        [11] = "november",
        [12] = "december"
    };

    /// <summary>
    /// Formats a ScheduleSpec as natural language using verbose syntax
    /// </summary>
    /// <param name="spec">The schedule specification to format</param>
    /// <returns>Natural language representation (e.g., "every day at 2pm", "every monday at 9am")</returns>
    /// <exception cref="ArgumentNullException">Thrown when spec is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when spec.Unit is not a valid IntervalUnit</exception>
    public string Format(ScheduleSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Check for range+step pattern first - this completely changes the format
        // "every 5 minutes between 0 and 30 of each hour"
        if (spec is { MinuteStart: not null, MinuteEnd: not null, MinuteStep: not null })
        {
            return FormatRangeStep(
                spec.MinuteStep.Value,
                "minute",
                spec.MinuteStart.Value,
                spec.MinuteEnd.Value,
                "hour",
                spec);
        }
        if (spec is { HourStart: not null, HourEnd: not null, HourStep: not null })
        {
            return FormatRangeStep(
                spec.HourStep.Value,
                "hour",
                spec.HourStart.Value,
                spec.HourEnd.Value,
                "day",
                spec,
                useHourFormat: true);
        }
        if (spec is { DayStart: not null, DayEnd: not null, DayStep: not null })
        {
            return FormatRangeStep(
                spec.DayStep.Value,
                "day",
                spec.DayStart.Value,
                spec.DayEnd.Value,
                "month",
                spec,
                useOrdinals: true);
        }

        // Start with "every"
        List<string> parts = ["every"];

        // Special case: "every month ... in january" is logically yearly, not monthly
        // Instead of "every year", use context-specific patterns that are more natural
        var effectiveUnit = spec.Unit;
        var effectiveInterval = spec.Interval;
        var isMonthlyWithSingleMonth = spec.Unit == IntervalUnit.Months &&
                                        spec.Interval == 1 &&
                                        spec.Month is MonthSpecifier.Single;

        // Handle monthly→yearly conversion with special formatting for different constraint types
        if (isMonthlyWithSingleMonth)
        {
            // Monthly + single month = runs once (or few times) per year
            // Use simplified patterns instead of "every year"

            if (spec is { NthOccurrence: not null, DayOfWeek: not null })
            {
                // "on the 3rd friday in january" (runs once per year)
                // Don't use "every", start with "on the"
                parts.Clear();
                var dayName = spec.DayOfWeek.Value.ToString().ToLowerInvariant();
                parts.Add($"on the {FormatOrdinal(spec.NthOccurrence.Value)} {dayName}");
                // Will add month later
            }
            else if (spec.IsLastDayOfWeek && spec.DayOfWeek.HasValue)
            {
                // "on the last friday in january"
                parts.Clear();
                var dayName = spec.DayOfWeek.Value.ToString().ToLowerInvariant();
                parts.Add($"on the last {dayName}");
                // Will add month later
            }
            else if (spec.IsLastDay)
            {
                // "on the last day in january"
                parts.Clear();
                parts.Add("on the last day");
                // Will add month later
            }
            else if (spec.DayOfWeek.HasValue)
            {
                // "every monday in january" (runs ~4-5 times per year)
                // Use day name instead of "year"
                parts.Add(spec.DayOfWeek.Value.ToString().ToLowerInvariant());
                // Will add month later
            }
            else if (spec.DayList is { Count: > 0 })
            {
                // "on the 1st and 15th in january"
                // Clear parts so we don't have "every year" prefix
                // The day list will be formatted by the normal day list handler below
                parts.Clear();
                effectiveUnit = IntervalUnit.Years;
                effectiveInterval = 1;
            }
            else if (spec is { DayStart: not null, DayEnd: not null })
            {
                // "every day between the 1st and 15th in january"
                // Use "day" instead of "year"
                parts.Add("day");
                // Will add range later
            }
            else if (spec.DayOfMonth.HasValue)
            {
                // "on january 15th" (will use combined syntax)
                // Set effective unit for combined month+day syntax check
                effectiveUnit = IntervalUnit.Years;
                effectiveInterval = 1;
            }
            else
            {
                // Fallback: use yearly
                effectiveUnit = IntervalUnit.Years;
                effectiveInterval = 1;
            }
        }

        // Add interval + unit (verbose form) only if not already handled above
        // Check both that parts.Count == 1 AND that the single element is "every"
        // (because monthly→yearly conversion may have cleared parts and added a custom pattern)
        if (parts.Count == 1 && parts[0] == "every") // Still just ["every"]
        {
            var intervalPart = FormatInterval(effectiveInterval, effectiveUnit);
            parts.Add(intervalPart);
        }

        // Add day-of-week constraint (e.g., "every monday" or "between monday and friday")
        // Skip if already handled in monthly→yearly conversion above
        // Priority: list > range > single day > pattern
        // Check for day-of-week list first: "every monday,wednesday,friday"
        if (spec.DayOfWeekList is { Count: > 0 } dayOfWeekList)
        {
            var dayNames = dayOfWeekList.Select(d => d.ToString().ToLowerInvariant());
            parts[^1] = string.Join(",", dayNames);
        }
        // Check for custom day-of-week range: "every tuesday-thursday"
        else if (spec is { DayOfWeekStart: not null, DayOfWeekEnd: not null })
        {
            var startDay = spec.DayOfWeekStart.Value.ToString().ToLowerInvariant();
            var endDay = spec.DayOfWeekEnd.Value.ToString().ToLowerInvariant();
            parts[^1] = $"{startDay}-{endDay}";
        }
        // Single day-of-week
        // Exception: Don't replace interval if NthOccurrence or IsLastDayOfWeek is set
        // because those need "every month on 3rd friday" not "every friday on 3rd friday"
        else if (!isMonthlyWithSingleMonth && // NEW: Skip if already handled
            spec is { DayOfWeek: not null, NthOccurrence: null, IsLastDayOfWeek: false })
        {
            // No "on" prefix for specific days when using "every" already
            // "every monday" not "every on monday"
            parts[^1] = spec.DayOfWeek.Value.ToString().ToLowerInvariant();
        }
        // Add day pattern constraint (e.g., "every weekday")
        else if (spec.DayPattern.HasValue)
        {
            var pattern = spec.DayPattern.Value == DayPattern.Weekdays ? "weekday" : "weekend";
            parts[^1] = pattern;
        }
        // Add day range (e.g., "between monday and friday")
        // Note: Currently only weekdays (mon-fri) is supported, which maps to DayPattern.Weekdays above
        // Future: Add DayRange support when needed

        // Add day-of-month constraint - check for advanced Quartz features first

        // Last weekday: "on the last weekday"
        if (spec is { IsLastDay: true, IsNearestWeekday: true })
        {
            parts.Add("on the last weekday");
        }
        // Last day offset: "on the 3rd to last day" or "on the day before last"
        else if (spec.LastDayOffset.HasValue)
        {
            parts.Add(spec.LastDayOffset.Value == 1
                ? "on the day before last"
                : $"on the {FormatOrdinal(spec.LastDayOffset.Value)} to last day");
        }
        // Last day: "on the last day"
        // Skip if already handled in monthly+single month block
        else if (spec.IsLastDay && !isMonthlyWithSingleMonth)
        {
            parts.Add("on the last day");
        }
        // Last occurrence of day-of-week: "on the last friday"
        // Skip if already handled in monthly+single month block
        else if (spec is { IsLastDayOfWeek: true, DayOfWeek: not null } && !isMonthlyWithSingleMonth)
        {
            var dayName = spec.DayOfWeek.Value.ToString().ToLowerInvariant();
            parts.Add($"on the last {dayName}");
        }
        // Weekday nearest: "on the weekday nearest the 15th"
        else if (spec is { IsNearestWeekday: true, DayOfMonth: not null })
        {
            parts.Add($"on the weekday nearest the {FormatOrdinal(spec.DayOfMonth.Value)}");
        }
        // Nth occurrence: "on the 3rd friday"
        // Skip if already handled in monthly+single month block
        else if (spec is { NthOccurrence: not null, DayOfWeek: not null } && !isMonthlyWithSingleMonth)
        {
            var dayName = spec.DayOfWeek.Value.ToString().ToLowerInvariant();
            parts.Add($"on the {FormatOrdinal(spec.NthOccurrence.Value)} {dayName}");
        }
        // Day constraints using discriminated union pattern
        // Determines strategy, then formats - clear separation of concerns
        else
        {
            var dayStrategy = DetermineDayFormatStrategy(spec, effectiveUnit, isMonthlyWithSingleMonth);
            var dayFormatted = FormatDayStrategy(dayStrategy);
            if (!string.IsNullOrEmpty(dayFormatted))
            {
                parts.Add(dayFormatted);
            }
        }

        // Add time constraints - check for lists/ranges first, then time-of-day
        // These are mutually exclusive ways of specifying when to run

        // Hour lists/ranges (e.g., "at hours 9,12,15,18" or "between hours 9 and 17")
        var hasHourListOrRange = false;
        if (spec.HourList is { Count: > 0 })
        {
            var hourListStr = CompactList(spec.HourList);
            parts.Add($"at hours {hourListStr}");
            hasHourListOrRange = true;
        }
        else if (spec is { HourStart: not null, HourEnd: not null })
        {
            var startTime = FormatHour(spec.HourStart.Value);
            var endTime = FormatHour(spec.HourEnd.Value);
            parts.Add($"between hours {startTime} and {endTime}");
            hasHourListOrRange = true;
        }

        // Minute lists/ranges (e.g., "at minutes 0,15,30,45" or "between minutes 0 and 30")
        var hasMinuteListOrRange = false;
        if (spec.MinuteList is { Count: > 0 })
        {
            var minuteListStr = CompactList(spec.MinuteList);
            parts.Add($"at minutes {minuteListStr}");
            hasMinuteListOrRange = true;
        }
        else if (spec is { MinuteStart: not null, MinuteEnd: not null })
        {
            parts.Add($"between minutes {spec.MinuteStart.Value} and {spec.MinuteEnd.Value}");
            hasMinuteListOrRange = true;
        }

        // Time-of-day constraint (e.g., "at 2pm", "at 14:30")
        // Only use if no hour/minute lists/ranges are specified
        if (!hasHourListOrRange && !hasMinuteListOrRange && spec.TimeOfDay.HasValue)
        {
            var timePart = FormatTime(spec.TimeOfDay.Value);
            parts.Add(timePart);
        }

        // Add month specifier (e.g., "in january", "between january and march")
        // Skip if already included in combined month+day syntax above (yearly schedules)
        // Don't skip if DayList has multiple items (those need separate month specifier)
        var skipMonth = effectiveUnit == IntervalUnit.Years && spec.DayOfMonth.HasValue && spec.Month is MonthSpecifier.Single && (spec.DayList is null or { Count: <= 1 });
        if (spec.Month is not MonthSpecifier.None && !skipMonth)
        {
            var monthPart = FormatMonthSpecifier(spec.Month);
            parts.Add(monthPart);
        }

        // Add year constraint (e.g., "in year 2025")
        if (spec.Year.HasValue)
        {
            parts.Add($"in year {spec.Year.Value}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Format range+step patterns: "every 5 minutes between 0 and 30 of each hour"
    /// </summary>
    private static string FormatRangeStep(
        int step,
        string stepUnit,
        int rangeStart,
        int rangeEnd,
        string scopeUnit,
        ScheduleSpec spec,
        bool useOrdinals = false,
        bool useHourFormat = false)
    {
        List<string> parts = ["every"];

        // Interval part: "5 minutes" or "2 hours"
        var unitName = step == 1 ? stepUnit : $"{stepUnit}s";
        var intervalPart = step == 1 ? unitName : $"{step} {unitName}";
        parts.Add(intervalPart);

        // Range part: "between 0 and 30", "between the 1st and 15th", or "between 9am and 5pm"
        string startStr, endStr, betweenPart;
        if (useOrdinals)
        {
            startStr = FormatOrdinal(rangeStart);
            endStr = FormatOrdinal(rangeEnd);
            betweenPart = $"between the {startStr} and {endStr}";
        }
        else if (useHourFormat)
        {
            startStr = FormatHour(rangeStart);
            endStr = FormatHour(rangeEnd);
            betweenPart = $"between {startStr} and {endStr}";
        }
        else
        {
            startStr = rangeStart.ToString();
            endStr = rangeEnd.ToString();
            betweenPart = $"between {startStr} and {endStr}";
        }
        parts.Add(betweenPart);

        // Scope part: "of each hour" or "of each day" or "of each month"
        parts.Add($"of each {scopeUnit}");

        // Add month specifier if present
        if (spec.Month is not MonthSpecifier.None)
        {
            var monthPart = FormatMonthSpecifier(spec.Month);
            parts.Add(monthPart);
        }

        // Add year constraint if present
        if (spec.Year.HasValue)
        {
            parts.Add($"in year {spec.Year.Value}");
        }

        return string.Join(" ", parts);
    }

    private static string FormatInterval(int interval, IntervalUnit unit)
    {
        // For interval = 1, use singular unit name without the number
        // "every day" not "every 1 day"
        // For interval > 1, include the number and use plural
        // "every 30 minutes"

        var unitName = unit switch
        {
            IntervalUnit.Seconds => interval == 1 ? "second" : "seconds",
            IntervalUnit.Minutes => interval == 1 ? "minute" : "minutes",
            IntervalUnit.Hours => interval == 1 ? "hour" : "hours",
            IntervalUnit.Days => interval == 1 ? "day" : "days",
            IntervalUnit.Weeks => interval == 1 ? "week" : "weeks",
            IntervalUnit.Months => interval == 1 ? "month" : "months",
            IntervalUnit.Years => interval == 1 ? "year" : "years",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Invalid interval unit")
        };

        return interval == 1 ? unitName : $"{interval} {unitName}";
    }

    private static string FormatMonthSpecifier(MonthSpecifier monthSpec)
    {
        return monthSpec switch
        {
            MonthSpecifier.None => string.Empty,
            MonthSpecifier.Single single => $"in {MonthNumberToName[single.Month]}",
            MonthSpecifier.Range range => $"between {MonthNumberToName[range.Start]} and {MonthNumberToName[range.End]}",
            MonthSpecifier.List list => $"in {CompactMonthList(list.Months)}",
            _ => throw new InvalidOperationException($"Unknown month specifier type: {monthSpec.GetType().Name}")
        };
    }

    /// <summary>
    /// Format month list with compact range notation: "january-march,july,october-december"
    /// Uses ranges for 3+ consecutive months, individual names otherwise
    /// </summary>
    private static string CompactMonthList(IReadOnlyList<int> months)
    {
        if (months.Count == 0)
        {
            return "*";
        }

        List<string> parts = [];
        var i = 0;

        while (i < months.Count)
        {
            var start = months[i];
            var end = start;

            // Find consecutive sequence
            while (i + 1 < months.Count && months[i + 1] == end + 1)
            {
                i++;
                end = months[i];
            }

            // Use range notation for 3+ consecutive months, otherwise list individual month names
            var sequenceLength = end - start + 1;
            if (sequenceLength >= 3)
            {
                parts.Add($"{MonthNumberToName[start]}-{MonthNumberToName[end]}");
            }
            else
            {
                // Add individual month names (1 or 2 consecutive months)
                for (var j = start; j <= end; j++)
                {
                    parts.Add(MonthNumberToName[j]);
                }
            }

            i++;
        }

        return string.Join(",", parts);
    }

    private static string FormatTime(TimeOnly time)
    {
        // Format as 12-hour with am/pm for whole hours, 24-hour for fractional hours
        if (time.Minute == 0)
        {
            return $"at {FormatHour(time.Hour)}";
        }
        else
        {
            return $"at {time:HH:mm}";
        }
    }

    /// <summary>
    /// Format hour as 12-hour time with am/pm (e.g., "9am", "5pm", "12pm")
    /// </summary>
    private static string FormatHour(int hour)
    {
        if (hour == 0) return "12am";
        if (hour < 12) return $"{hour}am";
        if (hour == 12) return "12pm";
        return $"{hour - 12}pm";
    }

    /// <summary>
    /// Format a number as an ordinal (1st, 2nd, 3rd, 15th, 21st, etc.)
    /// </summary>
    private static string FormatOrdinal(int number)
    {
        // Special cases: 11th, 12th, 13th (not 11st, 12nd, 13rd)
        return number switch
        {
            11 or 12 or 13 => $"{number}th",
            _ when number % 10 == 1 => $"{number}st",
            _ when number % 10 == 2 => $"{number}nd",
            _ when number % 10 == 3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    /// <summary>
    /// Compact a list of values by converting consecutive sequences to ranges
    /// Example: [0,1,2,3,4,8,9,10,11,12,20] → "0-4,8-12,20"
    /// Sequences of 3+ consecutive values are converted to ranges for compactness
    /// </summary>
    private static string CompactList(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return "*";
        }

        List<string> parts = [];
        var i = 0;

        while (i < values.Count)
        {
            var start = values[i];
            var end = start;

            // Find consecutive sequence
            while (i + 1 < values.Count && values[i + 1] == end + 1)
            {
                i++;
                end = values[i];
            }

            // Use range notation for 3+ consecutive values, otherwise list individual values
            var sequenceLength = end - start + 1;
            if (sequenceLength >= 3)
            {
                parts.Add($"{start}-{end}");
            }
            else
            {
                // Add individual values (1 or 2 consecutive values)
                for (var j = start; j <= end; j++)
                {
                    parts.Add(j.ToString());
                }
            }

            i++;
        }

        return string.Join(",", parts);
    }

    /// <summary>
    /// Determine which day formatting strategy to use based on the schedule spec
    /// Encodes precedence rules in one place for clarity and maintainability
    /// </summary>
    private static DayFormatStrategy DetermineDayFormatStrategy(
        ScheduleSpec spec,
        IntervalUnit effectiveUnit,
        bool isMonthlyWithSingleMonth)
    {
        // PRIORITY 1: Compact day list with ranges (3+ consecutive values)
        // Example: "on the 1-7,15-21,30"
        // Skip if DayList is redundant with DayOfMonth (parser sets both for single ordinals)
        if (spec.DayList is { Count: > 0 } dayList)
        {
            // If DayList has only one value that equals DayOfMonth, skip it (redundant)
            if (dayList.Count == 1 && spec.DayOfMonth.HasValue && dayList[0] == spec.DayOfMonth.Value)
            {
                // Fall through to check other strategies
            }
            else
            {
                var compactStr = CompactList(dayList);
                if (compactStr.Contains('-'))
                {
                    return new DayFormatStrategy.CompactList(dayList);
                }
                return new DayFormatStrategy.OrdinalList(dayList);
            }
        }

        // PRIORITY 2: Combined month+day syntax for yearly schedules
        // Example: "on january 15th" (more natural than "on the 15th in january")
        // Only use if no DayList (already checked above)
        if (effectiveUnit == IntervalUnit.Years
            && spec.Month is MonthSpecifier.Single single
            && spec.DayOfMonth.HasValue)
        {
            return new DayFormatStrategy.CombinedMonthDay(single.Month, spec.DayOfMonth.Value);
        }

        // PRIORITY 3: Day range with ordinals
        // Example: "between the 1st and 15th"
        if (spec is { DayStart: not null, DayEnd: not null })
        {
            return new DayFormatStrategy.DayRange(spec.DayStart.Value, spec.DayEnd.Value);
        }

        // PRIORITY 4: Single day-of-month
        // Example: "on the 15th"
        if (spec.DayOfMonth.HasValue)
        {
            return new DayFormatStrategy.SingleOrdinal(spec.DayOfMonth.Value);
        }

        // No day constraint
        return new DayFormatStrategy.None();
    }

    /// <summary>
    /// Format a day formatting strategy into natural language
    /// Uses exhaustive switch expression - compiler ensures all cases are handled
    /// </summary>
    private string FormatDayStrategy(DayFormatStrategy strategy)
    {
        return strategy switch
        {
            DayFormatStrategy.CompactList(var days) =>
                $"on the {CompactList(days)}",

            DayFormatStrategy.OrdinalList(var days) =>
                FormatOrdinalDayList(days),

            DayFormatStrategy.CombinedMonthDay(var month, var day) =>
                $"on {MonthNumberToName[month]} {FormatOrdinal(day)}",

            DayFormatStrategy.SingleOrdinal(var day) =>
                $"on the {FormatOrdinal(day)}",

            DayFormatStrategy.DayRange(var start, var end) =>
                $"between the {FormatOrdinal(start)} and {FormatOrdinal(end)}",

            DayFormatStrategy.None => "",

            // Compiler error if we add a new case and forget to handle it!
            _ => throw new InvalidOperationException($"Unknown day format strategy: {strategy.GetType().Name}")
        };
    }

    /// <summary>
    /// Format ordinal day list with proper conjunction handling
    /// Examples: "on the 1st", "on the 1st and 15th", "on the 1st, 15th, 30th"
    /// </summary>
    private static string FormatOrdinalDayList(IReadOnlyList<int> days)
    {
        var ordinals = days.Select(FormatOrdinal).ToList();
        string dayListStr = ordinals.Count switch
        {
            1 => ordinals[0],
            2 => string.Join(" and ", ordinals),
            _ => string.Join(", ", ordinals)
        };
        return $"on the {dayListStr}";
    }
}
