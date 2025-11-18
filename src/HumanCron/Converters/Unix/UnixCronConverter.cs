using HumanCron.Models.Internal;
using System;
using HumanCron.Abstractions;
using HumanCron.Models;
using HumanCron.Parsing;
using HumanCron.Formatting;
using NodaTime;

namespace HumanCron.Converters.Unix;

/// <summary>
/// Bidirectional converter between natural language and Unix 5-part cron expressions
/// </summary>
/// <remarks>
/// Implements the core IHumanCronConverter interface using:
/// - NaturalLanguageParser: text → ScheduleSpec
/// - UnixCronBuilder: ScheduleSpec → Unix cron
/// - UnixCronParser: Unix cron → ScheduleSpec
/// - NaturalLanguageFormatter: ScheduleSpec → text
/// </remarks>
public sealed class UnixCronConverter : IHumanCronConverter
{
    private readonly IScheduleParser _parser;
    private readonly IScheduleFormatter _formatter;
    private readonly UnixCronBuilder _cronBuilder;
    private readonly UnixCronParser _cronParser;
    private readonly DateTimeZone _localTimeZone;

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    /// <param name="parser">Natural language parser (text → ScheduleSpec)</param>
    /// <param name="formatter">Natural language formatter (ScheduleSpec → text)</param>
    /// <param name="clock">Clock for date/time operations (SystemClock.Instance in production, FakeClock in tests)</param>
    /// <param name="localTimeZone">Server's local timezone for cron execution (GetSystemDefault() in production, explicit timezone in tests)</param>
    internal UnixCronConverter(IScheduleParser parser, IScheduleFormatter formatter, IClock clock, DateTimeZone localTimeZone)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
        _cronBuilder = new UnixCronBuilder(clock, localTimeZone);
        _cronParser = new UnixCronParser();
    }

    /// <summary>
    /// Create a new Unix cron converter (production use)
    /// </summary>
    public static UnixCronConverter Create()
    {
        var localTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault()
            ?? throw new InvalidOperationException(
                "Could not determine system timezone. NodaTime TZDB data may be corrupted.");

        return new UnixCronConverter(
            new NaturalLanguageParser(),
            new NaturalLanguageFormatter(),
            SystemClock.Instance,
            localTimeZone
        );
    }

    /// <inheritdoc/>
    public ParseResult<string> ToCron(string naturalLanguage)
    {
        return ToCron(naturalLanguage, null);
    }

    /// <summary>
    /// Convert natural language to Unix cron expression with timezone support
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "1d at 2pm")</param>
    /// <param name="userTimezone">
    /// User's timezone for interpreting times (null = use system timezone)
    /// Times are converted from user timezone to server timezone for Unix cron output
    /// Use IANA timezone IDs (e.g., DateTimeZoneProviders.Tzdb["America/New_York"])
    /// </param>
    /// <returns>ParseResult containing Unix cron expression or error</returns>
    /// <remarks>
    /// IMPORTANT: Unix cron does not store timezone metadata. Times are converted at creation time only.
    ///
    /// DST Handling:
    /// - If userTimezone == server timezone: OS handles DST automatically ✅
    /// - If userTimezone != server timezone: Uses current DST offset, will be incorrect after DST change ⚠️
    /// - For DST-aware scheduling, use QuartzScheduleConverter instead
    ///
    /// Examples:
    /// - Same timezone: ToCron("1d at 2pm") → "0 14 * * *" (2pm local time)
    /// - Cross-timezone: ToCron("1d at 2pm", EST) on MST server → "0 12 * * *" (2pm EST = noon MST)
    /// </remarks>
    public ParseResult<string> ToCron(string naturalLanguage, DateTimeZone? userTimezone)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<string>.Error("Natural language input cannot be empty");
        }

        // Use provided timezone or default to server's local timezone
        var options = new ScheduleParserOptions
        {
            TimeZone = userTimezone ?? _localTimeZone
        };

        // Step 1: Parse natural language → ScheduleSpec
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is ParseResult<ScheduleSpec>.Error parseError)
        {
            return new ParseResult<string>.Error($"Failed to parse natural language: {parseError.Message}");
        }

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Step 2: Build Unix cron from ScheduleSpec
        var buildResult = _cronBuilder.Build(spec);
        if (buildResult is ParseResult<string>.Error buildError)
        {
            return new ParseResult<string>.Error($"Failed to convert to cron: {buildError.Message}");
        }

        return buildResult;
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNaturalLanguage(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return new ParseResult<string>.Error("Cron expression cannot be empty");
        }

        // Step 1: Parse Unix cron → ScheduleSpec
        var parseResult = _cronParser.Parse(cronExpression);
        if (parseResult is ParseResult<ScheduleSpec>.Error parseError)
        {
            return new ParseResult<string>.Error($"Failed to parse cron expression: {parseError.Message}");
        }

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        try
        {
            // Step 2: Format ScheduleSpec → natural language
            var naturalLanguage = _formatter.Format(spec);
            return new ParseResult<string>.Success(naturalLanguage);
        }
        catch (Exception ex)
        {
            return new ParseResult<string>.Error($"Failed to format as natural language: {ex.Message}");
        }
    }
}
