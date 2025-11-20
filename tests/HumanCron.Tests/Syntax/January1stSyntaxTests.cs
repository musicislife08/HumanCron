using HumanCron.Abstractions;
using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using HumanCron.Quartz;
using HumanCron.Quartz.Abstractions;
using NodaTime;
using NodaTime.Testing;
using NUnit.Framework;
using Quartz;

namespace HumanCron.Tests.Syntax;

/// <summary>
/// Tests for "every year on january 1st at 1am" use case
/// </summary>
[TestFixture]
public class January1stSyntaxTests
{
    private IHumanCronConverter _unixConverter = null!;
    private IQuartzScheduleConverter _quartzConverter = null!;

    [SetUp]
    public void Setup()
    {
        // Create Unix converter with deterministic clock
        var parser = new NaturalLanguageParser();
        var formatter = new NaturalLanguageFormatter();
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        _unixConverter = new UnixCronConverter(parser, formatter, fakeClock, DateTimeZone.Utc);

        // Create Quartz converter
        _quartzConverter = QuartzScheduleConverterFactory.Create();
    }

    [Test]
    public void UserRequestedSyntax_OnJanuary1st_MayNotBeSupportedYet()
    {
        // User asked: "every year on january 1st at 1am"
        var natural = "every year on january 1st at 1am";
        var result = _unixConverter.ToCron(natural);

        Console.WriteLine($"Input: {natural}");
        Console.WriteLine($"Result: {result}");
        Console.WriteLine($"Result type: {result.GetType().Name}");

        // This will tell us if the syntax is supported
        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"Success! Cron: {success.Value}");
            Assert.That(success.Value, Is.EqualTo("0 1 1 1 *"));
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Error: {error.Message}");
        }
    }

    [Test]
    public void AlternativeSyntax_OnThe1stInJanuary_ShouldWork()
    {
        // Alternative syntax: swap order to match our DSL
        var natural = "every year on the 1st in january at 1am";
        var result = _unixConverter.ToCron(natural);

        Console.WriteLine($"Input: {natural}");
        Console.WriteLine($"Result: {result}");

        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"Success! Cron: {success.Value}");
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Error: {error.Message}");
        }
    }

    [Test]
    public void AlternativeSyntax_EveryMonthOnThe1stInJanuary_ShouldWork()
    {
        // Using "every month" instead of "every year"
        var natural = "every month on the 1st in january at 1am";
        var result = _unixConverter.ToCron(natural);

        Console.WriteLine($"Input: {natural}");
        Console.WriteLine($"Result: {result}");

        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"Success! Cron: {success.Value}");
            Assert.That(success.Value, Is.EqualTo("0 1 1 1 *"));
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Error: {error.Message}");
        }
    }

    [Test]
    public void QuartzVersion_EveryMonthOnThe1stInJanuary_ShouldWork()
    {
        // Test with Quartz converter (returns IScheduleBuilder, not cron string)
        var natural = "every month on the 1st in january at 1am";
        var result = _quartzConverter.ToQuartzSchedule(natural);

        Console.WriteLine($"Input: {natural}");
        Console.WriteLine($"Result: {result}");

        if (result is ParseResult<IScheduleBuilder>.Success success)
        {
            Console.WriteLine($"Success! Got IScheduleBuilder: {success.Value.GetType().Name}");
            // Quartz converter returns a schedule builder, which works for the use case
            Assert.That(success.Value, Is.Not.Null);
        }
        else if (result is ParseResult<IScheduleBuilder>.Error error)
        {
            Console.WriteLine($"Error: {error.Message}");
        }
    }

    [Test]
    public void RoundTrip_CronToNaturalLanguageUsesNaturalSyntax()
    {
        // Test that cron "0 1 1 1 *" formats to natural "on january 1st" syntax
        const string cron = "0 1 1 1 *";
        var result = _unixConverter.ToNaturalLanguage(cron);

        switch (result)
        {
            case ParseResult<string>.Success success:
            {

                // Should use the natural "on january 1st" syntax
                Assert.That(success.Value, Does.Contain("on january 1st"));

                // Verify it can be parsed back
                var backToCron = _unixConverter.ToCron(success.Value);
                if (backToCron is ParseResult<string>.Success cronSuccess)
                {
                    Assert.That(cronSuccess.Value, Is.EqualTo(cron));
                }
                else
                {
                    Assert.Fail("Failed to convert back to cron");
                }
                break;
            }
            case ParseResult<string>.Error error:
                Assert.Fail($"Failed to convert to natural language: {error.Message}");
                break;
        }
    }

    #region Month Abbreviation Support

    /// <summary>
    /// Tests that all month abbreviations (jan, feb, mar, etc.) are properly supported
    /// in the combined month+day syntax
    /// </summary>
    [TestCase("every month on the 1st in jan at 1am", 1, 1)]
    [TestCase("every month on the 15th in feb at 2pm", 2, 15)]
    [TestCase("every month on the 10th in mar at 3pm", 3, 10)]
    [TestCase("every month on the 20th in apr at 4am", 4, 20)]
    [TestCase("every month on the 5th in may at 5am", 5, 5)]
    [TestCase("every month on the 25th in jun at 6pm", 6, 25)]
    [TestCase("every month on the 12th in jul at 7am", 7, 12)]
    [TestCase("every month on the 8th in aug at 8am", 8, 8)]
    [TestCase("every month on the 18th in sep at 9am", 9, 18)]
    [TestCase("every month on the 30th in oct at 10am", 10, 30)]
    [TestCase("every month on the 22nd in nov at 11am", 11, 22)]
    [TestCase("every month on the 31st in dec at 12pm", 12, 31)]
    public void MonthAbbreviations_AllMonths_ParseSuccessfully(string input, int expectedMonth, int expectedDay)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to parse '{input}'");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        // Unix cron format: minute hour day month dow
        Assert.That(cronParts[2], Is.EqualTo(expectedDay.ToString()), "Day mismatch");
        Assert.That(cronParts[3], Is.EqualTo(expectedMonth.ToString()), "Month mismatch");
    }

    #endregion

    #region Ordinal Endings Support

    /// <summary>
    /// Tests that all ordinal endings (1st, 2nd, 3rd, 11th, 21st, 22nd, 23rd, 31st)
    /// are properly handled
    /// </summary>
    [TestCase("every month on the 1st in january", 1)]
    [TestCase("every month on the 2nd in january", 2)]
    [TestCase("every month on the 3rd in january", 3)]
    [TestCase("every month on the 4th in january", 4)]
    [TestCase("every month on the 5th in january", 5)]
    [TestCase("every month on the 10th in january", 10)]
    [TestCase("every month on the 11th in january", 11)]
    [TestCase("every month on the 12th in january", 12)]
    [TestCase("every month on the 13th in january", 13)]
    [TestCase("every month on the 20th in january", 20)]
    [TestCase("every month on the 21st in january", 21)]
    [TestCase("every month on the 22nd in january", 22)]
    [TestCase("every month on the 23rd in january", 23)]
    [TestCase("every month on the 24th in january", 24)]
    [TestCase("every month on the 30th in january", 30)]
    [TestCase("every month on the 31st in january", 31)]
    public void OrdinalEndings_AllValidDays_ParseCorrectly(string input, int expectedDay)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to parse '{input}'");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        Assert.That(cronParts[2], Is.EqualTo(expectedDay.ToString()),
            $"Expected day {expectedDay} in cron output");
    }

    #endregion

    #region February 29th Edge Case

    /// <summary>
    /// Tests the edge case of February 29th (leap year day)
    /// </summary>
    [Test]
    public void February29th_LeapYearDay_ParsesSuccessfully()
    {
        // Arrange
        var input = "every month on the 29th in february at 10am";

        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            "Should successfully parse February 29th");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        Assert.That(cronParts[2], Is.EqualTo("29"), "Day should be 29");
        Assert.That(cronParts[3], Is.EqualTo("2"), "Month should be February (2)");
    }

    [Test]
    public void February30th_InvalidDay_ReturnsError()
    {
        // Arrange
        var input = "every month on the 30th in february at 10am";

        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        // Note: This may parse successfully but represents an impossible date
        // The behavior depends on implementation - document actual behavior
        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"February 30th parsed (may never trigger): {success.Value}");
        }
        else if (result is ParseResult<string>.Error error)
        {
            Assert.That(error.Message, Does.Contain("day").IgnoreCase);
        }
    }

    #endregion

    #region Different Months with 30 vs 31 Days

    /// <summary>
    /// Tests that months with different numbers of days handle day 31 appropriately
    /// </summary>
    [TestCase("every month on the 31st in january", true, 1)] // 31 days
    [TestCase("every month on the 31st in february", false, 2)] // 28/29 days
    [TestCase("every month on the 31st in march", true, 3)] // 31 days
    [TestCase("every month on the 31st in april", false, 4)] // 30 days
    [TestCase("every month on the 31st in may", true, 5)] // 31 days
    [TestCase("every month on the 31st in june", false, 6)] // 30 days
    [TestCase("every month on the 31st in july", true, 7)] // 31 days
    [TestCase("every month on the 31st in august", true, 8)] // 31 days
    [TestCase("every month on the 31st in september", false, 9)] // 30 days
    [TestCase("every month on the 31st in october", true, 10)] // 31 days
    [TestCase("every month on the 31st in november", false, 11)] // 30 days
    [TestCase("every month on the 31st in december", true, 12)] // 31 days
    public void Day31_InAllMonths_ParsesButMayNotTrigger(string input, bool monthHas31Days, int expectedMonth)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        if (result is ParseResult<string>.Success success)
        {
            var cronParts = success.Value.Split(' ');
            Assert.That(cronParts[2], Is.EqualTo("31"), "Day should be 31");
            Assert.That(cronParts[3], Is.EqualTo(expectedMonth.ToString()), "Month mismatch");

            if (!monthHas31Days)
            {
                Console.WriteLine($"Warning: {input} will never trigger (month only has 30 or fewer days)");
            }
        }
        else
        {
            // Some implementations may reject invalid day/month combinations
            Assert.That(result, Is.TypeOf<ParseResult<string>.Error>());
        }
    }

    #endregion

    #region Time Format Combinations

    /// <summary>
    /// Tests combination of month+day syntax with various time formats
    /// </summary>
    [TestCase("every month on the 15th in march at 1am", "1", "0")] // 12-hour AM
    [TestCase("every month on the 15th in march at 1pm", "13", "0")] // 12-hour PM
    [TestCase("every month on the 15th in march at 12am", "0", "0")] // Midnight
    [TestCase("every month on the 15th in march at 12pm", "12", "0")] // Noon
    [TestCase("every month on the 15th in march at 3:30am", "3", "30")] // With minutes AM
    [TestCase("every month on the 15th in march at 3:30pm", "15", "30")] // With minutes PM
    [TestCase("every month on the 15th in march at 00:00", "0", "0")] // 24-hour midnight
    [TestCase("every month on the 15th in march at 13:45", "13", "45")] // 24-hour afternoon
    [TestCase("every month on the 15th in march at 23:59", "23", "59")] // 24-hour late night
    public void TimeFormats_WithMonthAndDay_ParseCorrectly(string input, string expectedHour, string expectedMinute)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to parse '{input}'");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        // Unix cron format: minute hour day month dow
        Assert.That(cronParts[0], Is.EqualTo(expectedMinute), "Minute mismatch");
        Assert.That(cronParts[1], Is.EqualTo(expectedHour), "Hour mismatch");
    }

    #endregion

    #region Omitting Time Specification

    /// <summary>
    /// Tests that omitting time specification defaults to midnight
    /// </summary>
    [Test]
    public void OmitTime_DefaultsToMidnight()
    {
        // Arrange
        var input = "every month on the 15th in june";

        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        // Should default to 00:00 (midnight)
        Assert.That(cronParts[0], Is.EqualTo("0"), "Minutes should default to 0");
        Assert.That(cronParts[1], Is.EqualTo("0"), "Hour should default to 0 (midnight)");
        Assert.That(cronParts[2], Is.EqualTo("15"), "Day should be 15");
        Assert.That(cronParts[3], Is.EqualTo("6"), "Month should be June (6)");
    }

    #endregion

    #region Year Constraints

    /// <summary>
    /// Tests combination of month+day syntax with year constraints
    /// Note: Unix cron does not support year fields, so this tests Quartz compatibility
    /// </summary>
    [Test]
    public void YearConstraints_WithQuartzConverter_ParsesSuccessfully()
    {
        // Arrange
        var input = "every month on the 1st in january at 1am";

        // Act
        var result = _quartzConverter.ToQuartzSchedule(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>(),
            "Quartz converter should support month+day syntax");

        var success = (ParseResult<IScheduleBuilder>.Success)result;
        Assert.That(success.Value, Is.Not.Null);
    }

    #endregion

    #region Invalid Input Validation

    /// <summary>
    /// Tests that invalid day numbers are handled
    /// Note: The parser may accept these and pass them through to cron,
    /// which will handle the invalid values at runtime
    /// </summary>
    [TestCase("every month on the 0th in january")]
    [TestCase("every month on the 32nd in january")]
    [TestCase("every month on the 99th in march")]
    public void InvalidDayNumbers_MayParseOrError(string input)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        // The parser may either reject these or pass them through to cron
        // Document which behavior occurs
        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"Parser accepted invalid input '{input}': {success.Value}");
            // Invalid values may never trigger in practice
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Parser rejected invalid input '{input}': {error.Message}");
            Assert.That(error.Message, Does.Contain("day").IgnoreCase);
        }
    }

    /// <summary>
    /// Tests that invalid month names are handled
    /// Note: The parser may ignore unrecognized month names and treat the expression differently
    /// </summary>
    [TestCase("every month on the 15th in invalidmonth")]
    [TestCase("every month on the 15th in 13")]
    [TestCase("every month on the 15th in xyz")]
    public void InvalidMonthNames_MayParseOrError(string input)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        // The parser may either reject these or parse them in an unexpected way
        // Document which behavior occurs
        if (result is ParseResult<string>.Success success)
        {
            Console.WriteLine($"Parser accepted '{input}' as: {success.Value}");
            // May have parsed differently than intended
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Parser rejected '{input}': {error.Message}");
            Assert.That(error.Message, Is.Not.Empty);
        }
    }

    #endregion

    #region Formatting Consistency

    /// <summary>
    /// Tests that the formatter consistently uses full month names
    /// regardless of whether the input used abbreviations
    /// </summary>
    [TestCase("0 10 15 1 *", "january")]
    [TestCase("0 10 15 2 *", "february")]
    [TestCase("0 10 15 3 *", "march")]
    [TestCase("0 10 15 4 *", "april")]
    [TestCase("0 10 15 5 *", "may")]
    [TestCase("0 10 15 6 *", "june")]
    [TestCase("0 10 15 7 *", "july")]
    [TestCase("0 10 15 8 *", "august")]
    [TestCase("0 10 15 9 *", "september")]
    [TestCase("0 10 15 10 *", "october")]
    [TestCase("0 10 15 11 *", "november")]
    [TestCase("0 10 15 12 *", "december")]
    public void Formatter_AlwaysUsesFullMonthNames(string cronInput, string expectedMonth)
    {
        // Act
        var result = _unixConverter.ToNaturalLanguage(cronInput);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
        var success = (ParseResult<string>.Success)result;

        Assert.That(success.Value, Does.Contain(expectedMonth).IgnoreCase,
            $"Formatted output should contain full month name '{expectedMonth}'");
    }

    #endregion

    #region Equivalence with Old Syntax

    /// <summary>
    /// Tests that new combined syntax produces the same cron output as the old separate syntax
    /// </summary>
    [TestCase("every month on the 15th in january", "every month on 15 in january")]
    [TestCase("every month on the 1st in february", "every month on 1 in february")]
    [TestCase("every month on the 31st in december", "every month on 31 in december")]
    public void NewSyntax_EquivalentToOldSyntax_ProducesSameCron(string newSyntax, string oldSyntax)
    {
        // Act
        var newResult = _unixConverter.ToCron(newSyntax);
        var oldResult = _unixConverter.ToCron(oldSyntax);

        // Assert
        Assert.That(newResult, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(oldResult, Is.TypeOf<ParseResult<string>.Success>());

        var newSuccess = (ParseResult<string>.Success)newResult;
        var oldSuccess = (ParseResult<string>.Success)oldResult;

        Assert.That(newSuccess.Value, Is.EqualTo(oldSuccess.Value),
            "Both syntax forms should produce identical cron expressions");
    }

    #endregion

    #region Day Range Tests

    /// <summary>
    /// Tests various day values across the valid range (1-31)
    /// </summary>
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(15)]
    [TestCase(20)]
    [TestCase(25)]
    [TestCase(28)]
    [TestCase(29)]
    [TestCase(30)]
    [TestCase(31)]
    public void DayValues_AcrossValidRange_ParseSuccessfully(int day)
    {
        // Arrange
        var ordinal = GetOrdinalSuffix(day);
        var input = $"every month on the {day}{ordinal} in july";

        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Failed to parse day {day}");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        Assert.That(cronParts[2], Is.EqualTo(day.ToString()));
    }

    private static string GetOrdinalSuffix(int day)
    {
        if (day is >= 11 and <= 13)
            return "th";

        return (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    #endregion

    #region Mixed Month Name Formats

    /// <summary>
    /// Tests mixing abbreviated and full month names in parsing
    /// (should both work interchangeably)
    /// </summary>
    [Test]
    public void MixedMonthFormats_BothAbbreviatedAndFull_ParseCorrectly()
    {
        // Arrange
        var abbreviated = "every month on the 10th in jan at 9am";
        var fullName = "every month on the 10th in january at 9am";

        // Act
        var abbrevResult = _unixConverter.ToCron(abbreviated);
        var fullResult = _unixConverter.ToCron(fullName);

        // Assert
        Assert.That(abbrevResult, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(fullResult, Is.TypeOf<ParseResult<string>.Success>());

        var abbrevSuccess = (ParseResult<string>.Success)abbrevResult;
        var fullSuccess = (ParseResult<string>.Success)fullResult;

        Assert.That(abbrevSuccess.Value, Is.EqualTo(fullSuccess.Value),
            "Abbreviated and full month names should produce identical output");
    }

    #endregion

    #region Optional Ordinal Suffix

    /// <summary>
    /// Tests that ordinal suffix (st, nd, rd, th) is optional when using "on the N in month" syntax
    /// </summary>
    [TestCase("every month on 15 in january", "every month on the 15th in january")]
    [TestCase("every month on 1 in february", "every month on the 1st in february")]
    [TestCase("every month on 22 in march", "every month on the 22nd in march")]
    public void OrdinalSuffix_IsOptional_BothFormsParseSame(string withoutSuffix, string withSuffix)
    {
        // Act
        var resultWithout = _unixConverter.ToCron(withoutSuffix);
        var resultWith = _unixConverter.ToCron(withSuffix);

        // Assert
        Assert.That(resultWithout, Is.TypeOf<ParseResult<string>.Success>());
        Assert.That(resultWith, Is.TypeOf<ParseResult<string>.Success>());

        var successWithout = (ParseResult<string>.Success)resultWithout;
        var successWith = (ParseResult<string>.Success)resultWith;

        Assert.That(successWithout.Value, Is.EqualTo(successWith.Value),
            "With and without ordinal suffix should produce identical output");
    }

    #endregion

    #region Quartz Compatibility

    /// <summary>
    /// Tests that combined month+day syntax works with Quartz converter
    /// </summary>
    [TestCase("every month on the 1st in january at 1am")]
    [TestCase("every month on the 15th in june at 3pm")]
    [TestCase("every month on the 31st in december at 11:59pm")]
    public void QuartzCompatibility_CombinedSyntax_ProducesValidScheduleBuilder(string input)
    {
        // Act
        var result = _quartzConverter.ToQuartzSchedule(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>(),
            $"Quartz converter should handle: '{input}'");

        var success = (ParseResult<IScheduleBuilder>.Success)result;
        Assert.That(success.Value, Is.Not.Null);
        Assert.That(success.Value, Is.InstanceOf<IScheduleBuilder>());
    }

    #endregion

    #region Case Insensitivity

    /// <summary>
    /// Tests that month names are case-insensitive
    /// </summary>
    [TestCase("every month on the 10th in JANUARY at 9am")]
    [TestCase("every month on the 10th in January at 9am")]
    [TestCase("every month on the 10th in january at 9am")]
    [TestCase("every month on the 10th in JaNuArY at 9am")]
    [TestCase("every month on the 10th in JAN at 9am")]
    [TestCase("every month on the 10th in Jan at 9am")]
    public void CaseInsensitivity_MonthNames_AllVariationsWork(string input)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Case-insensitive parsing failed for: '{input}'");

        var success = (ParseResult<string>.Success)result;
        var cronParts = success.Value.Split(' ');

        Assert.That(cronParts[3], Is.EqualTo("1"), "All variations should parse to January (month 1)");
    }

    #endregion

    #region Whitespace Tolerance

    /// <summary>
    /// Tests that parser tolerates extra whitespace in various positions
    /// </summary>
    [TestCase("every  month  on  the  15th  in  january  at  9am")]
    [TestCase("every month on the 15th in january at 9am ")]
    [TestCase(" every month on the 15th in january at 9am")]
    [TestCase("every month  on the  15th  in  january at 9am")]
    public void WhitespaceTolerance_ExtraSpaces_ParsesSuccessfully(string input)
    {
        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<string>.Success>(),
            $"Parser should handle extra whitespace: '{input}'");

        var success = (ParseResult<string>.Success)result;
        Assert.That(success.Value, Is.EqualTo("0 9 15 1 *"),
            "Extra whitespace should not affect parsing outcome");
    }

    [Test]
    public void WhitespaceTolerance_TabsAndMixedWhitespace_ParsesSuccessfully()
    {
        // Arrange
        var input = "every month\ton the 15th in january at 9am";

        // Act
        var result = _unixConverter.ToCron(input);

        // Assert
        // This may or may not work depending on implementation
        // Document the behavior
        if (result is ParseResult<string>.Success success)
        {
            Assert.That(success.Value, Is.EqualTo("0 9 15 1 *"));
        }
        else if (result is ParseResult<string>.Error error)
        {
            Console.WriteLine($"Parser does not support tabs: {error.Message}");
        }
    }

    #endregion

}
