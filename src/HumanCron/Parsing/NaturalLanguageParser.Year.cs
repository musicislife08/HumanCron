using HumanCron.Models;
using HumanCron.Models.Internal;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language schedule descriptions into ScheduleSpec
/// INTERNAL: Used internally by converters
/// PARTIAL: Year constraint parsing logic (patterns, methods)
/// </summary>
internal sealed partial class NaturalLanguageParser
{
    // ===== YEAR-SPECIFIC PATTERNS =====

    /// <summary>
    /// Year constraint patterns: "in year 2025"
    /// </summary>
    [GeneratedRegex(@"in\s+year\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex YearPattern();

    // ===== YEAR PARSING METHODS =====

    /// <summary>
    /// Parse year constraint from natural language input
    /// Handles optional year specification: "in year 2025"
    /// </summary>
    private ParseResult<YearConstraint> TryParseYearConstraint(string input)
    {
        int? year = null;

        var yearMatch = YearPattern().Match(input);
        if (yearMatch.Success)
        {
            year = int.Parse(yearMatch.Groups[1].Value);
        }

        return new ParseResult<YearConstraint>.Success(new YearConstraint { Year = year });
    }
}
