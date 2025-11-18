using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HumanCron.Abstractions;
using HumanCron.Models;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// </summary>
internal sealed partial class NaturalLanguageParser : IScheduleParser
{
    /// <summary>
    /// Interval patterns: "every 30 seconds", "every 15 minutes", "every day"
    /// Supports both plural and singular forms
    /// </summary>
    [GeneratedRegex(@"every\s+(\d+)?\s*(second|seconds|minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)", RegexOptions.IgnoreCase)]
    private static partial Regex IntervalPattern();

    /// <summary>
    /// Time patterns: "at 2pm", "at 14:00", "at 3:30am"
    /// Supports both 12-hour (with am/pm) and 24-hour formats
    /// </summary>
    [GeneratedRegex(@"at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    /// <summary>
    /// Specific day patterns: "every monday", "every weekday", "every weekend"
    /// Accepts both full names and abbreviations (mon, tue, etc.)
    /// </summary>
    [GeneratedRegex(@"every\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun|weekday|weekdays|weekend|weekends)", RegexOptions.IgnoreCase)]
    private static partial Regex SpecificDayPattern();

    /// <summary>
    /// Day-of-week patterns with "on": "on monday", "on weekdays", "on weekends"
    /// Used within longer patterns like "every hour on monday"
    /// </summary>
    [GeneratedRegex(@"on\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun|weekday|weekdays|weekend|weekends)", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfWeekPattern();

    /// <summary>
    /// Day range patterns: "between monday and friday", "between mon and fri"
    /// </summary>
    [GeneratedRegex(@"between\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)\s+and\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)", RegexOptions.IgnoreCase)]
    private static partial Regex DayRangePattern();

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
    /// Day-of-month patterns: "on 15", "on 1", "on 31"
    /// Used with monthly intervals
    /// </summary>
    [GeneratedRegex(@"on\s+(\d{1,2})", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfMonthPattern();

    // Month name to number mappings (accepts both full names and abbreviations)
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["january"] = 1, ["jan"] = 1,
        ["february"] = 2, ["feb"] = 2,
        ["march"] = 3, ["mar"] = 3,
        ["april"] = 4, ["apr"] = 4,
        ["may"] = 5,
        ["june"] = 6, ["jun"] = 6,
        ["july"] = 7, ["jul"] = 7,
        ["august"] = 8, ["aug"] = 8,
        ["september"] = 9, ["sep"] = 9,
        ["october"] = 10, ["oct"] = 10,
        ["november"] = 11, ["nov"] = 11,
        ["december"] = 12, ["dec"] = 12
    };

    // Day name to DayOfWeek mappings (accepts both full names and abbreviations)
    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monday"] = DayOfWeek.Monday, ["mon"] = DayOfWeek.Monday,
        ["tuesday"] = DayOfWeek.Tuesday, ["tue"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
        ["thursday"] = DayOfWeek.Thursday, ["thu"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday, ["fri"] = DayOfWeek.Friday,
        ["saturday"] = DayOfWeek.Saturday, ["sat"] = DayOfWeek.Saturday,
        ["sunday"] = DayOfWeek.Sunday, ["sun"] = DayOfWeek.Sunday
    };

    public ParseResult<ScheduleSpec> Parse(string naturalLanguage, ScheduleParserOptions options)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<ScheduleSpec>.Error("Input cannot be empty");
        }

        var input = naturalLanguage.Trim();

        // Validate "every" is present (required for all patterns)
        if (!input.StartsWith("every", StringComparison.OrdinalIgnoreCase))
        {
            return new ParseResult<ScheduleSpec>.Error("All schedules must start with 'every'. For example: 'every 30 minutes', 'every day', 'every monday'");
        }

        // Check for specific day patterns first (e.g., "every monday", "every weekday")
        // These don't have explicit interval units, so they need special handling
        var specificDayMatch = SpecificDayPattern().Match(input);

        // Extract interval: "every 30 seconds", "every day", or "every monday"
        var intervalMatch = IntervalPattern().Match(input);

        if (!intervalMatch.Success && !specificDayMatch.Success)
        {
            return new ParseResult<ScheduleSpec>.Error($"Unable to parse interval from: {input}. Expected format like 'every 30 minutes', 'every day', or 'every monday'");
        }

        // Parse interval value (default to 1 if not specified)
        var interval = 1;
        IntervalUnit unit;

        if (specificDayMatch.Success && !intervalMatch.Success)
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
                    return new ParseResult<ScheduleSpec>.Error($"Invalid interval number: {intervalSpan.ToString()}");
                }
            }

            // Validate interval is positive
            if (interval <= 0)
            {
                return new ParseResult<ScheduleSpec>.Error("Interval must be a positive number (1 or greater)");
            }

            // Validate interval has reasonable upper bound
            if (interval > 1000)
            {
                return new ParseResult<ScheduleSpec>.Error($"Interval too large: {interval}. Maximum allowed is 1000.");
            }

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
        }

        // Extract time (optional): "at 2pm", "at 14:00", "at 3:30am"
        TimeOnly? timeOfDay = null;
        var timeMatch = TimePattern().Match(input);
        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].ValueSpan);
            var minute = timeMatch.Groups[2].Success ? int.Parse(timeMatch.Groups[2].ValueSpan) : 0;
            var amPm = timeMatch.Groups[3].Success ? timeMatch.Groups[3].Value : null;

            // Validate hour before conversion
            if (amPm != null)
            {
                // 12-hour format: hour must be 1-12
                if (hour < 1 || hour > 12)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid hour for 12-hour format: {hour} (must be 1-12)");
                }
            }
            else
            {
                // 24-hour format: hour must be 0-23
                if (hour < 0 || hour > 23)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid hour for 24-hour format: {hour} (must be 0-23)");
                }
            }

            // Validate minutes
            if (minute < 0 || minute > 59)
            {
                return new ParseResult<ScheduleSpec>.Error($"Invalid minutes: {minute} (must be 0-59)");
            }

            // Convert 12-hour to 24-hour if am/pm specified
            if (amPm != null)
            {
                if (string.Equals(amPm, "am", StringComparison.OrdinalIgnoreCase))
                {
                    if (hour == 12)
                        hour = 0;  // 12am = midnight = 00:00
                }
                else if (string.Equals(amPm, "pm", StringComparison.OrdinalIgnoreCase))
                {
                    if (hour != 12)
                        hour += 12;  // 1pm = 13:00, but 12pm stays 12:00
                }
            }

            timeOfDay = new TimeOnly(hour, minute);
        }

        // Extract day specifier (optional, context-aware)
        DayOfWeek? dayOfWeek = null;
        DayPattern? dayPattern = null;
        int? dayOfMonth = null;

        // For monthly intervals, "on" means day-of-month (1-31)
        if (unit == IntervalUnit.Months || unit == IntervalUnit.Years)
        {
            var dayOfMonthMatch = DayOfMonthPattern().Match(input);
            if (dayOfMonthMatch.Success)
            {
                var daySpan = dayOfMonthMatch.Groups[1].ValueSpan;
                if (!int.TryParse(daySpan, out var day))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day of month: {daySpan.ToString()}");
                }

                // Validate day of month is 1-31
                if (day < 1 || day > 31)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Day of month must be 1-31, got: {day}");
                }

                dayOfMonth = day;
            }
        }
        else
        {
            // For other intervals, check for day range first (higher priority)
            var dayRangeMatch = DayRangePattern().Match(input);
            if (dayRangeMatch.Success)
            {
                var startDay = dayRangeMatch.Groups[1].Value;
                var endDay = dayRangeMatch.Groups[2].Value;

                if (!DayNames.TryGetValue(startDay, out var startDayOfWeek))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day name: {startDay}");
                }

                if (!DayNames.TryGetValue(endDay, out var endDayOfWeek))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day name: {endDay}");
                }

                // Check for recognized day ranges
                if (startDayOfWeek == DayOfWeek.Monday && endDayOfWeek == DayOfWeek.Friday)
                {
                    dayPattern = DayPattern.Weekdays;
                }
                else if (startDayOfWeek == DayOfWeek.Saturday && endDayOfWeek == DayOfWeek.Sunday)
                {
                    dayPattern = DayPattern.Weekends;
                }
                else
                {
                    // TODO: Add DayRange support to ScheduleSpec for arbitrary ranges
                    return new ParseResult<ScheduleSpec>.Error($"Day ranges other than 'between monday and friday' (weekdays) or 'between saturday and sunday' (weekends) are not yet supported. Found: {startDay} to {endDay}");
                }
            }
            else
            {
                // Check if user tried to use day-of-month syntax with non-monthly interval
                var dayOfMonthMatch = DayOfMonthPattern().Match(input);
                if (dayOfMonthMatch.Success)
                {
                    var unitName = unit switch
                    {
                        IntervalUnit.Seconds => "seconds",
                        IntervalUnit.Minutes => "minutes",
                        IntervalUnit.Hours => "hours",
                        IntervalUnit.Days => "days",
                        IntervalUnit.Weeks => "weeks",
                        _ => unit.ToString()
                    };

                    return new ParseResult<ScheduleSpec>.Error(
                        $"Day-of-month (on {dayOfMonthMatch.Groups[1].ValueSpan.ToString()}) is only valid with monthly (month/months) or yearly (year/years) intervals, not {unitName}. " +
                        $"Did you mean to use a day-of-week instead? (e.g., 'every monday')");
                }

                // Check for day-of-week with "on" prefix (e.g., "every hour on monday")
                var dayOfWeekMatch = DayOfWeekPattern().Match(input);
                if (dayOfWeekMatch.Success || specificDayMatch.Success)
                {
                    // Use whichever pattern matched
                    var dayString = dayOfWeekMatch.Success
                        ? dayOfWeekMatch.Groups[1].Value.ToLowerInvariant()
                        : specificDayMatch.Groups[1].Value.ToLowerInvariant();

                    // Check for patterns first
                    if (dayString is "weekday" or "weekdays")
                    {
                        dayPattern = DayPattern.Weekdays;
                    }
                    else if (dayString is "weekend" or "weekends")
                    {
                        dayPattern = DayPattern.Weekends;
                    }
                    else if (DayNames.TryGetValue(dayString, out var parsedDay))
                    {
                        dayOfWeek = parsedDay;
                    }
                    else
                    {
                        return new ParseResult<ScheduleSpec>.Error($"Invalid day: {dayString}");
                    }
                }
            }
        }

        // Extract month specifier (optional)
        MonthSpecifier monthSpecifier = new MonthSpecifier.None();

        // Check for month list first (highest priority: "in jan,apr,jul,oct")
        var monthListMatch = MonthListPattern().Match(input);
        if (monthListMatch.Success)
        {
            var monthListString = monthListMatch.Groups[1].Value;
            var monthStrings = monthListString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            List<int> months = [];
            foreach (var monthStr in monthStrings)
            {
                if (!MonthNames.TryGetValue(monthStr, out var monthNum))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid month name: {monthStr}");
                }
                months.Add(monthNum);
            }

            if (months.Count < 2)
            {
                return new ParseResult<ScheduleSpec>.Error("Month list must contain at least 2 months");
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
                    return new ParseResult<ScheduleSpec>.Error($"Invalid month name: {startMonth}");
                }

                if (!MonthNames.TryGetValue(endMonth, out var endMonthNum))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid month name: {endMonth}");
                }

                if (startMonthNum >= endMonthNum)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Month range start ({startMonth}) must be before end ({endMonth})");
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
                        return new ParseResult<ScheduleSpec>.Error($"Invalid month name: {monthString}");
                    }

                    monthSpecifier = new MonthSpecifier.Single(monthNum);
                }
            }
        }

        // Note: We allow month intervals combined with month selection for patterns like:
        // "every month on 15 in january,april,july,october" = 15th of specific months
        // This maps to cron: "0 0 15 1,4,7,10 *"

        // Validate DayOfWeek and DayPattern are mutually exclusive (should not happen due to parsing logic, but defensive check)
        if (dayOfWeek.HasValue && dayPattern.HasValue)
        {
            return new ParseResult<ScheduleSpec>.Error(
                "Cannot specify both a specific day and a day pattern (internal parsing error)");
        }

        return new ParseResult<ScheduleSpec>.Success(new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfMonth = dayOfMonth,
            Month = monthSpecifier,
            TimeOfDay = timeOfDay,
            TimeZone = options.TimeZone
        });
    }
}
