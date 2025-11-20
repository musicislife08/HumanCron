using HumanCron.Models.Internal;
using HumanCron.Abstractions;
using HumanCron.Models;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Main Parse() method and class declaration
/// </summary>
internal sealed partial class NaturalLanguageParser : IScheduleParser
{
    public ParseResult<ScheduleSpec> Parse(string naturalLanguage, ScheduleParserOptions options)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<ScheduleSpec>.Error("Input cannot be empty");
        }

        var input = naturalLanguage.Trim();

        // Check if this is an "on" pattern (for advanced Quartz features with months)
        var isOnPattern = input.StartsWith("on ", System.StringComparison.OrdinalIgnoreCase);

        // Validate "every" or "on" is present
        if (!input.StartsWith("every", System.StringComparison.OrdinalIgnoreCase) && !isOnPattern)
        {
            return new ParseResult<ScheduleSpec>.Error("All schedules must start with 'every' or 'on'. For example: 'every 30 minutes', 'every day', 'on last day in january'");
        }

        // Check for range+step pattern FIRST (highest priority, most specific)
        // "every 5 minutes between 0 and 30 of each hour"
        var rangeStepMatch = RangeStepPattern().Match(input);
        if (rangeStepMatch.Success)
        {
            return ParseRangeStepPattern(rangeStepMatch, input, options);
        }

        // Check for specific day patterns first (e.g., "every monday", "every weekday")
        // These don't have explicit interval units, so they need special handling
        var specificDayMatch = SpecificDayPattern().Match(input);

        // Extract interval and unit - REFACTORED
        // Parse interval using extracted method for better maintainability
        var intervalResult = TryParseInterval(input, isOnPattern, specificDayMatch);
        if (intervalResult is ParseResult<(int, IntervalUnit)>.Error intervalError)
        {
            return new ParseResult<ScheduleSpec>.Error(intervalError.Message);
        }
        var (interval, unit) = ((ParseResult<(int, IntervalUnit)>.Success)intervalResult).Value;

        // Extract time constraints (optional) - REFACTORED
        // Parse time constraints using extracted method for better maintainability
        var timeConstraintResult = TryParseTimeConstraints(input);
        if (timeConstraintResult is ParseResult<TimeConstraints>.Error timeError)
        {
            return new ParseResult<ScheduleSpec>.Error(timeError.Message);
        }
        var timeConstraints = ((ParseResult<TimeConstraints>.Success)timeConstraintResult).Value;

        var timeOfDay = timeConstraints.TimeOfDay;

        // Extract day specifier (optional, context-aware) - REFACTORED
        // Parse day constraints using extracted method for better maintainability
        var dayConstraintResult = TryParseDayConstraints(input, unit, specificDayMatch);
        if (dayConstraintResult is ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Error dayError)
        {
            return new ParseResult<ScheduleSpec>.Error(dayError.Message);
        }
        var (dayConstraints, advancedQuartzConstraints) =
            ((ParseResult<(DayConstraints, AdvancedQuartzConstraints)>.Success)dayConstraintResult).Value;

        var dayOfWeek = dayConstraints.DayOfWeek;
        var dayPattern = dayConstraints.DayPattern;
        var dayOfMonth = dayConstraints.DayOfMonth;
        var dayOfWeekList = dayConstraints.DayOfWeekList;
        var dayOfWeekStart = dayConstraints.DayOfWeekStart;
        var dayOfWeekEnd = dayConstraints.DayOfWeekEnd;

        var isLastDay = advancedQuartzConstraints.IsLastDay;
        var isLastDayOfWeek = advancedQuartzConstraints.IsLastDayOfWeek;
        var lastDayOffset = advancedQuartzConstraints.LastDayOffset;
        var isNearestWeekday = advancedQuartzConstraints.IsNearestWeekday;
        var nthOccurrence = advancedQuartzConstraints.NthOccurrence;

        // Extract month constraints (optional) - REFACTORED
        // Parse month constraints using extracted method for better maintainability
        var monthConstraintResult = TryParseMonthConstraints(input, dayOfMonth);
        if (monthConstraintResult is ParseResult<(MonthConstraints, int?)>.Error monthError)
        {
            return new ParseResult<ScheduleSpec>.Error(monthError.Message);
        }
        var (monthConstraints, updatedDayOfMonth) =
            ((ParseResult<(MonthConstraints, int?)>.Success)monthConstraintResult).Value;

        var monthSpecifier = monthConstraints.Specifier;
        dayOfMonth = updatedDayOfMonth; // May be updated by combined month+day pattern

        // Note: We allow month intervals combined with month selection for patterns like:
        // "every month on 15 in january,april,july,october" = 15th of specific months
        // This maps to cron: "0 0 15 1,4,7,10 *"

        // Minute/hour/day lists and ranges already parsed - just assign values
        var minuteList = timeConstraints.MinuteList;
        var minuteStart = timeConstraints.MinuteStart;
        var minuteEnd = timeConstraints.MinuteEnd;
        var hourList = timeConstraints.HourList;
        var hourStart = timeConstraints.HourStart;
        var hourEnd = timeConstraints.HourEnd;
        var dayList = dayConstraints.DayList;
        var dayStart = dayConstraints.DayStart;
        var dayEnd = dayConstraints.DayEnd;

        // Extract year constraint (optional) - REFACTORED
        var yearConstraintResult = TryParseYearConstraint(input);
        // Year parsing never fails (always returns success with null or value)
        var yearConstraint = ((ParseResult<YearConstraint>.Success)yearConstraintResult).Value;
        var year = yearConstraint.Year;

        // Validate DayOfWeek and DayPattern are mutually exclusive (should not happen due to parsing logic, but defensive check)
        if (dayOfWeek.HasValue && dayPattern.HasValue)
        {
            return new ParseResult<ScheduleSpec>.Error(
                "Cannot specify both a specific day and a day pattern (internal parsing error)");
        }

        return new ParseResult<ScheduleSpec>.Success(new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfMonth = dayOfMonth,
            DayOfWeekList = dayOfWeekList,
            DayOfWeekStart = dayOfWeekStart,
            DayOfWeekEnd = dayOfWeekEnd,
            Month = monthSpecifier,
            TimeOfDay = timeOfDay,
            TimeZone = options.TimeZone,
            IsLastDay = isLastDay,
            IsLastDayOfWeek = isLastDayOfWeek,
            LastDayOffset = lastDayOffset,
            IsNearestWeekday = isNearestWeekday,
            NthOccurrence = nthOccurrence,
            MinuteList = minuteList,
            MinuteStart = minuteStart,
            MinuteEnd = minuteEnd,
            HourList = hourList,
            HourStart = hourStart,
            HourEnd = hourEnd,
            DayList = dayList,
            DayStart = dayStart,
            DayEnd = dayEnd,
            Year = year
        });
    }
}
