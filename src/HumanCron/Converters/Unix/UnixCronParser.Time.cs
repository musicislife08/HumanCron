using HumanCron.Models.Internal;
using System;

namespace HumanCron.Converters.Unix;

/// <summary>
/// UnixCronParser - Time parsing methods
/// </summary>
internal sealed partial class UnixCronParser
{
    private static TimeOnly? ParseTimeOfDay(string minute, string hour, IntervalUnit unit)
    {
        // Only parse time for daily/weekly intervals with fixed times
        if (unit == IntervalUnit.Minutes || unit == IntervalUnit.Hours)
        {
            return null;  // Sub-daily intervals don't have fixed time-of-day
        }

        // Parse hour and minute for daily/weekly schedules
        if (!int.TryParse(hour, out var hourValue) || !int.TryParse(minute, out var minuteValue)) return null;
        if (hourValue is >= 0 and <= 23 && minuteValue is >= 0 and <= 59)
        {
            return new TimeOnly(hourValue, minuteValue);
        }

        return null;
    }
}
