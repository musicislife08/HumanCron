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
}
