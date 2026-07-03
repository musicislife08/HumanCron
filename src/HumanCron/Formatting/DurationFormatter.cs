using NodaTime;
using System;
using System.Collections.Generic;

namespace HumanCron.Formatting;

/// <summary>
/// Formats a NodaTime Period as a natural language duration (e.g. "2 hours 30 minutes").
/// INTERNAL: shared by Layer 1 (HumanDurationConverter.FormatDuration) and Layer 2
/// (HumanDurationConverter.ToNaturalDuration) — the only difference is whether the
/// incoming Period ever has nonzero Years/Months.
/// </summary>
internal static class DurationFormatter
{
    internal static string Format(Period period)
    {
        ArgumentNullException.ThrowIfNull(period);

        var isNegative = period.Years < 0 || period.Months < 0 || period.Weeks < 0 || period.Days < 0
            || period.Hours < 0 || period.Minutes < 0 || period.Seconds < 0 || period.Milliseconds < 0;

        var magnitude = isNegative ? -period : period;

        // Period.Normalize() always reports Weeks as 0 (folded into Days), and
        // Period.Between(...) doesn't populate Weeks by default either — so this
        // fold-then-split is required regardless of where the Period came from.
        var totalDays = magnitude.Weeks * 7 + magnitude.Days;
        var weeks = totalDays / 7;
        var days = totalDays % 7;

        var parts = new List<string>();
        AppendPart(parts, magnitude.Years, "year", "years");
        AppendPart(parts, magnitude.Months, "month", "months");
        AppendPart(parts, weeks, "week", "weeks");
        AppendPart(parts, days, "day", "days");
        AppendPart(parts, magnitude.Hours, "hour", "hours");
        AppendPart(parts, magnitude.Minutes, "minute", "minutes");
        AppendPart(parts, magnitude.Seconds, "second", "seconds");
        AppendPart(parts, magnitude.Milliseconds, "millisecond", "milliseconds");

        if (parts.Count == 0)
        {
            return "";
        }

        var body = string.Join(" ", parts);
        return isNegative ? $"-{body}" : body;
    }

    private static void AppendPart(List<string> parts, long value, string singular, string plural)
    {
        if (value == 0)
        {
            return;
        }

        parts.Add(value == 1 ? $"{value} {singular}" : $"{value} {plural}");
    }
}
