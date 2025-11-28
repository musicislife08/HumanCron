using System;
using System.Collections.Generic;
using HumanCron.Models;
using HumanCron.Models.Internal;

namespace HumanCron.NCrontab.Converters;

/// <summary>
/// Parses NCrontab 6-field cron expressions back into ScheduleSpec
/// Format: second minute hour day month dayOfWeek
/// </summary>
internal sealed class NCrontabParser
{
    private const int MaxInterval = 1000;

    /// <summary>
    /// Parse NCrontab 6-field cron expression into ScheduleSpec
    /// </summary>
    public ParseResult<ScheduleSpec> Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return new ParseResult<ScheduleSpec>.Error("NCrontab expression cannot be empty");
        }

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6)
        {
            return new ParseResult<ScheduleSpec>.Error($"NCrontab expressions must have 6 fields (got {parts.Length}). Format: second minute hour day month dayOfWeek");
        }

        try
        {
            var second = parts[0];
            var minute = parts[1];
            var hour = parts[2];
            var day = parts[3];
            var month = parts[4];
            var dayOfWeek = parts[5];

            // Validate field ranges before parsing
            var validationError = ValidateFields(second, minute, hour, day, month, dayOfWeek);
            if (validationError != null)
            {
                return new ParseResult<ScheduleSpec>.Error(validationError);
            }

            // Determine interval unit and value based on pattern
            var (interval, unit) = DetermineInterval(second, minute, hour, day, dayOfWeek);
            if (interval == 0)
            {
                return new ParseResult<ScheduleSpec>.Error($"Could not determine interval from NCrontab expression: {cronExpression}");
            }

            // Parse day-of-week if specified
            var parsedDayOfWeek = ParseDayOfWeek(dayOfWeek);
            var parsedDayPattern = ParseDayPattern(dayOfWeek);
            var parsedDayOfWeekList = ParseDayOfWeekList(dayOfWeek);
            var (dayOfWeekStart, dayOfWeekEnd) = ParseDayOfWeekRange(dayOfWeek);

            // Parse time-of-day if specified
            var timeOfDay = ParseTimeOfDay(minute, hour, unit);

            // Parse month specifier
            var monthSpecifier = ParseMonthSpecifier(month);

            // Parse ranges and lists for second, minute, hour, and day fields
            var (secondStart, secondEnd, secondStep) = ParseRange(second);
            var (minuteStart, minuteEnd, minuteStep) = ParseRange(minute);
            var (hourStart, hourEnd, hourStep) = ParseRange(hour);
            var (dayStart, dayEnd, dayStep) = ParseRange(day);

            var secondList = ParseList(second, 0, 59);
            var minuteList = ParseList(minute, 0, 59);
            var hourList = ParseList(hour, 0, 23);
            var dayList = ParseList(day, 1, 31);

            // Parse single day-of-month value
            int? dayOfMonth = null;
            if (day != "*" && !day.Contains('-') && !day.Contains(',') && !day.Contains('/'))
            {
                if (int.TryParse(day, out var dayValue) && dayValue is >= 1 and <= 31)
                {
                    dayOfMonth = dayValue;
                }
            }

            // Parse single second value
            int? secondValue = null;
            if (second != "*" && !second.Contains('-') && !second.Contains(',') && !second.Contains('/'))
            {
                if (int.TryParse(second, out var secValue) && secValue is >= 0 and <= 59)
                {
                    secondValue = secValue;
                }
            }

            var spec = new ScheduleSpec
            {
                Interval = interval,
                Unit = unit,
                DayOfWeek = parsedDayOfWeek,
                DayPattern = parsedDayPattern,
                DayOfWeekList = parsedDayOfWeekList,
                DayOfWeekStart = dayOfWeekStart,
                DayOfWeekEnd = dayOfWeekEnd,
                DayOfMonth = dayOfMonth,
                Month = monthSpecifier,
                TimeOfDay = timeOfDay,
                Second = secondValue,
                SecondStart = secondStart,
                SecondEnd = secondEnd,
                SecondStep = secondStep,
                SecondList = secondList,
                MinuteStart = minuteStart,
                MinuteEnd = minuteEnd,
                MinuteStep = minuteStep,
                MinuteList = minuteList,
                HourStart = hourStart,
                HourEnd = hourEnd,
                HourStep = hourStep,
                HourList = hourList,
                DayStart = dayStart,
                DayEnd = dayEnd,
                DayStep = dayStep,
                DayList = dayList
            };

            return new ParseResult<ScheduleSpec>.Success(spec);
        }
        catch (Exception ex)
        {
            return new ParseResult<ScheduleSpec>.Error($"Failed to parse NCrontab expression: {ex.Message}");
        }
    }

    private static (int interval, IntervalUnit unit) DetermineInterval(string second, string minute, string hour, string day, string dayOfWeek)
    {
        // Pattern: */N in seconds field = every N seconds
        if (second.StartsWith("*/"))
        {
            if (int.TryParse(second[2..], out var interval) && interval > 0 && interval <= MaxInterval)
            {
                return (interval, IntervalUnit.Seconds);
            }
        }

        // Pattern: * or 0 in seconds field, */N in minutes field = every N minutes
        if ((second == "*" || second == "0") && minute.StartsWith("*/"))
        {
            if (int.TryParse(minute[2..], out var interval) && interval > 0 && interval <= MaxInterval)
            {
                return (interval, IntervalUnit.Minutes);
            }
        }

        // Pattern: 0 0 */N = every N hours
        if (second == "0" && minute == "0" && hour.StartsWith("*/"))
        {
            if (int.TryParse(hour[2..], out var interval) && interval > 0 && interval <= MaxInterval)
            {
                return (interval, IntervalUnit.Hours);
            }
        }

        // Pattern: 0 0 * */N = every N days
        if (second == "0" && minute == "0" && hour == "*" && day.StartsWith("*/"))
        {
            if (int.TryParse(day[2..], out var interval) && interval > 0 && interval <= MaxInterval)
            {
                return (interval, IntervalUnit.Days);
            }
        }

        // Pattern: specific day-of-week = weekly
        if (dayOfWeek != "*" && day == "*")
        {
            return (1, IntervalUnit.Weeks);
        }

        // Pattern: specific time but no interval = daily
        if (second is "0" && minute != "*" && hour != "*" && day == "*" && dayOfWeek == "*")
        {
            return (1, IntervalUnit.Days);
        }

        // Pattern: * in seconds, * in minutes = every minute
        if (second == "*" && minute == "*")
        {
            return (1, IntervalUnit.Minutes);
        }

        // Pattern: 0 in seconds, * in minutes = every minute (starting at 0 seconds)
        if (second == "0" && minute == "*" && hour == "*")
        {
            return (1, IntervalUnit.Minutes);
        }

        // Pattern: 0 0 * = every hour
        if (second == "0" && minute == "0" && hour == "*")
        {
            return (1, IntervalUnit.Hours);
        }

        // Default to daily
        return (1, IntervalUnit.Days);
    }

    private static TimeOnly? ParseTimeOfDay(string minute, string hour, IntervalUnit unit)
    {
        // Only parse time for daily/weekly schedules
        if (unit is not (IntervalUnit.Days or IntervalUnit.Weeks))
        {
            return null;
        }

        // Parse specific hour and minute values
        if (int.TryParse(hour, out var h) && int.TryParse(minute, out var m))
        {
            if (h is >= 0 and <= 23 && m is >= 0 and <= 59)
            {
                return new TimeOnly(h, m);
            }
        }

        return null;
    }

    private static MonthSpecifier ParseMonthSpecifier(string month)
    {
        if (month == "*")
        {
            return new MonthSpecifier.None();
        }

        // Single month: "1", "JAN"
        if (!month.Contains('-') && !month.Contains(','))
        {
            var monthNum = ParseMonthValue(month);
            if (monthNum.HasValue)
            {
                return new MonthSpecifier.Single(monthNum.Value);
            }
        }

        // Month range: "1-3", "JAN-MAR"
        if (month.Contains('-') && !month.Contains(','))
        {
            var rangeParts = month.Split('-');
            if (rangeParts.Length == 2)
            {
                var start = ParseMonthValue(rangeParts[0]);
                var end = ParseMonthValue(rangeParts[1]);
                if (start.HasValue && end.HasValue)
                {
                    return new MonthSpecifier.Range(start.Value, end.Value);
                }
            }
        }

        // Month list: "1,4,7,10", "JAN,APR,JUL,OCT"
        if (month.Contains(','))
        {
            var monthParts = month.Split(',');
            List<int> months = [];
            foreach (var part in monthParts)
            {
                var monthNum = ParseMonthValue(part.Trim());
                if (monthNum.HasValue)
                {
                    months.Add(monthNum.Value);
                }
            }
            if (months.Count > 0)
            {
                return new MonthSpecifier.List(months);
            }
        }

        return new MonthSpecifier.None();
    }

    private static int? ParseMonthValue(string month)
    {
        // Numeric month (1-12)
        if (int.TryParse(month, out var monthNum) && monthNum is >= 1 and <= 12)
        {
            return monthNum;
        }

        // Named month (JAN-DEC)
        return month.ToUpperInvariant() switch
        {
            "JAN" or "JANUARY" => 1,
            "FEB" or "FEBRUARY" => 2,
            "MAR" or "MARCH" => 3,
            "APR" or "APRIL" => 4,
            "MAY" => 5,
            "JUN" or "JUNE" => 6,
            "JUL" or "JULY" => 7,
            "AUG" or "AUGUST" => 8,
            "SEP" or "SEPTEMBER" => 9,
            "OCT" or "OCTOBER" => 10,
            "NOV" or "NOVEMBER" => 11,
            "DEC" or "DECEMBER" => 12,
            _ => null
        };
    }

    private static DayOfWeek? ParseDayOfWeek(string dayOfWeek)
    {
        if (dayOfWeek == "*" || dayOfWeek.Contains('-') || dayOfWeek.Contains(','))
        {
            return null;
        }

        // Numeric day (0-7, both 0 and 7 = Sunday)
        if (int.TryParse(dayOfWeek, out var dayNum))
        {
            return dayNum switch
            {
                0 or 7 => DayOfWeek.Sunday,
                1 => DayOfWeek.Monday,
                2 => DayOfWeek.Tuesday,
                3 => DayOfWeek.Wednesday,
                4 => DayOfWeek.Thursday,
                5 => DayOfWeek.Friday,
                6 => DayOfWeek.Saturday,
                _ => null
            };
        }

        // Named day (SUN-SAT)
        return dayOfWeek.ToUpperInvariant() switch
        {
            "SUN" or "SUNDAY" => DayOfWeek.Sunday,
            "MON" or "MONDAY" => DayOfWeek.Monday,
            "TUE" or "TUESDAY" => DayOfWeek.Tuesday,
            "WED" or "WEDNESDAY" => DayOfWeek.Wednesday,
            "THU" or "THURSDAY" => DayOfWeek.Thursday,
            "FRI" or "FRIDAY" => DayOfWeek.Friday,
            "SAT" or "SATURDAY" => DayOfWeek.Saturday,
            _ => null
        };
    }

    private static DayPattern? ParseDayPattern(string dayOfWeek)
    {
        // Weekdays: "1-5"
        if (dayOfWeek == "1-5")
        {
            return DayPattern.Weekdays;
        }

        // Weekends: "0,6" or "6,0"
        if (dayOfWeek is "0,6" or "6,0")
        {
            return DayPattern.Weekends;
        }

        return null;
    }

    private static IReadOnlyList<DayOfWeek>? ParseDayOfWeekList(string dayOfWeek)
    {
        if (!dayOfWeek.Contains(',') || dayOfWeek is "0,6" or "6,0" or "1-5")
        {
            return null;
        }

        var parts = dayOfWeek.Split(',');
        List<DayOfWeek> days = [];

        foreach (var part in parts)
        {
            var day = ParseDayOfWeek(part.Trim());
            if (day.HasValue)
            {
                days.Add(day.Value);
            }
        }

        return days.Count > 0 ? days : null;
    }

    private static (DayOfWeek? start, DayOfWeek? end) ParseDayOfWeekRange(string dayOfWeek)
    {
        if (!dayOfWeek.Contains('-') || dayOfWeek == "1-5")
        {
            return (null, null);
        }

        var parts = dayOfWeek.Split('-');
        if (parts.Length == 2)
        {
            var start = ParseDayOfWeek(parts[0].Trim());
            var end = ParseDayOfWeek(parts[1].Trim());
            if (start.HasValue && end.HasValue)
            {
                return (start.Value, end.Value);
            }
        }

        return (null, null);
    }

    private static (int? start, int? end, int? step) ParseRange(string field)
    {
        if (!field.Contains('-'))
        {
            return (null, null, null);
        }

        // Range with step: "0-30/5"
        var stepParts = field.Split('/');
        var rangePart = stepParts[0];
        int? step = null;

        if (stepParts.Length == 2 && int.TryParse(stepParts[1], out var stepValue))
        {
            step = stepValue;
        }

        // Parse range: "0-30"
        var rangeBounds = rangePart.Split('-');
        if (rangeBounds.Length == 2 &&
            int.TryParse(rangeBounds[0], out var start) &&
            int.TryParse(rangeBounds[1], out var end))
        {
            return (start, end, step);
        }

        return (null, null, null);
    }

    private static IReadOnlyList<int>? ParseList(string field, int min, int max)
    {
        if (!field.Contains(','))
        {
            return null;
        }

        var parts = field.Split(',');
        List<int> values = [];

        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var value) && value >= min && value <= max)
            {
                values.Add(value);
            }
        }

        return values.Count > 0 ? values : null;
    }

    /// <summary>
    /// Validate all fields are within valid NCrontab ranges
    /// </summary>
    private static string? ValidateFields(string second, string minute, string hour, string day, string month, string dayOfWeek)
    {
        // Validate second field (0-59)
        var secondError = ValidateField(second, 0, 59, "Second");
        if (secondError != null) return secondError;

        // Validate minute field (0-59)
        var minuteError = ValidateField(minute, 0, 59, "Minute");
        if (minuteError != null) return minuteError;

        // Validate hour field (0-23)
        var hourError = ValidateField(hour, 0, 23, "Hour");
        if (hourError != null) return hourError;

        // Validate day field (1-31)
        var dayError = ValidateField(day, 1, 31, "Day");
        if (dayError != null) return dayError;

        // Validate month field (1-12)
        var monthError = ValidateField(month, 1, 12, "Month");
        if (monthError != null) return monthError;

        // Validate day-of-week field (0-7, both 0 and 7 = Sunday)
        var dayOfWeekError = ValidateField(dayOfWeek, 0, 7, "Day-of-week");
        if (dayOfWeekError != null) return dayOfWeekError;

        return null;
    }

    /// <summary>
    /// Validate a single cron field against min/max range
    /// Handles wildcards (*), ranges (1-5), lists (1,2,3), and steps (*/5)
    /// </summary>
    private static string? ValidateField(string field, int min, int max, string fieldName)
    {
        // Wildcard is always valid
        if (field == "*") return null;

        // Handle step values: */N or N-M/S
        var stepParts = field.Split('/');
        var valueToValidate = stepParts[0];

        // Validate step value if present
        if (stepParts.Length == 2)
        {
            if (!int.TryParse(stepParts[1], out var step) || step < 1)
            {
                return $"{fieldName} step value must be >= 1, got '{stepParts[1]}'";
            }
        }

        // If base is wildcard, step is already validated
        if (valueToValidate == "*") return null;

        // Handle ranges: N-M
        if (valueToValidate.Contains('-'))
        {
            var rangeParts = valueToValidate.Split('-');
            if (rangeParts.Length != 2)
            {
                return $"{fieldName} range must be in format 'N-M', got '{valueToValidate}'";
            }

            if (!int.TryParse(rangeParts[0], out var rangeStart) || rangeStart < min || rangeStart > max)
            {
                return $"{fieldName} range start must be {min}-{max}, got '{rangeParts[0]}'";
            }

            if (!int.TryParse(rangeParts[1], out var rangeEnd) || rangeEnd < min || rangeEnd > max)
            {
                return $"{fieldName} range end must be {min}-{max}, got '{rangeParts[1]}'";
            }

            // Allow wraparound ranges (e.g., 22-6 for 10pm to 6am)
            // Both values are valid, wraparound is OK

            return null;
        }

        // Handle lists: N,M,O
        if (valueToValidate.Contains(','))
        {
            var listParts = valueToValidate.Split(',');
            foreach (var part in listParts)
            {
                if (!int.TryParse(part.Trim(), out var value) || value < min || value > max)
                {
                    return $"{fieldName} list value must be {min}-{max}, got '{part}'";
                }
            }
            return null;
        }

        // Handle single numeric value
        if (int.TryParse(valueToValidate, out var numValue))
        {
            if (numValue < min || numValue > max)
            {
                return $"{fieldName} must be {min}-{max}, got {numValue}";
            }
            return null;
        }

        // If we get here, it's an invalid format
        return $"{fieldName} has invalid format: '{field}'";
    }
}
