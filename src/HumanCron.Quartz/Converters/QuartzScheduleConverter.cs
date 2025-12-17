using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Abstractions;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz.Helpers;
using Quartz;
using System;
using NodaTime;
// ReSharper disable MemberCanBePrivate.Global

namespace HumanCron.Quartz.Converters;

/// <summary>
/// Converts between natural language and Quartz.NET schedules
/// Provides bidirectional conversion: natural language ↔ Quartz IScheduleBuilder
/// </summary>
public sealed class QuartzScheduleConverter : IQuartzScheduleConverter
{
    // Maximum input length to prevent DoS attacks via extremely long strings
    private const int MaxInputLength = 1000;

    private readonly IScheduleParser _parser;
    private readonly IScheduleFormatter _formatter;
    private readonly QuartzScheduleBuilder _quartzBuilder;
    private readonly QuartzScheduleParser _quartzParser;
    private readonly DateTimeZone _localTimeZone;

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    /// <param name="parser">Natural language parser</param>
    /// <param name="formatter">Natural language formatter</param>
    /// <param name="clock">Clock for date/time operations (SystemClock.Instance in production, FakeClock in tests)</param>
    /// <param name="localTimeZone">Server's local timezone (GetSystemDefault() in production, explicit timezone in tests)</param>
    internal QuartzScheduleConverter(IScheduleParser parser, IScheduleFormatter formatter, IClock clock, DateTimeZone localTimeZone)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
        _quartzBuilder = new QuartzScheduleBuilder(clock ?? throw new ArgumentNullException(nameof(clock)));
        _quartzParser = new QuartzScheduleParser();
    }

    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        int misfireInstruction = 0)
    {
        return ToQuartzSchedule(naturalLanguage, null, misfireInstruction);
    }

    /// <summary>
    /// Convert natural language to Quartz.NET schedule with timezone support (internal for testing)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "1d at 2pm")</param>
    /// <param name="userTimezone">
    /// User's timezone for interpreting times (null = use system default timezone)
    /// Quartz stores timezone metadata, so DST changes are handled automatically
    /// Use IANA timezone IDs (e.g., DateTimeZoneProviders.Tzdb["America/New_York"])
    /// </param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (default: 0 = SmartPolicy)
    /// </param>
    /// <returns>ParseResult containing Quartz IScheduleBuilder or error</returns>
    /// <remarks>
    /// Unlike Unix cron, Quartz schedules preserve timezone information via .InTimeZone().
    /// This means DST transitions are handled automatically at runtime.
    ///
    /// Examples:
    /// - Default: ToQuartzSchedule("1d at 2pm") → 2pm in system timezone
    /// - Explicit: ToQuartzSchedule("1d at 2pm", EST) → 2pm EST (handles DST automatically)
    /// </remarks>
    internal ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        DateTimeZone? userTimezone,
        int misfireInstruction = 0)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<IScheduleBuilder>.Error("Natural language input cannot be empty");
        }

        if (naturalLanguage.Length > MaxInputLength)
        {
            return new ParseResult<IScheduleBuilder>.Error(
                $"Natural language input exceeds maximum length of {MaxInputLength} characters");
        }

        // Use provided timezone or default to server's local timezone
        var options = new Parsing.ScheduleParserOptions
        {
            TimeZone = userTimezone ?? _localTimeZone
        };

        // Step 1: Parse natural language to ScheduleSpec
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            return new ParseResult<IScheduleBuilder>.Error(error.Message);
        }

        var spec = success.Value;

        // Step 2: Build Quartz schedule from ScheduleSpec
        try
        {
            var scheduleBuilder = _quartzBuilder.Build(spec);

            // Step 3: Apply misfire instruction to the schedule builder
            scheduleBuilder = MisfireInstructionHelper.ApplyMisfireInstruction(scheduleBuilder, misfireInstruction);

            return new ParseResult<IScheduleBuilder>.Success(scheduleBuilder);
        }
        catch (Exception ex)
        {
            return new ParseResult<IScheduleBuilder>.Error($"Failed to build Quartz schedule: {ex.Message}");
        }
    }

    public ParseResult<string> ToNaturalLanguage(IScheduleBuilder? scheduleBuilder)
    {
        if (scheduleBuilder == null)
        {
            return new ParseResult<string>.Error("Schedule builder cannot be null");
        }

        // Step 1: Parse Quartz schedule to ScheduleSpec
        var parseResult = _quartzParser.ParseScheduleBuilder(scheduleBuilder);
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            return new ParseResult<string>.Error(error.Message);
        }

        var spec = success.Value;

        // Step 2: Format ScheduleSpec to natural language
        var naturalLanguage = _formatter.Format(spec);
        return new ParseResult<string>.Success(naturalLanguage);
    }

    /// <summary>
    /// Calculate the appropriate start time for a schedule with constraints
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "2w on sunday at 2pm")</param>
    /// <param name="referenceTime">Optional reference time (defaults to UTC now)</param>
    /// <param name="userTimezone">User's timezone for interpreting times (null = use Local timezone)</param>
    /// <returns>The calculated start time, or null if no special start time is needed</returns>
    internal ParseResult<DateTimeOffset?> CalculateStartTime(
        string naturalLanguage,
        DateTimeOffset? referenceTime = null,
        DateTimeZone? userTimezone = null)
    {
        var options = new Parsing.ScheduleParserOptions
        {
            TimeZone = userTimezone ?? _localTimeZone
        };

        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            return new ParseResult<DateTimeOffset?>.Error(error.Message);
        }

        var spec = success.Value;
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);
        return new ParseResult<DateTimeOffset?>.Success(startTime);
    }

    public ParseResult<TriggerBuilder> CreateTriggerBuilder(
        string naturalLanguage,
        int misfireInstruction = 0)
    {
        // Get the schedule builder with misfire instruction applied
        // (ToQuartzSchedule validates input - null/empty/length checks)
        var scheduleResult = ToQuartzSchedule(naturalLanguage, misfireInstruction);
        if (scheduleResult is not ParseResult<IScheduleBuilder>.Success scheduleSuccess)
        {
            var error = (ParseResult<IScheduleBuilder>.Error)scheduleResult;
            return new ParseResult<TriggerBuilder>.Error(error.Message);
        }

        // Calculate start time (null if not needed)
        var startTimeResult = CalculateStartTime(naturalLanguage);
        if (startTimeResult is not ParseResult<DateTimeOffset?>.Success startSuccess)
        {
            var error = (ParseResult<DateTimeOffset?>.Error)startTimeResult;
            return new ParseResult<TriggerBuilder>.Error(error.Message);
        }

        // Create TriggerBuilder with schedule and optional start time
        var triggerBuilder = TriggerBuilder.Create()
            .WithSchedule(scheduleSuccess.Value);

        // Set start time if calculated (for CalendarInterval schedules with constraints)
        if (!startSuccess.Value.HasValue) return new ParseResult<TriggerBuilder>.Success(triggerBuilder);
        // Explicitly convert to UTC to ensure Quartz interprets it correctly
        var startTimeUtc = startSuccess.Value.Value.ToUniversalTime();
        triggerBuilder.StartAt(startTimeUtc);

        return new ParseResult<TriggerBuilder>.Success(triggerBuilder);
    }
}
