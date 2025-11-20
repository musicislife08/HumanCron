using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Combined month and day patterns: "on january 1st", "on dec 25th", "on april 15th"
    /// Natural syntax for specifying both month and day-of-month together
    /// </summary>
    [GeneratedRegex(@"on\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\s+(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase)]
    private static partial Regex MonthAndDayPattern();

    /// <summary>
    /// Day-of-month patterns: "on 15", "on 1", "on 31" or "on the 15th", "on the 1st"
    /// Used with monthly intervals
    /// </summary>
    [GeneratedRegex(@"on\s+(?:the\s+)?(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfMonthPattern();

    /// <summary>
    /// Minute list patterns: "at minutes 0,15,30,45" or "at minutes 0-2,4,6-8"
    /// </summary>
    [GeneratedRegex(@"at\s+minutes\s+([\d,\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MinuteListPattern();

    /// <summary>
    /// Hour list patterns: "at hours 9,12,15,18" or "at hours 9-17"
    /// </summary>
    [GeneratedRegex(@"at\s+hours\s+([\d,\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HourListPattern();

    /// <summary>
    /// Day list patterns: "on the 1st, 15th, and 30th" or "on the 1st,15th,30th"
    /// </summary>
    [GeneratedRegex(@"on\s+the\s+([\d,\s]+(st|nd|rd|th)[,\s]*)+", RegexOptions.IgnoreCase)]
    private static partial Regex DayListWithOrdinalsPattern();

    /// <summary>
    /// Minute range patterns: "between minutes 0 and 30"
    /// </summary>
    [GeneratedRegex(@"between\s+minutes\s+(\d+)\s+and\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MinuteRangePattern();

    /// <summary>
    /// Hour range patterns: "between hours 9 and 17"
    /// </summary>
    [GeneratedRegex(@"between\s+hours\s+(\d+)\s+and\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex HourRangePattern();

    /// <summary>
    /// Day range patterns with ordinals: "between the 1st and 15th"
    /// </summary>
    [GeneratedRegex(@"between\s+the\s+(\d+)(st|nd|rd|th)\s+and\s+(\d+)(st|nd|rd|th)", RegexOptions.IgnoreCase)]
    private static partial Regex DayRangeWithOrdinalsPattern();

    /// <summary>
    /// Range+step patterns: "every 5 minutes between 0 and 30 of each hour"
    /// </summary>
    [GeneratedRegex(@"every\s+(\d+)\s+(minutes?|hours?|days?)\s+between\s+(?:the\s+)?(\d+)(?:st|nd|rd|th)?\s+and\s+(?:the\s+)?(\d+)(?:st|nd|rd|th)?\s+of\s+each\s+(hour|day|month)", RegexOptions.IgnoreCase)]
    private static partial Regex RangeStepPattern();

    /// <summary>
    /// Year constraint patterns: "in year 2025"
    /// </summary>
    [GeneratedRegex(@"in\s+year\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex YearPattern();

    /// <summary>
    /// Last day patterns: "last day", "last day of month"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?last\s+day(?:\s+of\s+month)?", RegexOptions.IgnoreCase)]
    private static partial Regex LastDayPattern();

    /// <summary>
    /// Last weekday pattern: "last weekday"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?last\s+weekday", RegexOptions.IgnoreCase)]
    private static partial Regex LastWeekdayPattern();

    /// <summary>
    /// Last day-of-week patterns: "last monday", "last friday"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?last\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)", RegexOptions.IgnoreCase)]
    private static partial Regex LastDayOfWeekPattern();

    /// <summary>
    /// Last day offset patterns: "3rd to last day", "day before last"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?(?:(\d+)(?:st|nd|rd|th)\s+to\s+last\s+day|day\s+before\s+last)", RegexOptions.IgnoreCase)]
    private static partial Regex LastDayOffsetPattern();

    /// <summary>
    /// Weekday nearest patterns: "weekday nearest 15", "weekday nearest the 15th", "on weekday nearest the 1st"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?weekday\s+nearest\s+(?:the\s+)?(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase)]
    private static partial Regex WeekdayNearestPattern();

    /// <summary>
    /// Nth occurrence patterns: "1st monday", "3rd friday", "2nd thursday"
    /// </summary>
    [GeneratedRegex(@"(?:on\s+)?(\d+)(?:st|nd|rd|th)\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)", RegexOptions.IgnoreCase)]
    private static partial Regex NthOccurrencePattern();

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

    /// <summary>
    /// Parse ordinal strings like "1st", "2nd", "3rd", "15th" to integers
    /// </summary>
    private static int? ParseOrdinal(string ordinalStr)
    {
        if (string.IsNullOrWhiteSpace(ordinalStr))
        {
            return null;
        }

        // Strip ordinal suffix (st, nd, rd, th) - case insensitive
        var numberStr = Regex.Replace(ordinalStr.Trim(), @"(st|nd|rd|th)$", "", RegexOptions.IgnoreCase);
        return int.TryParse(numberStr, out var num) ? num : null;
    }

    /// <summary>
    /// Parse a list/range notation like "0,15,30,45" or "0-2,4,6-8" into individual values
    /// Similar to UnixCronParser.ParseList but for natural language input
    /// </summary>
    private static IReadOnlyList<int>? ParseListNotation(string notation, int minValue, int maxValue)
    {
        if (string.IsNullOrWhiteSpace(notation))
        {
            return null;
        }

        var parts = notation.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>();

        foreach (var part in parts)
        {
            // Check if this part is a range (e.g., "0-4")
            if (part.Contains('-') && !part.StartsWith("-"))
            {
                var rangeParts = part.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out var start) &&
                    int.TryParse(rangeParts[1], out var end) &&
                    start <= end &&
                    start >= minValue && end <= maxValue)
                {
                    // Expand range: 0-4 → [0, 1, 2, 3, 4]
                    for (var i = start; i <= end; i++)
                    {
                        values.Add(i);
                    }
                }
            }
            // Single value
            else if (int.TryParse(part, out var value) && value >= minValue && value <= maxValue)
            {
                values.Add(value);
            }
        }

        return values.Count >= 1 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }

    /// <summary>
    /// Parse ordinal list like "1st, 15th, and 30th" to list of integers
    /// </summary>
    private static IReadOnlyList<int>? ParseOrdinalList(string ordinalList)
    {
        if (string.IsNullOrWhiteSpace(ordinalList))
        {
            return null;
        }

        // Match all ordinals in the string (e.g., "1st", "15th", "30th")
        var ordinalMatches = Regex.Matches(ordinalList, @"(\d+)(st|nd|rd|th)", RegexOptions.IgnoreCase);
        var values = new List<int>();

        foreach (Match match in ordinalMatches)
        {
            var ordinal = ParseOrdinal(match.Value);
            if (ordinal is >= 1 and <= 31)
            {
                values.Add(ordinal.Value);
            }
        }

        return values.Count >= 1 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }

    /// <summary>
    /// Parse range+step patterns: "every 5 minutes between 0 and 30 of each hour"
    /// Regex groups: (1)=step (2)=unit (3)=start (4)=end (5)=scope
    /// </summary>
    private ParseResult<ScheduleSpec> ParseRangeStepPattern(Match match, string fullInput, ScheduleParserOptions options)
    {
        // Extract values from regex
        var step = int.Parse(match.Groups[1].Value);
        var unitStr = match.Groups[2].Value.ToLowerInvariant();
        var rangeStart = int.Parse(match.Groups[3].Value);
        var rangeEnd = int.Parse(match.Groups[4].Value);
        var scopeStr = match.Groups[5].Value.ToLowerInvariant();

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
    /// Extract month specifier from input string (helper for various parse methods)
    /// </summary>
    private MonthSpecifier ExtractMonthSpecifier(string input)
    {
        // Check for month list first (highest priority: "in jan,apr,jul,oct")
        var monthListMatch = MonthListPattern().Match(input);
        if (monthListMatch.Success)
        {
            var monthListString = monthListMatch.Groups[1].Value;
            var monthStrings = monthListString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    public ParseResult<ScheduleSpec> Parse(string naturalLanguage, ScheduleParserOptions options)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<ScheduleSpec>.Error("Input cannot be empty");
        }

        var input = naturalLanguage.Trim();

        // Check if this is an "on" pattern (for advanced Quartz features with months)
        var isOnPattern = input.StartsWith("on ", StringComparison.OrdinalIgnoreCase);

        // Validate "every" or "on" is present
        if (!input.StartsWith("every", StringComparison.OrdinalIgnoreCase) && !isOnPattern)
        {
            return new ParseResult<ScheduleSpec>.Error("All schedules must start with 'every' or 'on'. For example: 'every 30 minutes', 'every day', 'on last day in january'");
        }

        // Check for range+step pattern FIRST (highest priority, most specific)
        // "every 5 minutes between 0 and 30 of each hour"
        var rangeStepMatch = RangeStepPattern().Match(input);
        if (rangeStepMatch.Success)
        {
            return ParseRangeStepPattern(rangeStepMatch, input, options);
        }

        // Check for specific day patterns first (e.g., "every monday", "every weekday")
        // These don't have explicit interval units, so they need special handling
        var specificDayMatch = SpecificDayPattern().Match(input);

        // Extract interval: "every 30 seconds", "every day", or "every monday"
        var intervalMatch = IntervalPattern().Match(input);

        if (!intervalMatch.Success && !specificDayMatch.Success && !isOnPattern)
        {
            return new ParseResult<ScheduleSpec>.Error($"Unable to parse interval from: {input}. Expected format like 'every 30 minutes', 'every day', or 'every monday'");
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
                    return new ParseResult<ScheduleSpec>.Error($"Invalid interval number: {intervalSpan.ToString()}");
                }
            }

            switch (interval)
            {
                // Validate interval is positive
                case <= 0:
                    return new ParseResult<ScheduleSpec>.Error("Interval must be a positive number (1 or greater)");
                // Validate interval has reasonable upper bound
                case > 1000:
                    return new ParseResult<ScheduleSpec>.Error($"Interval too large: {interval}. Maximum allowed is 1000.");
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

        // Advanced Quartz features
        var isLastDay = false;
        var isLastDayOfWeek = false;
        int? lastDayOffset = null;
        var isNearestWeekday = false;
        int? nthOccurrence = null;

        // For monthly intervals, "on" means day-of-month (1-31) or advanced features
        if (unit == IntervalUnit.Months || unit == IntervalUnit.Years)
        {
            // Check for advanced features first (higher priority)

            // Last weekday: "last weekday"
            var lastWeekdayMatch = LastWeekdayPattern().Match(input);
            if (lastWeekdayMatch.Success)
            {
                isLastDay = true;
                isNearestWeekday = true;
            }
            // Last day offset: "3rd to last day", "day before last"
            else if (LastDayOffsetPattern().Match(input) is { Success: true } lastOffsetMatch)
            {
                if (lastOffsetMatch.Groups[1].Success)
                {
                    // "3rd to last day"
                    if (!int.TryParse(lastOffsetMatch.Groups[1].ValueSpan, out var offset))
                    {
                        return new ParseResult<ScheduleSpec>.Error($"Invalid offset: {lastOffsetMatch.Groups[1].Value}");
                    }
                    lastDayOffset = offset;
                }
                else
                {
                    // "day before last"
                    lastDayOffset = 1;
                }
            }
            // Last day: "last day", "last day of month"
            else if (LastDayPattern().Match(input).Success)
            {
                isLastDay = true;
            }
            // Last day-of-week: "last monday", "last friday"
            else if (LastDayOfWeekPattern().Match(input) is { Success: true } lastDayOfWeekMatch)
            {
                var dayString = lastDayOfWeekMatch.Groups[1].Value;
                if (!DayNames.TryGetValue(dayString, out var parsedDay))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day: {dayString}");
                }
                dayOfWeek = parsedDay;
                isLastDayOfWeek = true;
            }
            // Weekday nearest: "weekday nearest 15"
            else if (WeekdayNearestPattern().Match(input) is { Success: true } weekdayNearestMatch)
            {
                if (!int.TryParse(weekdayNearestMatch.Groups[1].ValueSpan, out var day))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day: {weekdayNearestMatch.Groups[1].Value}");
                }
                if (day < 1 || day > 31)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Day for weekday nearest must be 1-31, got: {day}");
                }
                dayOfMonth = day;
                isNearestWeekday = true;
            }
            // Nth occurrence: "1st monday", "3rd friday"
            else if (NthOccurrencePattern().Match(input) is { Success: true } nthMatch)
            {
                if (!int.TryParse(nthMatch.Groups[1].ValueSpan, out var nth))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid occurrence number: {nthMatch.Groups[1].Value}");
                }
                if (nth < 1 || nth > 5)
                {
                    return new ParseResult<ScheduleSpec>.Error($"Occurrence number must be 1-5, got: {nth}");
                }

                var dayString = nthMatch.Groups[2].Value;
                if (!DayNames.TryGetValue(dayString, out var parsedDay))
                {
                    return new ParseResult<ScheduleSpec>.Error($"Invalid day: {dayString}");
                }

                nthOccurrence = nth;
                dayOfWeek = parsedDay;
            }
            // Day-of-week pattern: "on monday" (when combined with monthly, e.g., "every month on monday")
            else if (DayOfWeekPattern().Match(input) is { Success: true } dowMatch)
            {
                var dayString = dowMatch.Groups[1].Value.ToLowerInvariant();

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
            // Regular day-of-month: "on 15"
            else
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

                switch (startDayOfWeek)
                {
                    // Check for recognized day ranges
                    case DayOfWeek.Monday when endDayOfWeek == DayOfWeek.Friday:
                        dayPattern = DayPattern.Weekdays;
                        break;
                    case DayOfWeek.Saturday when endDayOfWeek == DayOfWeek.Sunday:
                        dayPattern = DayPattern.Weekends;
                        break;
                    default:
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

        // Check for combined month+day pattern first: "on january 1st", "on dec 25th"
        // This is more specific than separate month and day patterns
        var monthAndDayMatch = MonthAndDayPattern().Match(input);
        MonthSpecifier monthSpecifier = new MonthSpecifier.None();

        if (monthAndDayMatch.Success)
        {
            var monthString = monthAndDayMatch.Groups[1].Value;
            var dayString = monthAndDayMatch.Groups[2].Value;

            if (!MonthNames.TryGetValue(monthString, out var monthNum))
            {
                return new ParseResult<ScheduleSpec>.Error($"Invalid month name: {monthString}");
            }

            if (!int.TryParse(dayString, out var day) || day < 1 || day > 31)
            {
                return new ParseResult<ScheduleSpec>.Error($"Invalid day of month: {dayString}. Must be 1-31.");
            }

            monthSpecifier = new MonthSpecifier.Single(monthNum);
            dayOfMonth = day;
        }

        // Extract month specifier (optional) if not already set by combined pattern
        // Check for month list first (highest priority: "in jan,apr,jul,oct")
        var monthListMatch = MonthListPattern().Match(input);
        if (!monthAndDayMatch.Success && monthListMatch.Success)
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
        else if (!monthAndDayMatch.Success)
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

        // Extract minute/hour/day lists and ranges
        IReadOnlyList<int>? minuteList = null;
        int? minuteStart = null, minuteEnd = null;
        IReadOnlyList<int>? hourList = null;
        int? hourStart = null, hourEnd = null;
        IReadOnlyList<int>? dayList = null;
        int? dayStart = null, dayEnd = null;
        int? year = null;

        // Minute list: "at minutes 0,15,30,45" or "at minutes 0-2,4,6-8"
        var minuteListMatch = MinuteListPattern().Match(input);
        if (minuteListMatch.Success)
        {
            var notation = minuteListMatch.Groups[1].Value;
            minuteList = ParseListNotation(notation, 0, 59);
        }
        // Minute range: "between minutes 0 and 30"
        else
        {
            var minuteRangeMatch = MinuteRangePattern().Match(input);
            if (minuteRangeMatch.Success)
            {
                minuteStart = int.Parse(minuteRangeMatch.Groups[1].Value);
                minuteEnd = int.Parse(minuteRangeMatch.Groups[2].Value);
            }
        }

        // Hour list: "at hours 9,12,15,18"
        var hourListMatch = HourListPattern().Match(input);
        if (hourListMatch.Success)
        {
            var notation = hourListMatch.Groups[1].Value;
            hourList = ParseListNotation(notation, 0, 23);
        }
        // Hour range: "between hours 9 and 17"
        else
        {
            var hourRangeMatch = HourRangePattern().Match(input);
            if (hourRangeMatch.Success)
            {
                hourStart = int.Parse(hourRangeMatch.Groups[1].Value);
                hourEnd = int.Parse(hourRangeMatch.Groups[2].Value);
            }
        }

        // Day list with ordinals: "on the 1st, 15th, and 30th"
        var dayListMatch = DayListWithOrdinalsPattern().Match(input);
        if (dayListMatch.Success)
        {
            dayList = ParseOrdinalList(dayListMatch.Value);
        }
        // Day range with ordinals: "between the 1st and 15th"
        else
        {
            var dayRangeMatch = DayRangeWithOrdinalsPattern().Match(input);
            if (dayRangeMatch.Success)
            {
                var startOrdinal = dayRangeMatch.Groups[1].Value + dayRangeMatch.Groups[2].Value;
                var endOrdinal = dayRangeMatch.Groups[3].Value + dayRangeMatch.Groups[4].Value;
                dayStart = ParseOrdinal(startOrdinal);
                dayEnd = ParseOrdinal(endOrdinal);
            }
        }

        // Year constraint: "in year 2025"
        var yearMatch = YearPattern().Match(input);
        if (yearMatch.Success)
        {
            year = int.Parse(yearMatch.Groups[1].Value);
        }

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
            TimeZone = options.TimeZone,
            IsLastDay = isLastDay,
            IsLastDayOfWeek = isLastDayOfWeek,
            LastDayOffset = lastDayOffset,
            IsNearestWeekday = isNearestWeekday,
            NthOccurrence = nthOccurrence,
            MinuteList = minuteList,
            MinuteStart = minuteStart,
            MinuteEnd = minuteEnd,
            HourList = hourList,
            HourStart = hourStart,
            HourEnd = hourEnd,
            DayList = dayList,
            DayStart = dayStart,
            DayEnd = dayEnd,
            Year = year
        });
    }
}
