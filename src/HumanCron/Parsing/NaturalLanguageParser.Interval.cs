using HumanCron.Models;
using HumanCron.Models.Internal;
using System;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Interval parsing logic (patterns, methods)
/// </summary>
internal sealed partial class NaturalLanguageParser
{
    // ===== INTERVAL-SPECIFIC PATTERNS =====

    /// <summary>
    /// Interval patterns: "every 30 seconds", "every 15 minutes", "every day"
    /// Supports both plural and singular forms
    /// </summary>
    [GeneratedRegex(@"every\s+(\d+)?\s*(second|seconds|minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)", RegexOptions.IgnoreCase)]
    private static partial Regex IntervalPattern();

    /// <summary>
    /// Range+step patterns: "every 5 minutes between 0 and 30 of each hour" or "every 2 hours between 9am and 5pm of each day"
    /// </summary>
    [GeneratedRegex(@"every\s+(\d+)\s+(minutes?|hours?|days?)\s+between\s+(?:the\s+)?(\d+)(am|pm)?(?:st|nd|rd|th)?\s+and\s+(?:the\s+)?(\d+)(am|pm)?(?:st|nd|rd|th)?\s+of\s+each\s+(hour|day|month)", RegexOptions.IgnoreCase)]
    private static partial Regex RangeStepPattern();

    // ===== INTERVAL PARSING METHODS =====

    /// <summary>
    /// Parse range+step patterns: "every 5 minutes between 0 and 30 of each hour" or "every 2 hours between 9am and 5pm of each day"
    /// Regex groups: (1)=step (2)=unit (3)=start (4)=start am/pm (5)=end (6)=end am/pm (7)=scope
    /// </summary>
    private ParseResult<ScheduleSpec> ParseRangeStepPattern(Match match, string fullInput, ScheduleParserOptions options)
    {
        // Extract values from regex
        var step = int.Parse(match.Groups[1].Value);
        var unitStr = match.Groups[2].Value.ToLowerInvariant();
        var startStr = match.Groups[3].Value;
        var startAmPm = match.Groups[4].Success ? match.Groups[4].Value : null;
        var endStr = match.Groups[5].Value;
        var endAmPm = match.Groups[6].Success ? match.Groups[6].Value : null;
        var scopeStr = match.Groups[7].Value.ToLowerInvariant();

        // Parse range start/end with am/pm support for hours
        int rangeStart, rangeEnd;
        if (unitStr is "hour" or "hours" && (startAmPm != null || endAmPm != null))
        {
            rangeStart = ParseHour(startStr, startAmPm);
            rangeEnd = ParseHour(endStr, endAmPm);
        }
        else
        {
            rangeStart = int.Parse(startStr);
            rangeEnd = int.Parse(endStr);
        }

        // Determine unit from step unit string
        var unit = unitStr switch
        {
            "minute" or "minutes" => IntervalUnit.Minutes,
            "hour" or "hours" => IntervalUnit.Hours,
            "day" or "days" => IntervalUnit.Days,
            _ => throw new InvalidOperationException($"Unknown unit: {unitStr}")
        };

        // Build ScheduleSpec based on the unit
        var spec = new ScheduleSpec
        {
            Interval = step,
            Unit = unit,
            TimeZone = options.TimeZone
        };

        spec = unit switch
        {
            // Set range fields based on unit
            IntervalUnit.Minutes => spec with { MinuteStart = rangeStart, MinuteEnd = rangeEnd, MinuteStep = step },
            IntervalUnit.Hours => spec with { HourStart = rangeStart, HourEnd = rangeEnd, HourStep = step },
            IntervalUnit.Days => spec with { DayStart = rangeStart, DayEnd = rangeEnd, DayStep = step },
            _ => spec
        };

        // Extract month specifier (from full input, not just the match)
        var monthSpecifier = ExtractMonthSpecifier(fullInput);
        spec = spec with { Month = monthSpecifier };

        // Extract year constraint (from full input)
        var yearMatch = YearPattern().Match(fullInput);
        if (!yearMatch.Success) return new ParseResult<ScheduleSpec>.Success(spec);
        var year = int.Parse(yearMatch.Groups[1].Value);
        spec = spec with { Year = year };

        return new ParseResult<ScheduleSpec>.Success(spec);
    }

    /// <summary>
    /// Parse interval and unit from natural language input
    /// Handles special cases like "on" patterns (implicitly monthly) and specific day patterns (implicitly weekly)
    /// </summary>
    private ParseResult<(int interval, IntervalUnit unit)> TryParseInterval(
        string input,
        bool isOnPattern,
        Match specificDayMatch)
    {
        // Extract interval: "every 30 seconds", "every day", or "every monday"
        var intervalMatch = IntervalPattern().Match(input);

        if (!intervalMatch.Success && !specificDayMatch.Success && !isOnPattern)
        {
            return new ParseResult<(int, IntervalUnit)>.Error(
                $"Unable to parse interval from: {input}. Expected format like 'every 30 minutes', 'every day', or 'every monday'");
        }

        // Parse interval value (default to 1 if not specified)
        var interval = 1;
        IntervalUnit unit;

        if (isOnPattern)
        {
            // Special case: "on" patterns are implicitly monthly
            // "on last day in january" = every month on last day in january
            interval = 1;
            unit = IntervalUnit.Months;
        }
        else if (specificDayMatch.Success && !intervalMatch.Success)
        {
            // Special case: "every monday" - no explicit interval, defaults to weekly
            interval = 1;
            unit = IntervalUnit.Weeks;
        }
        else
        {
            // Normal case: "every 30 minutes", "every day", etc.
            if (intervalMatch.Groups[1].Success && intervalMatch.Groups[1].ValueSpan.Trim().Length > 0)
            {
                var intervalSpan = intervalMatch.Groups[1].ValueSpan;
                if (!int.TryParse(intervalSpan, out interval))
                {
                    return new ParseResult<(int, IntervalUnit)>.Error($"Invalid interval number: {intervalSpan.ToString()}");
                }
            }

            switch (interval)
            {
                // Validate interval is positive
                case <= 0:
                    return new ParseResult<(int, IntervalUnit)>.Error("Interval must be a positive number (1 or greater)");
                // Validate interval has reasonable upper bound
                case > 1000:
                    return new ParseResult<(int, IntervalUnit)>.Error($"Interval too large: {interval}. Maximum allowed is 1000.");
                default:
                {
                    // Parse unit from full word
                    var unitString = intervalMatch.Groups[2].Value.ToLowerInvariant();
                    unit = unitString switch
                    {
                        "second" or "seconds" => IntervalUnit.Seconds,
                        "minute" or "minutes" => IntervalUnit.Minutes,
                        "hour" or "hours" => IntervalUnit.Hours,
                        "day" or "days" => IntervalUnit.Days,
                        "week" or "weeks" => IntervalUnit.Weeks,
                        "month" or "months" => IntervalUnit.Months,
                        "year" or "years" => IntervalUnit.Years,
                        _ => throw new InvalidOperationException($"Unknown unit: {unitString}")
                    };
                    break;
                }
            }
        }

        return new ParseResult<(int, IntervalUnit)>.Success((interval, unit));
    }
}
