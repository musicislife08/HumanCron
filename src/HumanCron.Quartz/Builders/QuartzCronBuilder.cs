using HumanCron.Models.Internal;
using HumanCron.Quartz.Helpers;
using Quartz;
using System;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz;

/// <summary>
/// Builds Quartz CronScheduleBuilder from ScheduleSpec
/// Generates 6-part Quartz cron expressions: second minute hour day month dayOfWeek
/// </summary>
internal sealed class QuartzCronBuilder
{
    public IScheduleBuilder Build(ScheduleSpec spec)
    {
        var cronExpression = BuildCronExpression(spec);
        var builder = CronScheduleBuilder.CronSchedule(cronExpression);

        // Apply timezone (convert NodaTime → BCL for Quartz)
        var timeZoneInfo = TimeZoneConverter.ToTimeZoneInfo(spec.TimeZone);
        builder = builder.InTimeZone(timeZoneInfo);

        return builder;
    }

    private static string BuildCronExpression(ScheduleSpec spec)
    {
        // Quartz cron format: second minute hour day month dayOfWeek
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
        if (spec.Unit == NaturalIntervalUnit.Seconds)
        {
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }
        return "0";  // Default to 0 seconds
    }

    private static string GetMinutePart(ScheduleSpec spec)
    {
        if (spec.Unit == NaturalIntervalUnit.Seconds)
        {
            return "*";  // Every minute when using seconds
        }

        if (spec.Unit == NaturalIntervalUnit.Minutes)
        {
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // If time specified, use minute component
        if (spec.TimeOfDay.HasValue)
        {
            return spec.TimeOfDay.Value.Minute.ToString();
        }

        return "0";  // Default to 0 minutes (top of hour)
    }

    private static string GetHourPart(ScheduleSpec spec)
    {
        if (spec.Unit == NaturalIntervalUnit.Seconds || spec.Unit == NaturalIntervalUnit.Minutes)
        {
            return "*";  // Every hour for sub-hourly intervals
        }

        if (spec.Unit == NaturalIntervalUnit.Hours)
        {
            // If time specified, it's the starting hour with interval
            if (spec.TimeOfDay.HasValue)
            {
                var startHour = spec.TimeOfDay.Value.Hour;
                return spec.Interval == 1
                    ? "*"
                    : $"{startHour}/{spec.Interval}";
            }
            return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // For daily/weekly, use specified time or midnight
        if (spec.TimeOfDay.HasValue)
        {
            return spec.TimeOfDay.Value.Hour.ToString();
        }

        return "0";  // Default to midnight
    }

    private static string GetDayPart(ScheduleSpec spec)
    {
        // Day-of-month (1-31) - only use when NOT using day-of-week
        if (spec.DayOfWeek.HasValue || spec.DayPattern.HasValue)
        {
            return "?";  // Use ? when day-of-week is specified (Quartz requirement)
        }

        // Specific day-of-month for monthly/yearly schedules: "on 15" → day field = "15"
        if (spec.DayOfMonth.HasValue)
        {
            return spec.DayOfMonth.Value.ToString();
        }

        if (spec.Unit == NaturalIntervalUnit.Days)
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

    private static string GetDayOfWeekPart(ScheduleSpec spec)
    {
        // Day-of-week (SUN-SAT or 1-7)
        if (spec.DayPattern.HasValue)
        {
            return spec.DayPattern.Value switch
            {
                DayPattern.Weekdays => "MON-FRI",
                DayPattern.Weekends => "SAT,SUN",
                _ => throw new InvalidOperationException($"Unknown day pattern: {spec.DayPattern}")
            };
        }

        if (spec.DayOfWeek.HasValue)
        {
            return ConvertDayOfWeek(spec.DayOfWeek.Value);
        }

        // If using day-of-month, use ?
        return "?";  // No specific day-of-week
    }

    private static string ConvertDayOfWeek(DayOfWeek day)
    {
        // Quartz uses SUN-SAT (or 1-7 where 1=Sunday)
        return day switch
        {
            DayOfWeek.Sunday => "SUN",
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => throw new InvalidOperationException($"Unknown day of week: {day}")
        };
    }
}
