using HumanCron.Models.Internal;

namespace HumanCron.Converters.Unix;

/// <summary>
/// UnixCronParser - Interval determination methods
/// </summary>
internal sealed partial class UnixCronParser
{
    private static (int Interval, IntervalUnit Unit) DetermineInterval(
        string minute, string hour, string day, string dayOfWeek)
    {
        // Pattern: */15 * * * * → Every 15 minutes
        if (minute.StartsWith("*/") || (minute == "*" && hour == "*"))
        {
            if (minute == "*")
            {
                return (1, IntervalUnit.Minutes);
            }
            var interval = int.Parse(minute[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Minutes); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Minutes);
        }

        // Pattern: 0 */6 * * * → Every 6 hours
        if (hour.StartsWith("*/") || (hour == "*" && day == "*" && dayOfWeek == "*"))
        {
            if (hour == "*")
            {
                return (1, IntervalUnit.Hours);
            }
            var interval = int.Parse(hour[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Hours); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Hours);
        }

        // Pattern: 0 14 * * * → Daily at specific time
        if (day == "*" && dayOfWeek == "*")
        {
            return (1, IntervalUnit.Days);
        }

        // Pattern: 0 14 */2 * * → Every 2 days at specific time
        if (day.StartsWith("*/"))
        {
            var interval = int.Parse(day[2..]);
            if (!IsValidInterval(interval))
            {
                return (0, IntervalUnit.Days); // Invalid - triggers error in caller
            }
            return (interval, IntervalUnit.Days);
        }

        // Pattern: 0 14 * * 1 → Weekly on Monday
        if (dayOfWeek != "*" && day == "*")
        {
            return (1, IntervalUnit.Weeks);
        }

        // Pattern: 0 1 1 1 * → Monthly on specific day (with month constraint)
        // When we have a specific day-of-month value, default to monthly
        // This allows patterns like "every month on the 15th" or "every month on january 1st"
        return (1, IntervalUnit.Months);
    }

    /// <summary>
    /// Validates that an interval value is reasonable (1 to MaxInterval)
    /// </summary>
    /// <param name="interval">The interval value to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool IsValidInterval(int interval) => interval is > 0 and <= MaxInterval;
}
