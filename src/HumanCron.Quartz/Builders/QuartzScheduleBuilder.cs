using HumanCron.Models.Internal;
using System;
using HumanCron.Quartz.Helpers;
using Quartz;
using NodaTime;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// Routes ScheduleSpec to appropriate Quartz schedule builder
/// Uses CronScheduleBuilder for simple patterns, CalendarIntervalScheduleBuilder for complex patterns
/// </summary>
internal sealed class QuartzScheduleBuilder : IQuartzScheduleBuilder
{
    private readonly IClock _clock;

    /// <summary>
    /// Create a new QuartzScheduleBuilder (production use)
    /// </summary>
    internal static QuartzScheduleBuilder Create()
    {
        return new QuartzScheduleBuilder(SystemClock.Instance);
    }

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    internal QuartzScheduleBuilder(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    public IScheduleBuilder Build(ScheduleSpec spec)
    {
        // Determine which Quartz scheduler to use based on pattern complexity
        return RequiresCalendarInterval(spec) ? BuildCalendarInterval(spec) : BuildCronSchedule(spec);
    }

    /// <summary>
    /// Determines if pattern requires CalendarIntervalSchedule
    /// CalendarInterval needed for: multi-week (2w+), months, years
    /// </summary>
    private static bool RequiresCalendarInterval(ScheduleSpec spec)
    {
        return spec.Unit switch
        {
            NaturalIntervalUnit.Weeks when spec.Interval > 1 => true,  // 2w, 3w, 4w, etc.
            NaturalIntervalUnit.Months => true,                         // All monthly intervals
            NaturalIntervalUnit.Years => true,                          // All yearly intervals
            _ => false                                                   // Everything else uses cron
        };
    }

    /// <summary>
    /// Build Quartz CronScheduleBuilder for simple patterns
    /// Used for: seconds, minutes, hours, days, single-week intervals
    /// </summary>
    private static IScheduleBuilder BuildCronSchedule(ScheduleSpec spec)
    {
        var cronBuilder = new QuartzCronBuilder();
        return cronBuilder.Build(spec);
    }

    /// <summary>
    /// Build Quartz CalendarIntervalScheduleBuilder for complex patterns
    /// Used for: multi-week (2w+), months, years
    /// </summary>
    private static IScheduleBuilder BuildCalendarInterval(ScheduleSpec spec)
    {
        var calendarBuilder = new QuartzCalendarIntervalBuilder();
        return calendarBuilder.Build(spec);
    }

    /// <summary>
    /// Calculate the appropriate start time for schedules with day-of-week or day-of-month constraints
    /// </summary>
    public DateTimeOffset? CalculateStartTime(ScheduleSpec spec, DateTimeOffset? referenceTime = null)
    {
        var refTime = referenceTime ?? _clock.GetCurrentInstant().ToDateTimeOffset();

        // Only calculate start time for CalendarInterval schedules with constraints
        if (!RequiresCalendarInterval(spec))
        {
            return null; // Cron schedules handle constraints natively
        }

        // Handle day-of-week constraint (e.g., "2w on sunday")
        if (spec.DayOfWeek.HasValue)
        {
            var timeZoneInfo = TimeZoneConverter.ToTimeZoneInfo(spec.TimeZone);
            return CalculateNextDayOfWeek(refTime, spec.DayOfWeek.Value, spec.TimeOfDay, timeZoneInfo);
        }

        // Handle day-of-month constraint (e.g., "3M on 15")
        // ReSharper disable once InvertIf
        if (spec.DayOfMonth.HasValue)
        {
            var timeZoneInfo = TimeZoneConverter.ToTimeZoneInfo(spec.TimeZone);
            return CalculateNextDayOfMonth(refTime, spec.DayOfMonth.Value, spec.TimeOfDay, timeZoneInfo);
        }

        // No constraints, no special start time needed
        return null;
    }

    /// <summary>
    /// Calculate the next occurrence of a specific day of week
    /// </summary>
    private static DateTimeOffset CalculateNextDayOfWeek(
        DateTimeOffset referenceTime,
        DayOfWeek targetDay,
        TimeOnly? timeOfDay,
        TimeZoneInfo timeZone)
    {
        // Convert reference time to target timezone
        var localTime = TimeZoneInfo.ConvertTime(referenceTime, timeZone);

        // Calculate days until target day
        var daysUntilTarget = ((int)targetDay - (int)localTime.DayOfWeek + 7) % 7;

        switch (daysUntilTarget)
        {
            // If we're already on the target day, check if we've passed the time
            case 0 when timeOfDay.HasValue:
            {
                var todayAtTime = localTime.Date.Add(timeOfDay.Value.ToTimeSpan());
                if (localTime.DateTime >= todayAtTime)
                {
                    // Already passed the time today, schedule for next week
                    daysUntilTarget = 7;
                }

                break;
            }
            case 0:
                // Already on target day and no time constraint, use next occurrence
                daysUntilTarget = 7;
                break;
        }

        // Calculate target date
        var targetDate = localTime.Date.AddDays(daysUntilTarget);
        var targetTime = timeOfDay?.ToTimeSpan() ?? TimeSpan.Zero;
        var targetDateTime = targetDate.Add(targetTime);

        // Return as DateTimeOffset in local timezone (with offset preserved)
        var targetLocal = new DateTimeOffset(targetDateTime, timeZone.GetUtcOffset(targetDateTime));
        return targetLocal;
    }

    /// <summary>
    /// Calculate the next occurrence of a specific day of month
    /// </summary>
    private static DateTimeOffset CalculateNextDayOfMonth(
        DateTimeOffset referenceTime,
        int targetDay,
        TimeOnly? timeOfDay,
        TimeZoneInfo timeZone)
    {
        // Convert reference time to target timezone
        var localTime = TimeZoneInfo.ConvertTime(referenceTime, timeZone);

        // Start with current month
        var targetDate = new DateTime(localTime.Year, localTime.Month, 1);

        // Ensure target day is valid for this month
        var daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
        var actualDay = Math.Min(targetDay, daysInMonth);
        targetDate = targetDate.AddDays(actualDay - 1);

        // If we've already passed this day/time in current month, move to next month
        var targetTime = timeOfDay?.ToTimeSpan() ?? TimeSpan.Zero;
        var targetDateTime = targetDate.Add(targetTime);

        if (localTime.DateTime >= targetDateTime)
        {
            // Move to next month
            targetDate = targetDate.AddMonths(1);
            targetDate = new DateTime(targetDate.Year, targetDate.Month, 1);
            daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
            actualDay = Math.Min(targetDay, daysInMonth);
            targetDate = targetDate.AddDays(actualDay - 1);
            targetDateTime = targetDate.Add(targetTime);
        }

        // Return as DateTimeOffset in local timezone (with offset preserved)
        var targetLocal = new DateTimeOffset(targetDateTime, timeZone.GetUtcOffset(targetDateTime));
        return targetLocal;
    }
}
