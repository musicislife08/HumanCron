using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Month parsing methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    private static MonthSpecifier ParseMonth(ReadOnlySpan<char> monthPart)
    {
        // "*" means all months (no constraint)
        if (monthPart is "*")
        {
            return new MonthSpecifier.None();
        }

        // Month range: "1-3" or "jan-mar" (January through March)
        var dashIndex = monthPart.IndexOf('-');
        if (dashIndex > 0)
        {
            var startSpan = monthPart[..dashIndex];
            var endSpan = monthPart[(dashIndex + 1)..];

            var start = ParseMonthValue(startSpan);
            var end = ParseMonthValue(endSpan);

            if (start.HasValue && end.HasValue)
            {
                return new MonthSpecifier.Range(start.Value, end.Value);
            }
        }

        // Month list: "1,4,7,10" or "jan,apr,jul,oct" (quarterly)
        if (monthPart.Contains(','))
        {
            List<int> months = [];
            Span<Range> ranges = stackalloc Range[12]; // Max 12 months
            var count = monthPart.Split(ranges, ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = 0; i < count; i++)
            {
                var month = ParseMonthValue(monthPart[ranges[i]]);
                if (month.HasValue)
                {
                    months.Add(month.Value);
                }
            }

            if (months.Count > 0)
            {
                return new MonthSpecifier.List(months);
            }
        }

        // Single month: "1" or "jan" (January only)
        var singleMonth = ParseMonthValue(monthPart);
        if (singleMonth.HasValue)
        {
            return new MonthSpecifier.Single(singleMonth.Value);
        }

        // Default to None if we can't parse
        return new MonthSpecifier.None();
    }

    /// <summary>
    /// Parse a month value from either numeric (1-12) or named (jan-dec) format
    /// Returns null only for empty/whitespace strings - otherwise returns parsed value (valid or invalid)
    /// Invalid values are accepted to document actual cron parser behavior (validation deferred)
    /// </summary>
    private static int? ParseMonthValue(ReadOnlySpan<char> value)
    {
        // Empty or whitespace - can't parse
        if (value.IsEmpty || value.IsWhiteSpace())
        {
            return null;
        }

        // Try numeric first - accept any integer (including negative, out-of-range)
        // This matches cron parser behavior: parse anything, validate later
        if (int.TryParse(value, out var numeric))
        {
            return numeric;
        }

        // Try named (case-insensitive) - use Equals for Span comparison
        if (value.Equals("jan", StringComparison.OrdinalIgnoreCase)) return 1;
        if (value.Equals("feb", StringComparison.OrdinalIgnoreCase)) return 2;
        if (value.Equals("mar", StringComparison.OrdinalIgnoreCase)) return 3;
        if (value.Equals("apr", StringComparison.OrdinalIgnoreCase)) return 4;
        if (value.Equals("may", StringComparison.OrdinalIgnoreCase)) return 5;
        if (value.Equals("jun", StringComparison.OrdinalIgnoreCase)) return 6;
        if (value.Equals("jul", StringComparison.OrdinalIgnoreCase)) return 7;
        if (value.Equals("aug", StringComparison.OrdinalIgnoreCase)) return 8;
        if (value.Equals("sep", StringComparison.OrdinalIgnoreCase)) return 9;
        if (value.Equals("oct", StringComparison.OrdinalIgnoreCase)) return 10;
        if (value.Equals("nov", StringComparison.OrdinalIgnoreCase)) return 11;
        if (value.Equals("dec", StringComparison.OrdinalIgnoreCase)) return 12;

        // Not a number, not a valid name - can't parse
        return null;
    }
}
