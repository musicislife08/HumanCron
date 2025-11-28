using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanCron.Converters.Unix;

/// <summary>
/// UnixCronParser - Generic helper methods for parsing ranges and lists
/// </summary>
internal sealed partial class UnixCronParser
{
    /// <summary>
    /// Parse range from cron field (e.g., "9-17", "0-30", "1-15", "9-17/2")
    /// Returns (null, null, null) if not a range
    /// </summary>
    private static (int? Start, int? End, int? Step) ParseRange(string field)
    {
        if (string.IsNullOrWhiteSpace(field) || !field.Contains('-'))
        {
            return (null, null, null);
        }

        // Skip if it's a wildcard or step pattern
        if (field == "*" || field.StartsWith("*/"))
        {
            return (null, null, null);
        }

        // Check for range+step: "9-17/2"
        int? step = null;
        var rangeField = field;

        if (field.Contains('/'))
        {
            var stepParts = field.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (stepParts.Length == 2 && int.TryParse(stepParts[1], out var stepValue))
            {
                step = stepValue;
                rangeField = stepParts[0];
            }
        }

        // Parse range: "9-17" → (9, 17) or "9-17/2" → (9, 17, 2)
        var parts = rangeField.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
        {
            return (start, end, step);
        }

        return (null, null, null);
    }

    /// <summary>
    /// Parse list from cron field with support for mixed syntax:
    /// - Simple list: "0,15,30,45" → [0, 15, 30, 45]
    /// - Mixed list+range: "0-4,8-12,20" → [0, 1, 2, 3, 4, 8, 9, 10, 11, 12, 20]
    /// Returns null if not a list
    /// </summary>
    private static IReadOnlyList<int>? ParseList(string field, int minValue, int maxValue)
    {
        if (string.IsNullOrWhiteSpace(field) || !field.Contains(','))
        {
            return null;
        }

        // Skip if it's a wildcard or step pattern
        if (field == "*" || field.StartsWith("*/"))
        {
            return null;
        }

        // Parse list with possible ranges: "0-4,8-12,20" → [0,1,2,3,4,8,9,10,11,12,20]
        var parts = field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<int> values = [];

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

        return values.Count >= 2 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }

    /// <summary>
    /// Validate all fields are within valid Unix cron ranges
    /// </summary>
    private static string? ValidateFields(string minute, string hour, string day, string month, string dayOfWeek)
    {
        // Validate minute field (0-59)
        var minuteError = ValidateField(minute, 0, 59, "Minute");
        if (minuteError != null) return minuteError;

        // Validate hour field (0-23)
        var hourError = ValidateField(hour, 0, 23, "Hour");
        if (hourError != null) return hourError;

        // Validate day field (1-31)
        var dayError = ValidateField(day, 1, 31, "Day");
        if (dayError != null) return dayError;

        // Validate month field (1-12)
        var monthError = ValidateField(month, 1, 12, "Month");
        if (monthError != null) return monthError;

        // Validate day-of-week field (0-7, both 0 and 7 = Sunday)
        var dayOfWeekError = ValidateField(dayOfWeek, 0, 7, "Day-of-week");
        if (dayOfWeekError != null) return dayOfWeekError;

        return null;
    }

    /// <summary>
    /// Validate a single cron field against min/max range
    /// Handles wildcards (*), ranges (1-5), lists (1,2,3), and steps (*/5)
    /// </summary>
    private static string? ValidateField(string field, int min, int max, string fieldName)
    {
        // Wildcard is always valid
        if (field == "*") return null;

        // Handle step values: */N or N-M/S
        var stepParts = field.Split('/');
        var valueToValidate = stepParts[0];

        // Validate step value if present
        if (stepParts.Length == 2)
        {
            if (!int.TryParse(stepParts[1], out var step) || step < 1)
            {
                return $"{fieldName} step value must be >= 1, got '{stepParts[1]}'";
            }
        }

        // If base is wildcard, step is already validated
        if (valueToValidate == "*") return null;

        // Handle ranges: N-M
        if (valueToValidate.Contains('-'))
        {
            var rangeParts = valueToValidate.Split('-');
            if (rangeParts.Length != 2)
            {
                return $"{fieldName} range must be in format 'N-M', got '{valueToValidate}'";
            }

            if (!int.TryParse(rangeParts[0], out var rangeStart) || rangeStart < min || rangeStart > max)
            {
                return $"{fieldName} range start must be {min}-{max}, got '{rangeParts[0]}'";
            }

            if (!int.TryParse(rangeParts[1], out var rangeEnd) || rangeEnd < min || rangeEnd > max)
            {
                return $"{fieldName} range end must be {min}-{max}, got '{rangeParts[1]}'";
            }

            // Allow wraparound ranges (e.g., 22-6 for 10pm to 6am)
            // Both values are valid, wraparound is OK

            return null;
        }

        // Handle lists: N,M,O
        if (valueToValidate.Contains(','))
        {
            var listParts = valueToValidate.Split(',');
            foreach (var part in listParts)
            {
                if (!int.TryParse(part.Trim(), out var value) || value < min || value > max)
                {
                    return $"{fieldName} list value must be {min}-{max}, got '{part}'";
                }
            }
            return null;
        }

        // Handle single numeric value
        if (int.TryParse(valueToValidate, out var numValue))
        {
            if (numValue < min || numValue > max)
            {
                return $"{fieldName} must be {min}-{max}, got {numValue}";
            }
            return null;
        }

        // If we get here, it's an invalid format
        return $"{fieldName} has invalid format: '{field}'";
    }
}
