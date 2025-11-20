using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Helper methods and lookup dictionaries
/// </summary>
internal sealed partial class NaturalLanguageParser
{
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
    /// Parse hour with optional am/pm suffix to 24-hour format
    /// Examples: "9" → 9, "9am" → 9, "9pm" → 21, "12am" → 0, "12pm" → 12
    /// </summary>
    private static int ParseHour(string hourStr, string? amPm)
    {
        var hour = int.Parse(hourStr);

        if (string.IsNullOrEmpty(amPm))
        {
            // No am/pm specified, treat as 24-hour format
            return hour;
        }

        var isAm = amPm.Equals("am", StringComparison.OrdinalIgnoreCase);

        if (hour == 12)
        {
            // 12am = midnight (0), 12pm = noon (12)
            return isAm ? 0 : 12;
        }

        // 1-11am = 1-11, 1-11pm = 13-23
        return isAm ? hour : hour + 12;
    }

}
