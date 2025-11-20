using HumanCron.Models;
using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Day constraint parsing logic (patterns, helpers, methods)
/// </summary>
internal sealed partial class NaturalLanguageParser
{
    // ===== DAY-SPECIFIC PATTERNS =====

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
    /// Day-of-week list patterns: "every monday,wednesday,friday" or "every mon,wed,fri"
    /// </summary>
    [GeneratedRegex(@"every\s+((?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)(?:\s*,\s*(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun))+)", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfWeekListPattern();

    /// <summary>
    /// Custom day-of-week range patterns: "every tuesday-thursday" or "every tue-thu"
    /// Compact notation for arbitrary day ranges (not just weekdays/weekends)
    /// </summary>
    [GeneratedRegex(@"every\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)\s*-\s*(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfWeekCustomRangePattern();

    /// <summary>
    /// Day-of-month patterns: "on 15", "on 1", "on 31" or "on the 15th", "on the 1st"
    /// Used with monthly intervals
    /// </summary>
    [GeneratedRegex(@"on\s+(?:the\s+)?(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase)]
    private static partial Regex DayOfMonthPattern();

    /// <summary>
    /// Day list patterns: "on the 1st, 15th, and 30th" or "on the 1st,15th,30th"
    /// </summary>
    [GeneratedRegex(@"on\s+the\s+([\d,\s]+(st|nd|rd|th)[,\s]*)+", RegexOptions.IgnoreCase)]
    private static partial Regex DayListWithOrdinalsPattern();

    /// <summary>
    /// Day list compact notation: "on the 1-7,15-21,30" (numeric ranges like minute/hour lists)
    /// Checked after ordinal patterns to avoid conflicts
    /// </summary>
    [GeneratedRegex(@"on\s+the\s+([\d,\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DayListCompactNotationPattern();

    /// <summary>
    /// Day range patterns with ordinals: "between the 1st and 15th"
    /// </summary>
    [GeneratedRegex(@"between\s+the\s+(\d+)(st|nd|rd|th)\s+and\s+(\d+)(st|nd|rd|th)", RegexOptions.IgnoreCase)]
    private static partial Regex DayRangeWithOrdinalsPattern();

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

    // ===== DAY-SPECIFIC HELPERS =====

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
        List<int> values = [];

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
    /// Parse day-of-week list like "monday,wednesday,friday" into DayOfWeek list
    /// </summary>
    private static IReadOnlyList<DayOfWeek>? ParseDayOfWeekList(string dayListString)
    {
        if (string.IsNullOrWhiteSpace(dayListString))
        {
            return null;
        }

        var dayStrings = dayListString.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        List<DayOfWeek> days = [];

        foreach (var dayStr in dayStrings)
        {
            if (DayNames.TryGetValue(dayStr, out var dayOfWeek))
            {
                days.Add(dayOfWeek);
            }
        }

        return days.Count >= 2 ? days : null;
    }

    // ===== DAY PARSING METHODS =====

    /// <summary>
    /// Parse day-related constraints from natural language input
    /// Handles day-of-week, day-of-month, day patterns, lists, ranges, and advanced Quartz features
    /// Context-aware: behavior differs for monthly/yearly vs other intervals
    /// </summary>
    private ParseResult<(DayConstraints, AdvancedQuartzConstraints)> TryParseDayConstraints(
        string input,
        IntervalUnit unit,
        Match specificDayMatch)
    {
        var dayConstraints = new DayConstraints();
        var advancedConstraints = new AdvancedQuartzConstraints();

        // For monthly/yearly intervals, "on" means day-of-month or advanced Quartz features
        if (unit == IntervalUnit.Months || unit == IntervalUnit.Years)
        {
            var result = ParseMonthlyDayConstraints(input, specificDayMatch);
            if (result is ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error error)
            {
                return error;
            }
            var success = (ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Success)result;
            dayConstraints = success.Value.Item1;
            advancedConstraints = success.Value.Item2;
        }
        else
        {
            // For other intervals, check for day-of-week patterns
            var result = ParseNonMonthlyDayConstraints(input, unit, specificDayMatch);
            if (result is ParseResult<DayConstraints>.Error error)
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(error.Message);
            }
            var success = (ParseResult<DayConstraints>.Success)result;
            dayConstraints = success.Value;
        }

        // Parse day lists and ranges (applies to all intervals)
        var listRangeResult = ParseDayListsAndRanges(input);
        if (listRangeResult is ParseResult<DayConstraints>.Error listError)
        {
            return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(listError.Message);
        }
        var listSuccess = (ParseResult<DayConstraints>.Success)listRangeResult;

        // Merge day list/range constraints with existing constraints
        dayConstraints = dayConstraints with
        {
            DayList = listSuccess.Value.DayList ?? dayConstraints.DayList,
            DayStart = listSuccess.Value.DayStart ?? dayConstraints.DayStart,
            DayEnd = listSuccess.Value.DayEnd ?? dayConstraints.DayEnd
        };

        return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Success((dayConstraints, advancedConstraints));
    }

    /// <summary>
    /// Parse day constraints for monthly/yearly intervals (day-of-month, advanced Quartz features)
    /// </summary>
    private ParseResult<(DayConstraints, AdvancedQuartzConstraints)> ParseMonthlyDayConstraints(
        string input,
        Match specificDayMatch)
    {
        DayOfWeek? dayOfWeek = null;
        DayPattern? dayPattern = null;
        int? dayOfMonth = null;

        var isLastDay = false;
        var isLastDayOfWeek = false;
        int? lastDayOffset = null;
        var isNearestWeekday = false;
        int? nthOccurrence = null;

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
                    return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                        $"Invalid offset: {lastOffsetMatch.Groups[1].Value}");
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
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error($"Invalid day: {dayString}");
            }
            dayOfWeek = parsedDay;
            isLastDayOfWeek = true;
        }
        // Weekday nearest: "weekday nearest 15"
        else if (WeekdayNearestPattern().Match(input) is { Success: true } weekdayNearestMatch)
        {
            if (!int.TryParse(weekdayNearestMatch.Groups[1].ValueSpan, out var day))
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                    $"Invalid day: {weekdayNearestMatch.Groups[1].Value}");
            }
            if (day < 1 || day > 31)
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                    $"Day for weekday nearest must be 1-31, got: {day}");
            }
            dayOfMonth = day;
            isNearestWeekday = true;
        }
        // Nth occurrence: "1st monday", "3rd friday"
        else if (NthOccurrencePattern().Match(input) is { Success: true } nthMatch)
        {
            if (!int.TryParse(nthMatch.Groups[1].ValueSpan, out var nth))
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                    $"Invalid occurrence number: {nthMatch.Groups[1].Value}");
            }
            if (nth < 1 || nth > 5)
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                    $"Occurrence number must be 1-5, got: {nth}");
            }

            var dayString = nthMatch.Groups[2].Value;
            if (!DayNames.TryGetValue(dayString, out var parsedDay))
            {
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error($"Invalid day: {dayString}");
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
                return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error($"Invalid day: {dayString}");
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
                    return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                        $"Invalid day of month: {daySpan.ToString()}");
                }

                // Validate day of month is 1-31
                if (day < 1 || day > 31)
                {
                    return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error(
                        $"Day of month must be 1-31, got: {day}");
                }

                dayOfMonth = day;
            }
        }

        var dayConstraints = new DayConstraints
        {
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfMonth = dayOfMonth
        };

        var advancedConstraints = new AdvancedQuartzConstraints
        {
            IsLastDay = isLastDay,
            IsLastDayOfWeek = isLastDayOfWeek,
            LastDayOffset = lastDayOffset,
            IsNearestWeekday = isNearestWeekday,
            NthOccurrence = nthOccurrence
        };

        return new ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Success((dayConstraints, advancedConstraints));
    }

    /// <summary>
    /// Parse day constraints for non-monthly intervals (day-of-week, day patterns, ranges)
    /// </summary>
    private ParseResult<DayConstraints> ParseNonMonthlyDayConstraints(
        string input,
        IntervalUnit unit,
        Match specificDayMatch)
    {
        DayOfWeek? dayOfWeek = null;
        DayPattern? dayPattern = null;
        IReadOnlyList<DayOfWeek>? dayOfWeekList = null;
        DayOfWeek? dayOfWeekStart = null, dayOfWeekEnd = null;

        // Check for day-of-week list first (highest priority): "every monday,wednesday,friday"
        var dayOfWeekListMatch = DayOfWeekListPattern().Match(input);
        if (dayOfWeekListMatch.Success)
        {
            var dayListString = dayOfWeekListMatch.Groups[1].Value;
            dayOfWeekList = ParseDayOfWeekList(dayListString);

            if (dayOfWeekList == null || dayOfWeekList.Count < 2)
            {
                return new ParseResult<DayConstraints>.Error("Day-of-week list must contain at least 2 days");
            }
        }
        // Check for custom day-of-week range: "every tuesday-thursday"
        else if (DayOfWeekCustomRangePattern().Match(input) is { Success: true } customRangeMatch)
        {
            var startDay = customRangeMatch.Groups[1].Value;
            var endDay = customRangeMatch.Groups[2].Value;

            if (!DayNames.TryGetValue(startDay, out var startDayOfWeek))
            {
                return new ParseResult<DayConstraints>.Error($"Invalid day name: {startDay}");
            }

            if (!DayNames.TryGetValue(endDay, out var endDayOfWeek))
            {
                return new ParseResult<DayConstraints>.Error($"Invalid day name: {endDay}");
            }

            dayOfWeekStart = startDayOfWeek;
            dayOfWeekEnd = endDayOfWeek;
        }
        // Check for "between" day range (medium priority)
        else
        {
            var dayRangeMatch = DayRangePattern().Match(input);
            if (dayRangeMatch.Success)
        {
            var startDay = dayRangeMatch.Groups[1].Value;
            var endDay = dayRangeMatch.Groups[2].Value;

            if (!DayNames.TryGetValue(startDay, out var startDayOfWeek))
            {
                return new ParseResult<DayConstraints>.Error($"Invalid day name: {startDay}");
            }

            if (!DayNames.TryGetValue(endDay, out var endDayOfWeek))
            {
                return new ParseResult<DayConstraints>.Error($"Invalid day name: {endDay}");
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
                    // Arbitrary day ranges with "between X and Y" syntax not supported
                    // Use compact notation instead: "every tuesday-thursday"
                    return new ParseResult<DayConstraints>.Error(
                        $"Day ranges other than 'between monday and friday' (weekdays) or 'between saturday and sunday' (weekends) are not yet supported. Found: {startDay} to {endDay}. " +
                        $"Try using compact notation instead: 'every {startDay}-{endDay}'");
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

                return new ParseResult<DayConstraints>.Error(
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
                    return new ParseResult<DayConstraints>.Error($"Invalid day: {dayString}");
                }
            }
            }
        }

        return new ParseResult<DayConstraints>.Success(new DayConstraints
        {
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfWeekList = dayOfWeekList,
            DayOfWeekStart = dayOfWeekStart,
            DayOfWeekEnd = dayOfWeekEnd
        });
    }

    /// <summary>
    /// Parse day lists and ranges: "on the 1st, 15th, 30th" or "on the 1-7,15-21,30" or "between the 1st and 15th"
    /// </summary>
    private ParseResult<DayConstraints> ParseDayListsAndRanges(string input)
    {
        IReadOnlyList<int>? dayList = null;
        int? dayStart = null, dayEnd = null;

        // Check ordinals first (more specific patterns take precedence)
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
            // Day list compact notation: "on the 1-7,15-21,30" (last, matches numeric-only patterns)
            else
            {
                var dayListCompactMatch = DayListCompactNotationPattern().Match(input);
                if (dayListCompactMatch.Success)
                {
                    var notation = dayListCompactMatch.Groups[1].Value;
                    dayList = ParseListNotation(notation, 1, 31);
                }
            }
        }

        return new ParseResult<DayConstraints>.Success(new DayConstraints
        {
            DayList = dayList,
            DayStart = dayStart,
            DayEnd = dayEnd
        });
    }
}
