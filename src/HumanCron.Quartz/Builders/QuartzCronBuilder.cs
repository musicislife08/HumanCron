using HumanCron.Models.Internal;
using HumanCron.Quartz.Helpers;
using HumanCron.Utilities;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
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
        // Quartz cron format: second minute hour day month dayOfWeek [year]
        var second = GetSecondPart(spec);
        var minute = GetMinutePart(spec);
        var hour = GetHourPart(spec);
        var day = GetDayPart(spec);
        var month = GetMonthPart(spec);
        var dayOfWeek = GetDayOfWeekPart(spec);

        // Optional 7th field: year (1970-2099)
        return spec.Year.HasValue ? $"{second} {minute} {hour} {day} {month} {dayOfWeek} {spec.Year.Value}" : 
            $"{second} {minute} {hour} {day} {month} {dayOfWeek}";
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

        switch (spec.Unit)
        {
            case NaturalIntervalUnit.Seconds:
            case NaturalIntervalUnit.Minutes:
                return "*";  // Every hour for sub-hourly intervals
            // If time specified, it's the starting hour with interval
            case NaturalIntervalUnit.Hours when spec.TimeOfDay.HasValue:
            {
                var startHour = spec.TimeOfDay.Value.Hour;
                return spec.Interval == 1
                    ? "*"
                    : $"{startHour}/{spec.Interval}";
            }
            case NaturalIntervalUnit.Hours:
                return spec.Interval == 1 ? "*" : $"*/{spec.Interval}";
        }

        // For daily/weekly, use specified time or midnight
        return spec.TimeOfDay.HasValue ? spec.TimeOfDay.Value.Hour.ToString() : "0"; // Default to midnight
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

        // Advanced Quartz features in day field (L, W)

        // Last day offset: "3rd to last day" → "L-3"
        if (spec.LastDayOffset.HasValue)
        {
            return $"L-{spec.LastDayOffset.Value}";
        }

        // Last weekday: "last weekday" → "LW"
        if (spec is { IsLastDay: true, IsNearestWeekday: true })
        {
            return "LW";
        }

        // Last day: "last day" → "L"
        if (spec.IsLastDay)
        {
            return "L";
        }

        // Nearest weekday: "weekday nearest 15" → "15W"
        if (spec is { IsNearestWeekday: true, DayOfMonth: not null })
        {
            return $"{spec.DayOfMonth.Value}W";
        }

        // Day-of-month (1-31) - only use when NOT using day-of-week
        if (spec.DayOfWeek.HasValue || spec.DayOfWeekList is { Count: > 0 } ||
            spec is { DayOfWeekStart: not null, DayOfWeekEnd: not null } ||
            spec.DayPattern.HasValue || spec.NthOccurrence.HasValue || spec.IsLastDayOfWeek)
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
        // Advanced Quartz features in day-of-week field (#, L)

        switch (spec)
        {
            // Nth occurrence: "3rd friday" → "6#3"
            case { NthOccurrence: not null, DayOfWeek: not null }:
            {
                var quartzDay = ConvertDayOfWeekToNumber(spec.DayOfWeek.Value);
                return $"{quartzDay}#{spec.NthOccurrence.Value}";
            }
            // Last occurrence of day-of-week: "last friday" → "6L"
            case { IsLastDayOfWeek: true, DayOfWeek: not null }:
            {
                var quartzDay = ConvertDayOfWeekToNumber(spec.DayOfWeek.Value);
                return $"{quartzDay}L";
            }
        }

        // Day-of-week list (e.g., "every monday,wednesday,friday" → "MON,WED,FRI")
        if (spec.DayOfWeekList is { Count: > 0 } dayList)
        {
            var dayNames = dayList.Select(ConvertDayOfWeek);
            return string.Join(",", dayNames);
        }

        // Day-of-week custom range (e.g., "every tuesday-thursday" → "TUE,WED,THU")
        if (spec is { DayOfWeekStart: not null, DayOfWeekEnd: not null })
        {
            var days = ExpandDayOfWeekRange(spec.DayOfWeekStart.Value, spec.DayOfWeekEnd.Value);
            return string.Join(",", days.Select(ConvertDayOfWeek));
        }

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

        return spec.DayOfWeek.HasValue ? ConvertDayOfWeek(spec.DayOfWeek.Value) :
            // If using day-of-month, use ?
            "?"; // No specific day-of-week
    }

    /// <summary>
    /// Expand day-of-week range to list of days
    /// Handles wraparound: Friday-Monday → [Friday, Saturday, Sunday, Monday]
    /// </summary>
    private static IEnumerable<DayOfWeek> ExpandDayOfWeekRange(DayOfWeek start, DayOfWeek end)
    {
        var startNum = (int)start;
        var endNum = (int)end;

        if (startNum <= endNum)
        {
            // Simple range: Tuesday-Thursday = [2,3,4]
            for (int i = startNum; i <= endNum; i++)
            {
                yield return (DayOfWeek)i;
            }
        }
        else
        {
            // Wraparound: Friday-Monday = [5,6,0,1]
            for (int i = startNum; i <= 6; i++)
            {
                yield return (DayOfWeek)i;
            }
            for (int i = 0; i <= endNum; i++)
            {
                yield return (DayOfWeek)i;
            }
        }
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

    private static int ConvertDayOfWeekToNumber(DayOfWeek day)
    {
        // Quartz uses 1-7 where 1=Sunday
        // .NET DayOfWeek uses 0=Sunday, so we add 1
        return ((int)day) + 1;
    }

}
