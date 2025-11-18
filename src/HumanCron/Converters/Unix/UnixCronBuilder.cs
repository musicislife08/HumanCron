using HumanCron.Models.Internal;
using System;
using HumanCron.Models;
using NodaTime;

namespace HumanCron.Converters.Unix;

/// <summary>
/// Builds Unix 5-part cron expressions from ScheduleSpec
/// Format: minute hour day month dayOfWeek
/// </summary>
internal sealed class UnixCronBuilder
{
    private readonly IClock _clock;
    private readonly DateTimeZone _localTimeZone;

    /// <summary>
    /// Create a new UnixCronBuilder (production use)
    /// </summary>
    internal static UnixCronBuilder Create()
    {
        var localTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault()
            ?? throw new InvalidOperationException(
                "Could not determine system timezone. NodaTime TZDB data may be corrupted.");

        return new UnixCronBuilder(
            SystemClock.Instance,
            localTimeZone
        );
    }

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    /// <param name="clock">Clock for date/time operations (SystemClock.Instance in production, FakeClock in tests)</param>
    /// <param name="localTimeZone">Server's local timezone for cron execution (GetSystemDefault() in production, explicit timezone in tests)</param>
    internal UnixCronBuilder(IClock clock, DateTimeZone localTimeZone)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
    }
    /// <summary>
    /// Build Unix 5-part cron expression from ScheduleSpec
    /// </summary>
    /// <param name="spec">Schedule specification</param>
    /// <returns>ParseResult with Unix cron expression or error</returns>
    public ParseResult<string> Build(ScheduleSpec spec)
    {
        // Validate that spec can be expressed as Unix cron
        var validation = Validate(spec);
        if (!validation.isValid)
        {
            return new ParseResult<string>.Error(validation.errorMessage);
        }

        try
        {
            var cronExpression = BuildCronExpression(spec);
            return new ParseResult<string>.Success(cronExpression);
        }
        catch (Exception ex)
        {
            return new ParseResult<string>.Error($"Failed to build cron expression: {ex.Message}");
        }
    }

    private static (bool isValid, string errorMessage) Validate(ScheduleSpec spec)
    {
        return spec.Unit switch
        {
            // Unix cron doesn't support seconds-level precision
            IntervalUnit.Seconds => (false,
                "Unix cron does not support seconds-level precision. Use minutes or larger intervals."),
            // Unix cron doesn't support multi-week/month/year intervals
            IntervalUnit.Weeks when spec.Interval > 1 => (false,
                $"Unix cron does not support multi-week intervals ({spec.Interval}w). Use '1w' with specific day-of-week."),
            IntervalUnit.Months when spec.Interval > 1 => (false,
                $"Unix cron does not support multi-month intervals ({spec.Interval}M)."),
            IntervalUnit.Years when spec.Interval > 1 => (false,
                $"Unix cron does not support multi-year intervals ({spec.Interval}y)."),
            _ => (true, string.Empty)
        };
    }

    private string BuildCronExpression(ScheduleSpec spec)
    {
        // Unix cron format: minute hour day month dayOfWeek
        var minute = GetMinutePart(spec);
        var hour = GetHourPart(spec);
        var day = GetDayPart(spec);
        var month = GetMonthPart(spec);
        var dayOfWeek = GetDayOfWeekPart(spec);

        return $"{minute} {hour} {day} {month} {dayOfWeek}";
    }

    private string GetMinutePart(ScheduleSpec spec)
    {
        if (spec.Unit == IntervalUnit.Minutes)
        {
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // If time specified, convert timezone and use minute component
        if (spec.TimeOfDay.HasValue)
        {
            var convertedTime = ConvertTimeToLocal(spec.TimeOfDay.Value, spec.TimeZone);
            return convertedTime.Minute.ToString();
        }

        return "0";  // Default to 0 minutes (top of hour)
    }

    private string GetHourPart(ScheduleSpec spec)
    {
        if (spec.Unit == IntervalUnit.Minutes)
        {
            return "*";  // Every hour for minute intervals
        }

        if (spec.Unit == IntervalUnit.Hours)
        {
            // If time specified, it's the starting hour with interval
            if (spec.TimeOfDay.HasValue)
            {
                var convertedTime = ConvertTimeToLocal(spec.TimeOfDay.Value, spec.TimeZone);
                var startHour = convertedTime.Hour;
                return spec.Interval == 1
                    ? "*"
                    : $"{startHour}/{spec.Interval}";
            }
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // For daily/weekly, use specified time or midnight
        if (spec.TimeOfDay.HasValue)
        {
            var convertedTime = ConvertTimeToLocal(spec.TimeOfDay.Value, spec.TimeZone);
            return convertedTime.Hour.ToString();
        }

        return "0";  // Default to midnight
    }

    /// <summary>
    /// Convert time from user's timezone to server's local timezone
    /// Uses clock's current date for DST offset calculation (static conversion - see UnixCronConverter remarks)
    /// </summary>
    /// <param name="time">Time in user's timezone</param>
    /// <param name="sourceTimeZone">User's timezone</param>
    /// <returns>Time converted to server's local timezone</returns>
    private TimeOnly ConvertTimeToLocal(TimeOnly time, DateTimeZone sourceTimeZone)
    {
        // If source timezone is same as server timezone, no conversion needed
        if (sourceTimeZone.Equals(_localTimeZone))
        {
            return time;
        }

        // Use the clock's current instant to get today's date in the source timezone
        var now = _clock.GetCurrentInstant();
        var todayInSourceZone = now.InZone(sourceTimeZone).Date;

        // Create LocalDateTime in the source timezone
        var localTime = new LocalTime(time.Hour, time.Minute, time.Second);
        var localDateTime = todayInSourceZone + localTime;

        // Convert: source timezone → UTC → server local timezone
        var zonedDateTime = sourceTimeZone.AtLeniently(localDateTime);
        var localZonedDateTime = zonedDateTime.WithZone(_localTimeZone);

        return new TimeOnly(localZonedDateTime.Hour, localZonedDateTime.Minute, localZonedDateTime.Second);
    }

    private static string GetDayPart(ScheduleSpec spec)
    {
        // Day-of-month (1-31) - use * when using day-of-week
        if (spec.DayOfWeek.HasValue || spec.DayPattern.HasValue)
        {
            return "*";  // Wildcard when day-of-week is specified
        }

        // Specific day-of-month for monthly/yearly schedules: "on 15" → day field = "15"
        if (spec.DayOfMonth.HasValue)
        {
            return spec.DayOfMonth.Value.ToString();
        }

        if (spec.Unit == IntervalUnit.Days)
        {
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        return "*";  // Every day for sub-daily intervals
    }

    private static string GetMonthPart(ScheduleSpec spec)
    {
        // Pattern match on discriminated union for month selection
        return spec.Month switch
        {
            MonthSpecifier.None => "*",  // All months
            MonthSpecifier.Single single => single.Month.ToString(),  // Specific month: "1"
            MonthSpecifier.Range range => $"{range.Start}-{range.End}",  // Month range: "1-3"
            MonthSpecifier.List list => string.Join(",", list.Months),  // Month list: "1,4,7,10"
            _ => throw new InvalidOperationException($"Unknown month specifier type: {spec.Month.GetType().Name}")
        };
    }

    private string GetDayOfWeekPart(ScheduleSpec spec)
    {
        // Day-of-week (0-7, both 0 and 7 = Sunday)
        if (spec.DayPattern.HasValue)
        {
            return spec.DayPattern.Value switch
            {
                DayPattern.Weekdays => "1-5",      // Monday-Friday
                DayPattern.Weekends => "0,6",      // Sunday,Saturday
                _ => throw new InvalidOperationException($"Unknown day pattern: {spec.DayPattern}")
            };
        }

        if (spec.DayOfWeek.HasValue)
        {
            return ConvertDayOfWeek(spec.DayOfWeek.Value);
        }

        // Default weekly intervals to current day of week (using clock and server timezone)
        if (spec is not { Unit: IntervalUnit.Weeks, Interval: 1 }) return "*";
        var now = _clock.GetCurrentInstant();
        var isoDayOfWeek = now.InZone(_localTimeZone).DayOfWeek;
        var bclDayOfWeek = ConvertIsoDayOfWeekToBcl(isoDayOfWeek);
        return ConvertDayOfWeek(bclDayOfWeek);

        // No specific day-of-week constraint
    }

    /// <summary>
    /// Convert NodaTime IsoDayOfWeek to BCL DayOfWeek
    /// ISO: Monday=1, Sunday=7
    /// BCL: Sunday=0, Monday=1
    /// </summary>
    private static DayOfWeek ConvertIsoDayOfWeekToBcl(IsoDayOfWeek isoDayOfWeek)
    {
        return isoDayOfWeek switch
        {
            IsoDayOfWeek.Monday => DayOfWeek.Monday,
            IsoDayOfWeek.Tuesday => DayOfWeek.Tuesday,
            IsoDayOfWeek.Wednesday => DayOfWeek.Wednesday,
            IsoDayOfWeek.Thursday => DayOfWeek.Thursday,
            IsoDayOfWeek.Friday => DayOfWeek.Friday,
            IsoDayOfWeek.Saturday => DayOfWeek.Saturday,
            IsoDayOfWeek.Sunday => DayOfWeek.Sunday,
            _ => throw new InvalidOperationException($"Unknown ISO day of week: {isoDayOfWeek}")
        };
    }

    private static string ConvertDayOfWeek(DayOfWeek day)
    {
        // Unix cron uses 0-7 (0 and 7 = Sunday)
        return day switch
        {
            DayOfWeek.Sunday => "0",
            DayOfWeek.Monday => "1",
            DayOfWeek.Tuesday => "2",
            DayOfWeek.Wednesday => "3",
            DayOfWeek.Thursday => "4",
            DayOfWeek.Friday => "5",
            DayOfWeek.Saturday => "6",
            _ => throw new InvalidOperationException($"Unknown day of week: {day}")
        };
    }
}
