using System;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// QuartzCronParser - Interval determination methods
/// </summary>
internal sealed partial class QuartzCronParser
{
    private static (int Interval, NaturalIntervalUnit Unit) DetermineInterval(
        ReadOnlySpan<char> second, ReadOnlySpan<char> minute, ReadOnlySpan<char> hour, ReadOnlySpan<char> day, ReadOnlySpan<char> dayOfWeek)
    {
        // Pattern: */30 * * * * ? → Every 30 seconds
        if (second.StartsWith("*/") || second is "*")
        {
            if (second is "*")
            {
                return (1, NaturalIntervalUnit.Seconds);
            }
            var interval = int.Parse(second[2..]);
            return (interval, NaturalIntervalUnit.Seconds);
        }

        // Pattern: 0 */15 * * * ? → Every 15 minutes
        if (minute.StartsWith("*/") || (minute is "*" && hour is "*"))
        {
            if (minute is "*")
            {
                return (1, NaturalIntervalUnit.Minutes);
            }
            var interval = int.Parse(minute[2..]);
            return (interval, NaturalIntervalUnit.Minutes);
        }

        // Pattern: 0 0 */6 * * ? → Every 6 hours
        if (hour.StartsWith("*/") || (hour is "*" && day is "*" && dayOfWeek is "?"))
        {
            if (hour is "*")
            {
                return (1, NaturalIntervalUnit.Hours);
            }
            var interval = int.Parse(hour[2..]);
            return (interval, NaturalIntervalUnit.Hours);
        }

        // Pattern: 0 0 14 * * ? → Daily at specific time
        if (day is "*" && dayOfWeek is "?")
        {
            return (1, NaturalIntervalUnit.Days);
        }

        // Pattern: 0 0 14 */2 * ? → Every 2 days at specific time
        if (day.StartsWith("*/"))
        {
            var interval = int.Parse(day[2..]);
            return (interval, NaturalIntervalUnit.Days);
        }

        // Pattern: 0 0 14 ? * MON → Weekly on Monday
        if (!(dayOfWeek is "?") && day is "?")
        {
            return (1, NaturalIntervalUnit.Weeks);
        }

        // Default to daily if we can't determine
        return (1, NaturalIntervalUnit.Days);
    }
}
