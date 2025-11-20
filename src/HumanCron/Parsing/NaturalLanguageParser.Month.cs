using HumanCron.Models;
using HumanCron.Models.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Month constraint parsing logic (patterns, helpers, methods)
/// </summary>
internal sealed partial class NaturalLanguageParser
{
    // ===== MONTH-SPECIFIC PATTERNS =====

    /// <summary>
    /// Specific month patterns: "in january", "in jan"
    /// </summary>
    [GeneratedRegex(@"in\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SpecificMonthPattern();

    /// <summary>
    /// Month range patterns: "between january and march", "between jan and mar"
    /// </summary>
    [GeneratedRegex(@"between\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\s+and\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthRangePattern();

    /// <summary>
    /// Month list patterns: "in jan,apr,jul,oct" or "in january, april, july, october"
    /// </summary>
    [GeneratedRegex(@"in\s+((?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(?:\s*,\s*(?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec))+)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthListPattern();

    /// <summary>
    /// Month list compact notation: "in january-march,july,october-december" (ranges like day lists)
    /// Checked after regular month list pattern to avoid conflicts
    /// </summary>
    [GeneratedRegex(@"in\s+((?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(?:\s*[\-,]\s*(?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec))+)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthListCompactPattern();

    /// <summary>
    /// Combined month and day patterns: "on january 1st", "on dec 25th", "on april 15th"
    /// Natural syntax for specifying both month and day-of-month together
    /// </summary>
    [GeneratedRegex(@"on\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\s+(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase)]
    private static partial Regex MonthAndDayPattern();

    // ===== MONTH-SPECIFIC HELPERS =====

    /// <summary>
    /// Parse month range notation like "january-march,july,october-december" into month numbers
    /// Similar to ParseListNotation but for month names instead of numbers
    /// </summary>
    private static IReadOnlyList<int>? ParseMonthRangeNotation(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation))
        {
            return null;
        }

        var parts = notation.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        List<int> values = [];

        foreach (var part in parts)
        {
            // Check if this part is a range (e.g., "january-march")
            if (part.Contains('-') && !part.StartsWith("-"))
            {
                var rangeParts = part.Split('-', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (rangeParts.Length == 2 &&
                    MonthNames.TryGetValue(rangeParts[0], out var startMonth) &&
                    MonthNames.TryGetValue(rangeParts[1], out var endMonth) &&
                    startMonth <= endMonth)
                {
                    // Expand range: january-march → [1, 2, 3]
                    for (var i = startMonth; i <= endMonth; i++)
                    {
                        values.Add(i);
                    }
                }
            }
            // Single month name
            else if (MonthNames.TryGetValue(part, out var monthNum))
            {
                values.Add(monthNum);
            }
        }

        return values.Count >= 1 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }

    // ===== MONTH PARSING METHODS =====

    /// <summary>
    /// Extract month specifier from input string (helper for various parse methods)
    /// </summary>
    private MonthSpecifier ExtractMonthSpecifier(string input)
    {
        // Check for month list first (highest priority: "in jan,apr,jul,oct")
        var monthListMatch = MonthListPattern().Match(input);
        if (monthListMatch.Success)
        {
            var monthListString = monthListMatch.Groups[1].Value;
            var monthStrings = monthListString.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

            List<int> months = [];
            foreach (var monthStr in monthStrings)
            {
                if (MonthNames.TryGetValue(monthStr, out var monthNum))
                {
                    months.Add(monthNum);
                }
            }

            if (months.Count >= 2)
            {
                return new MonthSpecifier.List(months);
            }
        }

        // Check for month range (medium priority: "between january and march")
        var monthRangeMatch = MonthRangePattern().Match(input);
        if (monthRangeMatch.Success)
        {
            var startMonth = monthRangeMatch.Groups[1].Value;
            var endMonth = monthRangeMatch.Groups[2].Value;

            if (MonthNames.TryGetValue(startMonth, out var startMonthNum) &&
                MonthNames.TryGetValue(endMonth, out var endMonthNum))
            {
                return new MonthSpecifier.Range(startMonthNum, endMonthNum);
            }
        }

        // Check for single month (lowest priority: "in january")
        var monthMatch = SpecificMonthPattern().Match(input);
        if (!monthMatch.Success) return new MonthSpecifier.None();
        {
            var monthString = monthMatch.Groups[1].Value;
            if (MonthNames.TryGetValue(monthString, out var monthNum))
            {
                return new MonthSpecifier.Single(monthNum);
            }
        }

        return new MonthSpecifier.None();
    }

    /// <summary>
    /// Parse month-related constraints from natural language input
    /// Handles combined month+day patterns, month lists, month ranges, and single months
    /// Priority order: combined month+day > month list > month range > single month
    /// Also updates dayOfMonth if combined pattern is used
    /// </summary>
    private ParseResult<(MonthConstraints, int? updatedDayOfMonth)> TryParseMonthConstraints(
        string input,
        int? currentDayOfMonth)
    {
        MonthSpecifier monthSpecifier = new MonthSpecifier.None();
        int? dayOfMonth = currentDayOfMonth;

        // Check for combined month+day pattern first: "on january 1st", "on dec 25th"
        // This is more specific than separate month and day patterns
        var monthAndDayMatch = MonthAndDayPattern().Match(input);

        if (monthAndDayMatch.Success)
        {
            var monthString = monthAndDayMatch.Groups[1].Value;
            var dayString = monthAndDayMatch.Groups[2].Value;

            if (!MonthNames.TryGetValue(monthString, out var monthNum))
            {
                return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid month name: {monthString}");
            }

            if (!int.TryParse(dayString, out var day) || day < 1 || day > 31)
            {
                return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid day of month: {dayString}. Must be 1-31.");
            }

            monthSpecifier = new MonthSpecifier.Single(monthNum);
            dayOfMonth = day;
        }
        // Extract month specifier (optional) if not already set by combined pattern
        // Check for compact notation first (contains ranges: "january-march,july,october-december")
        else
        {
            var monthListCompactMatch = MonthListCompactPattern().Match(input);
            if (monthListCompactMatch.Success)
            {
                var notation = monthListCompactMatch.Groups[1].Value;
                var months = ParseMonthRangeNotation(notation);

                if (months == null || months.Count < 2)
                {
                    return new ParseResult<(MonthConstraints, int?)>.Error("Month list must contain at least 2 months");
                }

                monthSpecifier = new MonthSpecifier.List(months);
            }
            // Check for regular month list (no ranges: "in jan,apr,jul,oct")
            else
            {
                var monthListMatch = MonthListPattern().Match(input);
                if (monthListMatch.Success)
                {
                    var monthListString = monthListMatch.Groups[1].Value;
                    var monthStrings = monthListString.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

                    List<int> months = [];
                    foreach (var monthStr in monthStrings)
                    {
                        if (!MonthNames.TryGetValue(monthStr, out var monthNum))
                        {
                            return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid month name: {monthStr}");
                        }
                        months.Add(monthNum);
                    }

                    if (months.Count < 2)
                    {
                        return new ParseResult<(MonthConstraints, int?)>.Error("Month list must contain at least 2 months");
                    }

                    monthSpecifier = new MonthSpecifier.List(months);
                }
                else
                {
                    // Check for month range (medium priority: "between january and march")
                    var monthRangeMatch = MonthRangePattern().Match(input);
                    if (monthRangeMatch.Success)
                    {
                        var startMonth = monthRangeMatch.Groups[1].Value;
                        var endMonth = monthRangeMatch.Groups[2].Value;

                        if (!MonthNames.TryGetValue(startMonth, out var startMonthNum))
                        {
                            return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid month name: {startMonth}");
                        }

                        if (!MonthNames.TryGetValue(endMonth, out var endMonthNum))
                        {
                            return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid month name: {endMonth}");
                        }

                        if (startMonthNum >= endMonthNum)
                        {
                            return new ParseResult<(MonthConstraints, int?)>.Error($"Month range start ({startMonth}) must be before end ({endMonth})");
                        }

                        monthSpecifier = new MonthSpecifier.Range(startMonthNum, endMonthNum);
                    }
                    else
                    {
                        // Check for single month (lowest priority: "in january")
                        var monthMatch = SpecificMonthPattern().Match(input);
                        if (monthMatch.Success)
                        {
                            var monthString = monthMatch.Groups[1].Value;

                            if (!MonthNames.TryGetValue(monthString, out var monthNum))
                            {
                                return new ParseResult<(MonthConstraints, int?)>.Error($"Invalid month name: {monthString}");
                            }

                            monthSpecifier = new MonthSpecifier.Single(monthNum);
                        }
                    }
                }
            }
        }

        var monthConstraints = new MonthConstraints { Specifier = monthSpecifier };
        return new ParseResult<(MonthConstraints, int?)>.Success((monthConstraints, dayOfMonth));
    }
}
