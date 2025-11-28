using System;
using System.Collections.Generic;
using System.Linq;
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Utilities;
using NodaTime;

namespace HumanCron.NCrontab.Converters;

/// <summary>
/// Builds NCrontab 6-field cron expressions from ScheduleSpec
/// Format: second minute hour day month dayOfWeek
/// </summary>
internal sealed class NCrontabBuilder
{
    private readonly IClock _clock;
    private readonly DateTimeZone _localTimeZone;

    /// <summary>
    /// Create a new NCrontabBuilder (production use)
    /// </summary>
    internal static NCrontabBuilder Create()
    {
        var localTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault()
            ?? throw new InvalidOperationException(
                "Could not determine system timezone. NodaTime TZDB data may be corrupted.");

        return new NCrontabBuilder(
            SystemClock.Instance,
            localTimeZone
        );
    }

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    internal NCrontabBuilder(IClock clock, DateTimeZone localTimeZone)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
    }

    /// <summary>
    /// Build NCrontab 6-field cron expression from ScheduleSpec
    /// </summary>
    public ParseResult<string> Build(ScheduleSpec spec)
    {
        // Validate that spec can be expressed as NCrontab
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
            return new ParseResult<string>.Error($"Failed to build NCrontab expression: {ex.Message}");
        }
    }

    private static (bool isValid, string errorMessage) Validate(ScheduleSpec spec)
    {
        return spec.Unit switch
        {
            // NCrontab supports seconds (unlike Unix cron) ✓
            // NCrontab doesn't support multi-week/month/year intervals
            IntervalUnit.Weeks when spec.Interval > 1 => (false,
                $"NCrontab does not support multi-week intervals ({spec.Interval}w). Use Hangfire's CalendarIntervalSchedule instead."),
            IntervalUnit.Months when spec.Interval > 1 => (false,
                $"NCrontab does not support multi-month intervals ({spec.Interval}M)."),
            IntervalUnit.Years when spec.Interval > 1 => (false,
                $"NCrontab does not support multi-year intervals ({spec.Interval}y)."),
            _ => (true, string.Empty)
        };
    }

    private string BuildCronExpression(ScheduleSpec spec)
    {
        // NCrontab format: second minute hour day month dayOfWeek
        var second = GetSecondPart(spec);
        var minute = GetMinutePart(spec);
        var hour = GetHourPart(spec);
        var day = GetDayPart(spec);
        var month = GetMonthPart(spec);
        var dayOfWeek = GetDayOfWeekPart(spec);

        return $"{second} {minute} {hour} {day} {month} {dayOfWeek}";
    }

    private static string GetSecondPart(ScheduleSpec spec)
    {
        // Second list (0,15,30,45) - compact consecutive sequences to ranges
        if (spec.SecondList is { Count: > 0 })
        {
            return CronFormatHelpers.CompactList(spec.SecondList);
        }

        // Second range (0-30) or range+step (0-30/5)
        if (spec is { SecondStart: not null, SecondEnd: not null })
        {
            var range = $"{spec.SecondStart.Value}-{spec.SecondEnd.Value}";
            return spec.SecondStep.HasValue ? $"{range}/{spec.SecondStep.Value}" : range;
        }

        // Second interval (*/5, */30)
        if (spec.Unit == IntervalUnit.Seconds)
        {
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // Specific second value
        if (spec.Second.HasValue)
        {
            return spec.Second.Value.ToString();
        }

        return "0";  // Default to 0 seconds
    }

    private string GetMinutePart(ScheduleSpec spec)
    {
        // Minute list (0,15,30,45) - compact consecutive sequences to ranges
        if (spec.MinuteList is { Count: > 0 })
        {
            return CronFormatHelpers.CompactList(spec.MinuteList);
        }

        // Minute range (0-30) or range+step (0-30/5)
        if (spec is { MinuteStart: not null, MinuteEnd: not null })
        {
            var range = $"{spec.MinuteStart.Value}-{spec.MinuteEnd.Value}";
            return spec.MinuteStep.HasValue ? $"{range}/{spec.MinuteStep.Value}" : range;
        }

        if (spec.Unit == IntervalUnit.Seconds)
        {
            return "*";  // Every minute for second intervals
        }

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
        // Hour list (9,12,15,18) - compact consecutive sequences to ranges
        if (spec.HourList is { Count: > 0 })
        {
            return CronFormatHelpers.CompactList(spec.HourList);
        }

        // Hour range (9-17) or range+step (9-17/2)
        if (spec is { HourStart: not null, HourEnd: not null })
        {
            var range = $"{spec.HourStart.Value}-{spec.HourEnd.Value}";
            return spec.HourStep.HasValue ? $"{range}/{spec.HourStep.Value}" : range;
        }

        if (spec.Unit is IntervalUnit.Seconds or IntervalUnit.Minutes)
        {
            return "*";  // Every hour for second/minute intervals
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
    /// </summary>
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
        // Day list (1,15,30) - compact consecutive sequences to ranges
        if (spec.DayList is { Count: > 0 })
        {
            return CronFormatHelpers.CompactList(spec.DayList);
        }

        // Day range (1-15) or range+step (1-15/3)
        if (spec is { DayStart: not null, DayEnd: not null })
        {
            var range = $"{spec.DayStart.Value}-{spec.DayEnd.Value}";
            return spec.DayStep.HasValue ? $"{range}/{spec.DayStep.Value}" : range;
        }

        // Day-of-month (1-31) - use * when using day-of-week
        if (spec.DayOfWeek.HasValue || spec.DayPattern.HasValue || spec.DayOfWeekList != null || spec.DayOfWeekStart.HasValue)
        {
            return "*";  // Wildcard when day-of-week is specified
        }

        // Specific day-of-month for monthly/yearly schedules
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
            MonthSpecifier.Single single => single.Month.ToString(),  // Specific month
            MonthSpecifier.Range range => $"{range.Start}-{range.End}",  // Month range
            MonthSpecifier.List list => string.Join(",", list.Months),  // Month list
            _ => throw new InvalidOperationException($"Unknown month specifier type: {spec.Month.GetType().Name}")
        };
    }

    private string GetDayOfWeekPart(ScheduleSpec spec)
    {
        // Day-of-week list (e.g., "every monday,wednesday,friday" → "1,3,5")
        if (spec.DayOfWeekList is { Count: > 0 } dayList)
        {
            var dayNumbers = dayList.Select(ConvertDayOfWeek);
            return string.Join(",", dayNumbers);
        }

        // Day-of-week custom range (e.g., "every tuesday-thursday" → "2,3,4")
        if (spec is { DayOfWeekStart: not null, DayOfWeekEnd: not null })
        {
            var days = ExpandDayOfWeekRange(spec.DayOfWeekStart.Value, spec.DayOfWeekEnd.Value);
            return string.Join(",", days.Select(ConvertDayOfWeek));
        }

        // Day pattern (weekdays, weekends)
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

        // Default weekly intervals to current day of week
        if (spec is { Unit: IntervalUnit.Weeks, Interval: 1 })
        {
            var now = _clock.GetCurrentInstant();
            var isoDayOfWeek = now.InZone(_localTimeZone).DayOfWeek;
            var bclDayOfWeek = ConvertIsoDayOfWeekToBcl(isoDayOfWeek);
            return ConvertDayOfWeek(bclDayOfWeek);
        }

        return "*";  // No specific day-of-week constraint
    }

    /// <summary>
    /// Expand day-of-week range to list of days
    /// Handles wraparound: Friday-Monday → [Friday, Saturday, Sunday, Monday]
    /// </summary>
    private static IReadOnlyList<DayOfWeek> ExpandDayOfWeekRange(DayOfWeek start, DayOfWeek end)
    {
        var startNum = (int)start;
        var endNum = (int)end;

        if (startNum <= endNum)
        {
            // Simple range: Tuesday-Thursday = [2,3,4]
            var result = new DayOfWeek[endNum - startNum + 1];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (DayOfWeek)(startNum + i);
            }
            return result;
        }
        else
        {
            // Wraparound: Friday-Monday = [5,6,0,1]
            var count = (6 - startNum + 1) + (endNum + 1);
            var result = new DayOfWeek[count];
            int idx = 0;
            for (int i = startNum; i <= 6; i++)
            {
                result[idx++] = (DayOfWeek)i;
            }
            for (int i = 0; i <= endNum; i++)
            {
                result[idx++] = (DayOfWeek)i;
            }
            return result;
        }
    }

    /// <summary>
    /// Convert NodaTime IsoDayOfWeek to BCL DayOfWeek
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
        // NCrontab uses 0-6 (0 = Sunday)
        // BCL DayOfWeek enum matches exactly: Sunday=0, Monday=1, ..., Saturday=6
        return ((int)day).ToString();
    }
}
