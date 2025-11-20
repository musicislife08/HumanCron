using HumanCron.Models;
using HumanCron.Models.Internal;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Time constraint parsing logic (patterns, methods)
/// </summary>
internal sealed partial class NaturalLanguageParser
{
    // ===== TIME-SPECIFIC PATTERNS =====

    /// <summary>
    /// Time patterns: "at 2pm", "at 14:00", "at 3:30am"
    /// Supports both 12-hour (with am/pm) and 24-hour formats
    /// </summary>
    [GeneratedRegex(@"at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    /// <summary>
    /// Minute list patterns: "at minutes 0,15,30,45" or "at minutes 0-2,4,6-8"
    /// </summary>
    [GeneratedRegex(@"at\s+minutes\s+([\d,\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MinuteListPattern();

    /// <summary>
    /// Minute range patterns: "between minutes 0 and 30"
    /// </summary>
    [GeneratedRegex(@"between\s+minutes\s+(\d+)\s+and\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MinuteRangePattern();

    /// <summary>
    /// Hour list patterns: "at hours 9,12,15,18" or "at hours 9-17"
    /// </summary>
    [GeneratedRegex(@"at\s+hours\s+([\d,\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HourListPattern();

    /// <summary>
    /// Hour range patterns: "between hours 9 and 17" or "between hours 9am and 5pm"
    /// </summary>
    [GeneratedRegex(@"between\s+hours\s+(\d+)(am|pm)?\s+and\s+(\d+)(am|pm)?", RegexOptions.IgnoreCase)]
    private static partial Regex HourRangePattern();

    // ===== TIME PARSING METHODS =====

    /// <summary>
    /// Parse time-related constraints from natural language input
    /// Handles time of day, minute lists/ranges, and hour lists/ranges
    /// </summary>
    private ParseResult<TimeConstraints> TryParseTimeConstraints(string input)
    {
        TimeOnly? timeOfDay = null;
        IReadOnlyList<int>? minuteList = null;
        int? minuteStart = null, minuteEnd = null;
        IReadOnlyList<int>? hourList = null;
        int? hourStart = null, hourEnd = null;

        // Extract time of day (optional): "at 2pm", "at 14:00", "at 3:30am"
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
                    return new ParseResult<TimeConstraints>.Error($"Invalid hour for 12-hour format: {hour} (must be 1-12)");
                }
            }
            else
            {
                // 24-hour format: hour must be 0-23
                if (hour < 0 || hour > 23)
                {
                    return new ParseResult<TimeConstraints>.Error($"Invalid hour for 24-hour format: {hour} (must be 0-23)");
                }
            }

            // Validate minutes
            if (minute < 0 || minute > 59)
            {
                return new ParseResult<TimeConstraints>.Error($"Invalid minutes: {minute} (must be 0-59)");
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
        // Hour range: "between hours 9 and 17" or "between hours 9am and 5pm"
        else
        {
            var hourRangeMatch = HourRangePattern().Match(input);
            if (hourRangeMatch.Success)
            {
                var startHourStr = hourRangeMatch.Groups[1].Value;
                var startAmPm = hourRangeMatch.Groups[2].Success ? hourRangeMatch.Groups[2].Value : null;
                var endHourStr = hourRangeMatch.Groups[3].Value;
                var endAmPm = hourRangeMatch.Groups[4].Success ? hourRangeMatch.Groups[4].Value : null;

                hourStart = ParseHour(startHourStr, startAmPm);
                hourEnd = ParseHour(endHourStr, endAmPm);
            }
        }

        return new ParseResult<TimeConstraints>.Success(new TimeConstraints
        {
            TimeOfDay = timeOfDay,
            MinuteList = minuteList,
            MinuteStart = minuteStart,
            MinuteEnd = minuteEnd,
            HourList = hourList,
            HourStart = hourStart,
            HourEnd = hourEnd
        });
    }
}
