using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;

namespace HumanCron.Converters.Unix;

/// <summary>
/// UnixCronParser - Month parsing methods
/// </summary>
internal sealed partial class UnixCronParser
{
    private static MonthSpecifier ParseMonthSpecifier(string monthPart)
    {
        // Wildcard: all months
        if (monthPart == "*")
        {
            return new MonthSpecifier.None();
        }

        // Range: 1-3 or jan-mar (january through march)
        if (monthPart.Contains('-'))
        {
            var parts = monthPart.Split('-');
            if (parts.Length == 2)
            {
                var start = ParseMonthValue(parts[0]);
                var end = ParseMonthValue(parts[1]);

                if (start.HasValue && end.HasValue && start.Value < end.Value)
                {
                    return new MonthSpecifier.Range(start.Value, end.Value);
                }
            }
            // Invalid range - fall through to None
            return new MonthSpecifier.None();
        }

        // List: 1,4,7,10 or jan,apr,jul,oct (quarterly)
        if (monthPart.Contains(','))
        {
            var parts = monthPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            List<int> months = [];

            foreach (var part in parts)
            {
                var month = ParseMonthValue(part);
                if (month.HasValue)
                {
                    months.Add(month.Value);
                }
            }

            if (months.Count >= 2)
            {
                return new MonthSpecifier.List(months);
            }
            // Invalid list - fall through to None
            return new MonthSpecifier.None();
        }

        // Single month: 1 or jan (january)
        var singleMonth = ParseMonthValue(monthPart);
        if (singleMonth.HasValue)
        {
            return new MonthSpecifier.Single(singleMonth.Value);
        }

        // Default: all months
        return new MonthSpecifier.None();
    }

    /// <summary>
    /// Parse a month value from either numeric (1-12) or named (jan-dec) format
    /// Returns null only for empty/whitespace strings - otherwise returns parsed value (valid or invalid)
    /// Invalid values are accepted to document actual cron parser behavior (validation deferred)
    /// </summary>
    private static int? ParseMonthValue(string value)
    {
        // Empty or whitespace - can't parse
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Try numeric first - accept any integer (including negative, out-of-range)
        // This matches cron parser behavior: parse anything, validate later
        if (int.TryParse(value, out var numeric))
        {
            return numeric;
        }

        // Try named (case-insensitive)
        return value.ToLowerInvariant() switch
        {
            "jan" => 1,
            "feb" => 2,
            "mar" => 3,
            "apr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "aug" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            "dec" => 12,
            _ => null  // Not a number, not a valid name - can't parse
        };
    }
}
