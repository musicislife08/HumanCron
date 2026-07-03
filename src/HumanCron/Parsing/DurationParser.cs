using HumanCron.Models;
using NodaTime;
using System;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language duration strings (e.g. "1d 2h 30m") into a NodaTime Period.
/// INTERNAL: unit-agnostic — callers decide which unit combinations are valid for their layer.
/// </summary>
internal static partial class DurationParser
{
    // Longest-alternative-first so "ms" is tried before "m", and every full word is tried
    // before its single-letter abbreviation. Full words use an inline case-insensitive
    // scope (?i:...) so "Hours"/"HOURS" work without making the single-letter tokens
    // case-insensitive too — "m" (minutes) and "M" (months) must stay distinct.
    // Whitespace is OPTIONAL between a value and its own unit (so "30m" and "30 minutes"
    // both work) but MANDATORY ("\s+") between one complete token and the next - that's
    // what rejects a squashed compound like "1d2h30m" while accepting "1d 2h 30m". A named
    // group repeated across the pattern (once outside the trailing "*" group, once inside
    // it) populates one Capture per occurrence, in match order, in .NET's regex engine -
    // so a single anchored match yields every (value, unit) pair via Group.Captures.
    [GeneratedRegex(
        @"^\s*(?<value>\d+)\s*(?<unit>ms|(?i:milliseconds?)|(?i:seconds?)|s|(?i:minutes?)|m|(?i:hours?)|h|(?i:days?)|d|(?i:weeks?)|w|(?i:months?)|M|(?i:years?)|y)(?:\s+(?<value>\d+)\s*(?<unit>ms|(?i:milliseconds?)|(?i:seconds?)|s|(?i:minutes?)|m|(?i:hours?)|h|(?i:days?)|d|(?i:weeks?)|w|(?i:months?)|M|(?i:years?)|y))*\s*$")]
    private static partial Regex DurationBodyPattern();

    internal static ParseResult<Period> Parse(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return new ParseResult<Period>.Success(Period.Zero);
        }

        var trimmed = duration.Trim();
        var (isNegative, body) = ExtractSign(trimmed);

        if (body.Length == 0)
        {
            return new ParseResult<Period>.Error(
                $"Unable to parse duration from: '{duration}'. Expected a magnitude after the sign, e.g. '-2 hours'.");
        }

        var match = DurationBodyPattern().Match(body);
        if (!match.Success)
        {
            return new ParseResult<Period>.Error(
                $"Unable to parse duration from: '{duration}'. Expected a compound duration like " +
                "'1 day 2 hours 30 minutes' or '1d 2h 30m'.");
        }

        var builder = new PeriodBuilder();
        var values = match.Groups["value"].Captures;
        var units = match.Groups["unit"].Captures;
        for (var i = 0; i < values.Count; i++)
        {
            if (!long.TryParse(values[i].Value, out var value))
            {
                return new ParseResult<Period>.Error($"Invalid duration value: {values[i].Value}");
            }

            var error = AddToBuilder(builder, units[i].Value, value);
            if (error is not null)
            {
                return new ParseResult<Period>.Error(error);
            }
        }

        var period = builder.Build();
        return new ParseResult<Period>.Success(isNegative ? -period : period);
    }

    private static (bool isNegative, string body) ExtractSign(string trimmed)
    {
        if (trimmed.StartsWith('-'))
        {
            return (true, trimmed[1..].TrimStart());
        }

        if (trimmed.Length > 5 && trimmed.StartsWith("minus", StringComparison.OrdinalIgnoreCase)
            && char.IsWhiteSpace(trimmed[5]))
        {
            return (true, trimmed[5..].TrimStart());
        }

        if (trimmed.Length > 8 && trimmed.StartsWith("negative", StringComparison.OrdinalIgnoreCase)
            && char.IsWhiteSpace(trimmed[8]))
        {
            return (true, trimmed[8..].TrimStart());
        }

        return (false, trimmed);
    }

    private static string? AddToBuilder(PeriodBuilder builder, string rawUnit, long value)
    {
        var unit = CanonicalUnit(rawUnit);
        try
        {
            checked
            {
                switch (unit)
                {
                    case "ms":
                        builder.Milliseconds += value;
                        return null;
                    case "s":
                        builder.Seconds += value;
                        return null;
                    case "m":
                        builder.Minutes += value;
                        return null;
                    case "h":
                        builder.Hours += value;
                        return null;
                    case "d":
                    case "w":
                    case "M":
                    case "y":
                        if (value > int.MaxValue)
                        {
                            return $"Duration value too large: {value}. Maximum is {int.MaxValue}.";
                        }

                        var intValue = (int)value;
                        switch (unit)
                        {
                            case "d": builder.Days += intValue; break;
                            case "w": builder.Weeks += intValue; break;
                            case "M": builder.Months += intValue; break;
                            case "y": builder.Years += intValue; break;
                        }

                        return null;
                    default:
                        throw new InvalidOperationException($"Unknown duration unit: {rawUnit}");
                }
            }
        }
        catch (OverflowException)
        {
            return $"Duration value too large: the accumulated total for repeated '{rawUnit}' units overflows.";
        }
    }

    private static string CanonicalUnit(string rawUnit)
    {
        if (rawUnit is "ms" or "s" or "m" or "h" or "d" or "w" or "M" or "y")
        {
            return rawUnit;
        }

        var lower = rawUnit.ToLowerInvariant();
        if (lower.StartsWith("millisecond", StringComparison.Ordinal)) return "ms";
        if (lower.StartsWith("second", StringComparison.Ordinal)) return "s";
        if (lower.StartsWith("minute", StringComparison.Ordinal)) return "m";
        if (lower.StartsWith("hour", StringComparison.Ordinal)) return "h";
        if (lower.StartsWith("week", StringComparison.Ordinal)) return "w";
        if (lower.StartsWith("month", StringComparison.Ordinal)) return "M";
        if (lower.StartsWith("year", StringComparison.Ordinal)) return "y";
        if (lower.StartsWith("day", StringComparison.Ordinal)) return "d";
        throw new InvalidOperationException($"Unknown duration unit: {rawUnit}");
    }
}
