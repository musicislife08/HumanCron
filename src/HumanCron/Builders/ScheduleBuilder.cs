using HumanCron.Models.Internal;
using HumanCron.Formatting;
using System;
using NodaTime;

namespace HumanCron.Builders;

/// <summary>
/// Fluent API for building schedule specifications programmatically
/// Provides IDE autocomplete support and compile-time safety
/// Returns natural language strings that can be converted to cron
/// </summary>
/// <example>
/// <code>
/// // Every 6 hours
/// var schedule = Schedule.Every(6).Hours().Build(); // Returns "6h"
///
/// // Daily at 2pm
/// var schedule = Schedule.Every(1).Day().At(14, 0).Build(); // Returns "1d at 2pm"
///
/// // Weekly on Monday at 9am
/// var schedule = Schedule.Every(1).Week().OnMonday().At(9, 0).Build(); // Returns "1w on monday at 9am"
///
/// // Daily on weekdays at 9am
/// var schedule = Schedule.Every(1).Day().OnWeekdays().At(9, 0).Build(); // Returns "1d on weekdays at 9am"
/// </code>
/// </example>
public sealed class ScheduleBuilder
{
    private int _interval = 1;
    private IntervalUnit _unit = IntervalUnit.Days;
    private DayOfWeek? _dayOfWeek;
    private DayPattern? _dayPattern;
    private int? _dayOfMonth;
    private TimeOnly? _timeOfDay;

    // Default timezone: system local timezone (via DateTimeZoneProviders.Tzdb.GetSystemDefault())
    // Evaluated at instance creation time
    // Can be overridden via InTimeZone() method
    private DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();

    private ScheduleBuilder() { }

    /// <summary>
    /// Start building a schedule with specified interval
    /// </summary>
    /// <param name="interval">Interval value (must be positive)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <exception cref="ArgumentException">Thrown when interval is not positive</exception>
    /// <example>
    /// <code>
    /// Schedule.Every(6).Hours()  // Every 6 hours
    /// Schedule.Every(1).Day()    // Daily
    /// </code>
    /// </example>
    public static ScheduleBuilder Every(int interval)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval);
        return new ScheduleBuilder { _interval = interval };
    }

    // ========================================
    // Interval Unit Methods
    // ========================================

    /// <summary>Set interval unit to seconds (e.g., Every(30).Seconds() → every 30 seconds)</summary>
    public ScheduleBuilder Seconds()
    {
        _unit = IntervalUnit.Seconds;
        return this;
    }

    /// <summary>Set interval unit to seconds (singular form)</summary>
    public ScheduleBuilder Second() => Seconds();

    /// <summary>Set interval unit to minutes (e.g., Every(15).Minutes() → every 15 minutes)</summary>
    public ScheduleBuilder Minutes()
    {
        _unit = IntervalUnit.Minutes;
        return this;
    }

    /// <summary>Set interval unit to minutes (singular form)</summary>
    public ScheduleBuilder Minute() => Minutes();

    /// <summary>Set interval unit to hours (e.g., Every(6).Hours() → every 6 hours)</summary>
    public ScheduleBuilder Hours()
    {
        _unit = IntervalUnit.Hours;
        return this;
    }

    /// <summary>Set interval unit to hours (singular form)</summary>
    public ScheduleBuilder Hour() => Hours();

    /// <summary>Set interval unit to days (e.g., Every(1).Days() → daily)</summary>
    public ScheduleBuilder Days()
    {
        _unit = IntervalUnit.Days;
        return this;
    }

    /// <summary>Set interval unit to days (singular form)</summary>
    public ScheduleBuilder Day() => Days();

    /// <summary>Set interval unit to weeks (e.g., Every(1).Weeks() → weekly)</summary>
    public ScheduleBuilder Weeks()
    {
        _unit = IntervalUnit.Weeks;
        return this;
    }

    /// <summary>Set interval unit to weeks (singular form)</summary>
    public ScheduleBuilder Week() => Weeks();

    /// <summary>Set interval unit to months (e.g., Every(1).Months() → monthly)</summary>
    public ScheduleBuilder Months()
    {
        _unit = IntervalUnit.Months;
        return this;
    }

    /// <summary>Set interval unit to months (singular form)</summary>
    public ScheduleBuilder Month() => Months();

    /// <summary>Set interval unit to years (e.g., Every(1).Years() → yearly)</summary>
    public ScheduleBuilder Years()
    {
        _unit = IntervalUnit.Years;
        return this;
    }

    /// <summary>Set interval unit to years (singular form)</summary>
    public ScheduleBuilder Year() => Years();

    // ========================================
    // Day of Week Methods
    // ========================================

    /// <summary>Specify day of week (e.g., On(DayOfWeek.Monday))</summary>
    public ScheduleBuilder On(DayOfWeek day)
    {
        _dayOfWeek = day;
        _dayPattern = null;  // Clear pattern if setting specific day
        return this;
    }

    /// <summary>Schedule on Monday</summary>
    public ScheduleBuilder OnMonday() => On(DayOfWeek.Monday);

    /// <summary>Schedule on Tuesday</summary>
    public ScheduleBuilder OnTuesday() => On(DayOfWeek.Tuesday);

    /// <summary>Schedule on Wednesday</summary>
    public ScheduleBuilder OnWednesday() => On(DayOfWeek.Wednesday);

    /// <summary>Schedule on Thursday</summary>
    public ScheduleBuilder OnThursday() => On(DayOfWeek.Thursday);

    /// <summary>Schedule on Friday</summary>
    public ScheduleBuilder OnFriday() => On(DayOfWeek.Friday);

    /// <summary>Schedule on Saturday</summary>
    public ScheduleBuilder OnSaturday() => On(DayOfWeek.Saturday);

    /// <summary>Schedule on Sunday</summary>
    public ScheduleBuilder OnSunday() => On(DayOfWeek.Sunday);

    /// <summary>Schedule on weekdays (Monday-Friday)</summary>
    public ScheduleBuilder OnWeekdays()
    {
        _dayPattern = DayPattern.Weekdays;
        _dayOfWeek = null;  // Clear specific day if setting pattern
        return this;
    }

    /// <summary>Schedule on weekends (Saturday-Sunday)</summary>
    public ScheduleBuilder OnWeekends()
    {
        _dayPattern = DayPattern.Weekends;
        _dayOfWeek = null;  // Clear specific day if setting pattern
        return this;
    }

    // ========================================
    // Day of Month Methods
    // ========================================

    /// <summary>
    /// Specify day of month for monthly/yearly schedules
    /// </summary>
    /// <param name="day">Day of month (1-31)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when day is not 1-31</exception>
    /// <example>
    /// <code>
    /// Schedule.Every(1).Month().OnDayOfMonth(15).At(14, 0)  // 15th of every month at 2pm
    /// Schedule.Every(1).Year().OnDayOfMonth(1).At(0, 0)     // January 1st at midnight
    /// </code>
    /// </example>
    public ScheduleBuilder OnDayOfMonth(int day)
    {
        if (day < 1 || day > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Day of month must be 1-31");
        }
        _dayOfMonth = day;
        return this;
    }

    // ========================================
    // Time of Day Methods
    // ========================================

    /// <summary>
    /// Specify time of day using TimeOnly
    /// </summary>
    /// <param name="time">TimeOnly instance</param>
    /// <example>
    /// <code>
    /// .At(new TimeOnly(14, 0))
    /// .At(TimeOnly.FromTimeSpan(TimeSpan.FromHours(14)))
    /// </code>
    /// </example>
    public ScheduleBuilder At(TimeOnly time)
    {
        _timeOfDay = time;
        return this;
    }

    /// <summary>
    /// Specify time of day using hour (24-hour format)
    /// </summary>
    /// <param name="hour">Hour in 24-hour format (0-23)</param>
    /// <param name="minute">Minute (0-59), defaults to 0</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when hour or minute is out of range</exception>
    /// <example>
    /// <code>
    /// .AtHour(14)              // 2:00 PM
    /// .AtHour(9, minute: 30)   // 9:30 AM (named parameter for readability)
    /// .AtHour(0)               // Midnight
    /// </code>
    /// </example>
    public ScheduleBuilder AtHour(int hour, int minute = 0)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be between 0 and 23");
        }

        if (minute < 0 || minute > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Minute must be between 0 and 59");
        }

        _timeOfDay = new TimeOnly(hour, minute);
        return this;
    }

    /// <summary>
    /// Specify time as noon (12:00 PM)
    /// </summary>
    /// <example>
    /// <code>
    /// .AtNoon()  // 12:00 PM
    /// </code>
    /// </example>
    public ScheduleBuilder AtNoon()
    {
        _timeOfDay = new TimeOnly(12, 0);
        return this;
    }

    /// <summary>
    /// Specify time as midnight (12:00 AM)
    /// </summary>
    /// <example>
    /// <code>
    /// .AtMidnight()  // 12:00 AM
    /// </code>
    /// </example>
    public ScheduleBuilder AtMidnight()
    {
        _timeOfDay = new TimeOnly(0, 0);
        return this;
    }

    // ========================================
    // Timezone Methods
    // ========================================

    /// <summary>
    /// Specify timezone for schedule execution
    /// </summary>
    /// <param name="timeZone">DateTimeZone instance (use IANA IDs)</param>
    /// <exception cref="ArgumentNullException">Thrown when timeZone is null</exception>
    /// <example>
    /// <code>
    /// .InTimeZone(DateTimeZoneProviders.Tzdb["America/New_York"])
    /// .InTimeZone(DateTimeZone.Utc)
    /// </code>
    /// </example>
    public ScheduleBuilder InTimeZone(DateTimeZone timeZone)
    {
        _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
        return this;
    }

    /// <summary>
    /// Set timezone to UTC
    /// </summary>
    public ScheduleBuilder InUtc()
    {
        _timeZone = DateTimeZone.Utc;
        return this;
    }

    /// <summary>
    /// Set timezone to local system timezone (default)
    /// </summary>
    public ScheduleBuilder InLocalTime()
    {
        _timeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault();
        return this;
    }

    // ========================================
    // Build Method
    // ========================================

    /// <summary>
    /// Build the final natural language schedule string from the configured builder
    /// </summary>
    /// <returns>Natural language schedule string (e.g., "1d at 2pm", "6h", "1w on monday at 9am")</returns>
    /// <example>
    /// <code>
    /// var schedule = Schedule.Every(1).Day().At(14, 0).Build(); // Returns "1d at 2pm"
    /// </code>
    /// </example>
    public string Build()
    {
        var spec = new ScheduleSpec
        {
            Interval = _interval,
            Unit = _unit,
            DayOfWeek = _dayOfWeek,
            DayPattern = _dayPattern,
            DayOfMonth = _dayOfMonth,
            TimeOfDay = _timeOfDay,
            TimeZone = _timeZone
        };

        var formatter = new NaturalLanguageFormatter();
        return formatter.Format(spec);
    }
}

/// <summary>
/// Static entry point for fluent schedule building
/// </summary>
public static class Schedule
{
    /// <summary>
    /// Start building a schedule with specified interval
    /// </summary>
    /// <param name="interval">Interval value (must be positive)</param>
    /// <returns>Builder instance for method chaining</returns>
    /// <example>
    /// <code>
    /// Schedule.Every(6).Hours().Build()
    /// Schedule.Every(1).Day().At(14, 0).Build()
    /// </code>
    /// </example>
    public static ScheduleBuilder Every(int interval) => ScheduleBuilder.Every(interval);
}
