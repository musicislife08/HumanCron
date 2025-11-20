using System;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Year parsing methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    /// <summary>
    /// Parse optional year field (1970-2099)
    /// Returns null if not specified or wildcard (*)
    /// </summary>
    private static int? ParseYear(ReadOnlySpan<char> yearPart)
    {
        // Empty or wildcard - no year constraint
        if (yearPart.IsEmpty || yearPart is "*")
        {
            return null;
        }

        // Parse year value
        if (int.TryParse(yearPart, out var year))
        {
            // Quartz spec: year range is 1970-2099
            // We don't validate here to match parser behavior (deferred validation)
            return year;
        }

        return null;
    }
}
