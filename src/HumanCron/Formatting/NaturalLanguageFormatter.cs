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

        // Start with "every"
        List<string> parts = ["every"];

        // Add interval + unit (verbose form)
        var intervalPart = FormatInterval(spec.Interval, spec.Unit);
        parts.Add(intervalPart);

        // Add day-of-week constraint (e.g., "every monday" or "between monday and friday")
        if (spec.DayOfWeek.HasValue)
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

        // Add month specifier (e.g., "in january", "between january and march")
        if (spec.Month is not MonthSpecifier.None)
        {
            var monthPart = FormatMonthSpecifier(spec.Month);
            parts.Add(monthPart);
        }

        // Add day-of-month constraint (e.g., "on 15")
        if (spec.DayOfMonth.HasValue)
        {
            parts.Add($"on {spec.DayOfMonth.Value}");
        }

        // Add time-of-day constraint (e.g., "at 2pm", "at 14:30")
        if (spec.TimeOfDay.HasValue)
        {
            var timePart = FormatTime(spec.TimeOfDay.Value);
            parts.Add(timePart);
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
            MonthSpecifier.List list => $"in {string.Join(",", list.Months.Select(m => MonthNumberToName[m]))}",
            _ => throw new InvalidOperationException($"Unknown month specifier type: {monthSpec.GetType().Name}")
        };
    }

    private static string FormatTime(TimeOnly time)
    {
        // Format as 12-hour with am/pm for whole hours, 24-hour for fractional hours
        if (time.Minute == 0)
        {
            var hour12 = time.Hour == 0 ? 12 : time.Hour > 12 ? time.Hour - 12 : time.Hour;
            var ampm = time.Hour >= 12 ? "pm" : "am";
            return $"at {hour12}{ampm}";
        }
        else
        {
            return $"at {time:HH:mm}";
        }
    }
}
