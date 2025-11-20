using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Generic helper methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    /// <summary>
    /// Parse range from cron field (e.g., "9-17", "0-30", "1-15", "9-17/2")
    /// Returns (null, null, null) if not a range
    /// </summary>
    private static (int? Start, int? End, int? Step) ParseRange(ReadOnlySpan<char> field)
    {
        // Check if field contains a dash (for ranges like "9-17")
        var dashIndex = field.IndexOf('-');
        if (field.IsEmpty || dashIndex < 0)
        {
            return (null, null, null);
        }

        // Skip if it's a wildcard or step pattern
        if (field is "*" || field is "?" || field.StartsWith("*/"))
        {
            return (null, null, null);
        }

        // Check for range+step: "9-17/2"
        int? step = null;
        var rangeField = field;

        var slashIndex = field.IndexOf('/');
        if (slashIndex > 0)
        {
            var stepSpan = field[(slashIndex + 1)..];
            // Parse step value - validation deferred to Quartz scheduler
            // This allows parsing of any integer value (including 0, negative, or very large)
            // Invalid values will be caught by Quartz during trigger creation
            if (int.TryParse(stepSpan, out var stepValue))
            {
                step = stepValue;
                rangeField = field[..slashIndex];
            }
        }

        // Re-find dash in the range field (not the original field)
        dashIndex = rangeField.IndexOf('-');

        // Parse range: "9-17" → (9, 17) or "9-17/2" → (9, 17, 2)
        if (dashIndex <= 0 || dashIndex >= rangeField.Length - 1) return (null, null, null);
        var startSpan = rangeField[..dashIndex];
        var endSpan = rangeField[(dashIndex + 1)..];

        if (int.TryParse(startSpan, out var start) && int.TryParse(endSpan, out var end))
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
    private static IReadOnlyList<int>? ParseList(ReadOnlySpan<char> field, int minValue, int maxValue)
    {
        // Check if field contains commas (for lists like "0,15,30,45")
        var commaIndex = field.IndexOf(',');
        if (field.IsEmpty || commaIndex < 0)
        {
            return null;
        }

        // Skip if it's a wildcard or step pattern
        if (field is "*" || field is "?" || field.StartsWith("*/"))
        {
            return null;
        }

        // Parse list with possible ranges: "0-4,8-12,20" → [0,1,2,3,4,8,9,10,11,12,20]
        var values = new List<int>();
        var start = 0;

        while (start < field.Length)
        {
            var nextComma = field[start..].IndexOf(',');
            var part = nextComma >= 0
                ? field.Slice(start, nextComma)
                : field[start..];

            // Check if this part is a range (e.g., "0-4")
            var dashIndex = part.IndexOf('-');
            if (dashIndex > 0 && dashIndex < part.Length - 1)  // Not at start or end
            {
                var rangeStart = part[..dashIndex];
                var rangeEnd = part[(dashIndex + 1)..];

                if (int.TryParse(rangeStart, out var s) &&
                    int.TryParse(rangeEnd, out var e) &&
                    s <= e &&
                    s >= minValue && e <= maxValue)
                {
                    // Expand range: 0-4 → [0, 1, 2, 3, 4]
                    for (var i = s; i <= e; i++)
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

            if (nextComma < 0) break;
            start += nextComma + 1;
        }

        // Remove duplicates and sort
        return values.Count >= 2 ? values.Distinct().OrderBy(v => v).ToList() : null;
    }
}
