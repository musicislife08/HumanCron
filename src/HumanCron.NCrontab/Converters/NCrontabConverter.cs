using System;
using HumanCron.Abstractions;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.NCrontab.Abstractions;
using HumanCron.Parsing;
using NodaTime;

namespace HumanCron.NCrontab.Converters;

/// <summary>
/// Bidirectional converter between natural language and NCrontab 6-field cron expressions
/// </summary>
/// <remarks>
/// Implements the INCrontabConverter interface using:
/// - NaturalLanguageParser: text → ScheduleSpec
/// - NCrontabBuilder: ScheduleSpec → NCrontab cron
/// - NCrontabParser: NCrontab cron → ScheduleSpec
/// - NaturalLanguageFormatter: ScheduleSpec → text
/// NCrontab format: second minute hour day month dayOfWeek
/// </remarks>
public sealed class NCrontabConverter : INCrontabConverter
{
    // Maximum input length to prevent DoS attacks via extremely long strings
    private const int MaxInputLength = 1000;

    private readonly IScheduleParser _parser;
    private readonly IScheduleFormatter _formatter;
    private readonly NCrontabBuilder _cronBuilder;
    private readonly NCrontabParser _cronParser;
    private readonly DateTimeZone _localTimeZone;

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    internal NCrontabConverter(IScheduleParser parser, IScheduleFormatter formatter, IClock clock, DateTimeZone localTimeZone)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
        _cronBuilder = new NCrontabBuilder(clock, localTimeZone);
        _cronParser = new NCrontabParser();
    }

    /// <summary>
    /// Create a new NCrontab converter (production use)
    /// </summary>
    public static NCrontabConverter Create()
    {
        var localTimeZone = DateTimeZoneProviders.Tzdb.GetSystemDefault()
            ?? throw new InvalidOperationException(
                "Could not determine system timezone. NodaTime TZDB data may be corrupted.");

        return new NCrontabConverter(
            new NaturalLanguageParser(),
            new NaturalLanguageFormatter(),
            SystemClock.Instance,
            localTimeZone
        );
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNCrontab(string naturalLanguage)
    {
        return ToNCrontab(naturalLanguage, null);
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNCrontab(string naturalLanguage, DateTimeZone? userTimezone)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<string>.Error("Natural language input cannot be empty");
        }

        if (naturalLanguage.Length > MaxInputLength)
        {
            return new ParseResult<string>.Error(
                $"Natural language input exceeds maximum length of {MaxInputLength} characters");
        }

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

        // Step 2: Build NCrontab cron from ScheduleSpec
        var buildResult = _cronBuilder.Build(spec);
        if (buildResult is ParseResult<string>.Error buildError)
        {
            return new ParseResult<string>.Error($"Failed to convert to NCrontab: {buildError.Message}");
        }

        return buildResult;
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNaturalLanguage(string ncrontabExpression)
    {
        if (string.IsNullOrWhiteSpace(ncrontabExpression))
        {
            return new ParseResult<string>.Error("NCrontab expression cannot be empty");
        }

        if (ncrontabExpression.Length > MaxInputLength)
        {
            return new ParseResult<string>.Error(
                $"NCrontab expression exceeds maximum length of {MaxInputLength} characters");
        }

        // Step 1: Parse NCrontab cron → ScheduleSpec
        var parseResult = _cronParser.Parse(ncrontabExpression);
        if (parseResult is ParseResult<ScheduleSpec>.Error parseError)
        {
            return new ParseResult<string>.Error($"Failed to parse NCrontab expression: {parseError.Message}");
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
