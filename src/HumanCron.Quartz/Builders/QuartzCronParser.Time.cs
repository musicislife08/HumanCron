using System;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Time parsing methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    private static TimeOnly? ParseTimeOfDay(ReadOnlySpan<char> second, ReadOnlySpan<char> minute, ReadOnlySpan<char> hour, NaturalIntervalUnit unit)
    {
        // Only parse time for daily/weekly intervals with fixed times
        if (unit == NaturalIntervalUnit.Seconds || unit == NaturalIntervalUnit.Minutes || unit == NaturalIntervalUnit.Hours)
        {
            return null;  // Sub-daily intervals don't have fixed time-of-day
        }

        // Parse hour, minute, and second for daily/weekly schedules
        if (!int.TryParse(hour, out var hourValue) || !int.TryParse(minute, out var minuteValue))
        {
            return null;
        }

        // Parse second if it's a specific value (not "*" or "*/n")
        var secondValue = 0;  // Default to 0 seconds
        if (int.TryParse(second, out var parsedSecond))
        {
            secondValue = parsedSecond;
        }

        // Validate ranges and construct TimeOnly
        if (hourValue is >= 0 and <= 23 &&
            minuteValue is >= 0 and <= 59 &&
            secondValue is >= 0 and <= 59)
        {
            return new TimeOnly(hourValue, minuteValue, secondValue);
        }

        return null;
    }
}
