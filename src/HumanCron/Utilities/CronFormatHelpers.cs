using System.Collections.Generic;

namespace HumanCron.Utilities;

/// <summary>
/// Shared utilities for formatting cron expressions
/// </summary>
internal static class CronFormatHelpers
{
    /// <summary>
    /// Compact a list of values by converting consecutive sequences to ranges
    /// Example: [0,1,2,3,4,8,9,10,11,12,20] → "0-4,8-12,20"
    /// Sequences of 3+ consecutive values are converted to ranges for compactness
    /// </summary>
    /// <param name="values">The list of values to compact</param>
    /// <returns>Compacted string representation (e.g., "0-4,8-12,20")</returns>
    internal static string CompactList(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return "*";
        }

        List<string> parts = [];
        var i = 0;

        while (i < values.Count)
        {
            var start = values[i];
            var end = start;

            // Find consecutive sequence
            while (i + 1 < values.Count && values[i + 1] == end + 1)
            {
                i++;
                end = values[i];
            }

            // Use range notation for 3+ consecutive values, otherwise list individual values
            var sequenceLength = end - start + 1;
            if (sequenceLength >= 3)
            {
                parts.Add($"{start}-{end}");
            }
            else
            {
                // Add individual values (1 or 2 consecutive values)
                for (var j = start; j <= end; j++)
                {
                    parts.Add(j.ToString());
                }
            }

            i++;
        }

        return string.Join(",", parts);
    }
}
