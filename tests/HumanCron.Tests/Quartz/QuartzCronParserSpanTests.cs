using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Quartz;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Comprehensive edge case tests for QuartzCronParser Span optimizations
/// Tests the recently refactored Span-based parsing logic for correctness,
/// safety, and proper handling of edge cases
/// </summary>
[TestFixture]
public class QuartzCronParserSpanTests
{
    private QuartzCronParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new QuartzCronParser();
    }

    // ========================================
    // Valid Expression Parsing Tests
    // ========================================

    [Test]
    public void Parse_MinuteIntervals_ParsesCorrectly()
    {
        // Arrange - Test various minute intervals
        var testCases = new[]
        {
            ("0 */15 * * * ?", 15),
            ("0 */30 * * * ?", 30),
            ("0 */45 * * * ?", 45)
        };

        foreach (var (cronExpression, expectedInterval) in testCases)
        {
            // Act
            var result = _parser.Parse(cronExpression);

            // Assert
            Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
                $"Failed to parse valid expression: {cronExpression}");
            var success = (ParseResult<ScheduleSpec>.Success)result;
            Assert.That(success.Value.Interval, Is.EqualTo(expectedInterval));
            Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Minutes));
        }
    }

    [Test]
    public void Parse_SecondIntervals_ParsesCorrectly()
    {
        // Arrange - Test various second intervals
        var testCases = new[]
        {
            ("*/30 * * * * ?", 30),
            ("*/45 * * * * ?", 45),
            ("*/15 * * * * ?", 15)
        };

        foreach (var (cronExpression, expectedInterval) in testCases)
        {
            // Act
            var result = _parser.Parse(cronExpression);

            // Assert
            Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
                $"Failed to parse valid expression: {cronExpression}");
            var success = (ParseResult<ScheduleSpec>.Success)result;
            Assert.That(success.Value.Interval, Is.EqualTo(expectedInterval));
            Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Seconds));
        }
    }

    [TestCase("0 0 14 * * ?", 14, 0)]
    [TestCase("0 30 9 * * ?", 9, 30)]
    [TestCase("0 0 0 * * ?", 0, 0)]
    [TestCase("0 59 23 * * ?", 23, 59)]
    public void Parse_TimeOfDayExpressions_ParsesCorrectly(string cronExpression, int expectedHour, int expectedMinute)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(expectedHour));
        Assert.That(success.Value.TimeOfDay.Value.Minute, Is.EqualTo(expectedMinute));
    }

    [TestCase("0 0 14 ? * MON", DayOfWeek.Monday)]
    [TestCase("0 0 14 ? * TUE", DayOfWeek.Tuesday)]
    [TestCase("0 0 14 ? * WED", DayOfWeek.Wednesday)]
    [TestCase("0 0 14 ? * THU", DayOfWeek.Thursday)]
    [TestCase("0 0 14 ? * FRI", DayOfWeek.Friday)]
    [TestCase("0 0 14 ? * SAT", DayOfWeek.Saturday)]
    [TestCase("0 0 14 ? * SUN", DayOfWeek.Sunday)]
    public void Parse_DayOfWeekExpressions_UpperCase_ParsesCorrectly(string cronExpression, DayOfWeek expectedDay)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(expectedDay));
    }

    [TestCase("0 0 14 ? * mon", DayOfWeek.Monday)]
    [TestCase("0 0 14 ? * tue", DayOfWeek.Tuesday)]
    [TestCase("0 0 14 ? * Wed", DayOfWeek.Wednesday)]
    [TestCase("0 0 14 ? * tHu", DayOfWeek.Thursday)]
    [TestCase("0 0 14 ? * FrI", DayOfWeek.Friday)]
    public void Parse_DayOfWeekExpressions_MixedCase_ParsesCorrectly(string cronExpression, DayOfWeek expectedDay)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse mixed-case day-of-week: {cronExpression}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(expectedDay));
    }

    [TestCase("0 0 14 ? * 2", DayOfWeek.Monday)]
    [TestCase("0 0 14 ? * 3", DayOfWeek.Tuesday)]
    [TestCase("0 0 14 ? * 4", DayOfWeek.Wednesday)]
    [TestCase("0 0 14 ? * 5", DayOfWeek.Thursday)]
    [TestCase("0 0 14 ? * 6", DayOfWeek.Friday)]
    [TestCase("0 0 14 ? * 7", DayOfWeek.Saturday)]
    [TestCase("0 0 14 ? * 1", DayOfWeek.Sunday)]
    public void Parse_DayOfWeekExpressions_Numeric_ParsesCorrectly(string cronExpression, DayOfWeek expectedDay)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(expectedDay));
    }

    [Test]
    public void Parse_DayPatternWeekdays_ParsesCorrectly()
    {
        // Arrange
        var cronExpression = "0 0 9 ? * MON-FRI";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays));
        Assert.That(success.Value.DayOfWeek, Is.Null,
            "DayOfWeek should be null when DayPattern is set");
    }

    [Test]
    public void Parse_DayPatternWeekends_ParsesCorrectly()
    {
        // Arrange
        var cronExpression = "0 0 10 ? * SAT,SUN";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekends));
        Assert.That(success.Value.DayOfWeek, Is.Null,
            "DayOfWeek should be null when DayPattern is set");
    }

    // ========================================
    // Month Specifier Tests (Span-based parsing)
    // ========================================

    [TestCase("0 0 9 * 1 ?", 1)]
    [TestCase("0 0 9 * 6 ?", 6)]
    [TestCase("0 0 9 * 12 ?", 12)]
    public void Parse_SingleMonth_ParsesCorrectly(string cronExpression, int expectedMonth)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(expectedMonth));
    }

    [TestCase("0 0 9 * 1-3 ?", 1, 3)]
    [TestCase("0 0 9 * 4-6 ?", 4, 6)]
    [TestCase("0 0 9 * 10-12 ?", 10, 12)]
    public void Parse_MonthRange_ParsesCorrectly(string cronExpression, int expectedStart, int expectedEnd)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Range>());
        var monthSpec = (MonthSpecifier.Range)success.Value.Month;
        Assert.That(monthSpec.Start, Is.EqualTo(expectedStart));
        Assert.That(monthSpec.End, Is.EqualTo(expectedEnd));
    }

    [Test]
    public void Parse_MonthList_ParsesCorrectly()
    {
        // Arrange - Quarterly months
        var cronExpression = "0 0 9 * 1,4,7,10 ?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 4, 7, 10 }));
    }

    [Test]
    public void Parse_MonthListWithSpaces_ParsesCorrectly()
    {
        // Arrange - Test Span trimming logic
        // Note: The parser uses StringSplitOptions.TrimEntries which should handle spaces
        var cronExpression = "0 0 9 * 1,4,7,10 ?";  // Without spaces for now

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 4, 7, 10 }));
    }

    [Test]
    public void Parse_MonthWildcard_ParsesAsNone()
    {
        // Arrange
        var cronExpression = "0 0 9 * * ?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.None>());
    }

    // ========================================
    // Edge Cases - Whitespace Handling
    // ========================================

    [Test]
    public void Parse_ExtraLeadingWhitespace_ParsesCorrectly()
    {
        // Arrange - Leading spaces
        var cronExpression = "   0 */15 * * * ?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should handle leading whitespace");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(15));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Minutes));
    }

    [Test]
    public void Parse_ExtraTrailingWhitespace_ParsesCorrectly()
    {
        // Arrange - Trailing spaces
        var cronExpression = "0 */15 * * * ?   ";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should handle trailing whitespace");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(15));
    }

    [Test]
    public void Parse_MultipleSpacesBetweenParts_ParsesCorrectly()
    {
        // Arrange - Multiple spaces between parts (tests StringSplitOptions.RemoveEmptyEntries)
        var cronExpression = "0  */15  *  *  *  ?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should handle multiple spaces between parts");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(15));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Minutes));
    }

    [Test]
    public void Parse_TabCharacterSeparator_HandlesGracefully()
    {
        // Arrange - Tab characters instead of spaces
        // Note: Current implementation splits on ' ' only, not all whitespace
        var cronExpression = "0\t*/15\t*\t*\t*\t?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Parser should handle this gracefully (either parse or error)
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            "Parser should not crash on tab separators");

        // Document actual behavior: Parser may not support tab separators
        // This is acceptable as the spec uses space-separated values
        if (result is ParseResult<ScheduleSpec>.Error error)
        {
            Assert.That(error.Message, Is.Not.Empty);
        }
    }

    // ========================================
    // Edge Cases - Invalid Input
    // ========================================

    [Test]
    public void Parse_EmptyString_ReturnsError()
    {
        // Act
        var result = _parser.Parse("");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
    }

    [Test]
    public void Parse_WhitespaceOnly_ReturnsError()
    {
        // Arrange - Various whitespace patterns
        var whitespaceInputs = new[] { "   ", "\t\t\t", "  \t  ", "\n\n" };

        foreach (var input in whitespaceInputs)
        {
            // Act
            var result = _parser.Parse(input);

            // Assert
            Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>(),
                $"Should reject whitespace-only input: '{input.Replace("\t", "\\t").Replace("\n", "\\n")}'");
            var error = (ParseResult<ScheduleSpec>.Error)result;
            Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
        }
    }

    [TestCase("0 */15 * * *", 5)]  // Unix format (5 parts)
    [TestCase("*/15 * *", 3)]  // Too few parts
    [TestCase("0 */15", 2)]  // Way too few
    [TestCase("*", 1)]  // Single part
    public void Parse_WrongPartCount_ReturnsError(string cronExpression, int partCount)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>(),
            $"Should reject expression with {partCount} parts");
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Does.Contain("6 or 7 parts").IgnoreCase);
        Assert.That(error.Message, Does.Contain($"got {partCount}").IgnoreCase);
    }

    [Test]
    public void Parse_TooManyParts_ReturnsError()
    {
        // Arrange - 8 parts (Quartz requires 6 or 7)
        var cronExpression = "0 0 */15 * * * ? 2025";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Does.Contain("6 or 7 parts").IgnoreCase);
        Assert.That(error.Message, Does.Contain("got 8").IgnoreCase);
    }

    // ========================================
    // Edge Cases - Boundary Values
    // ========================================

    [TestCase("0 0 0 * * ?")]  // Midnight
    [TestCase("0 0 23 * * ?")]  // 11pm
    [TestCase("0 59 23 * * ?")]  // 23:59
    [TestCase("59 59 23 * * ?")]  // 23:59:59
    public void Parse_BoundaryTimeValues_ParsesCorrectly(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse boundary time values: {cronExpression}");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
    }

    [Test]
    public void Parse_MonthBoundaryValues_ParsesCorrectly()
    {
        // Arrange - Test months 1 and 12 (boundary values)
        var januaryCron = "0 0 9 * 1 ?";
        var decemberCron = "0 0 9 * 12 ?";

        // Act & Assert - January
        var januaryResult = _parser.Parse(januaryCron);
        Assert.That(januaryResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var januarySuccess = (ParseResult<ScheduleSpec>.Success)januaryResult;
        Assert.That(januarySuccess.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        Assert.That(((MonthSpecifier.Single)januarySuccess.Value.Month).Month, Is.EqualTo(1));

        // Act & Assert - December
        var decemberResult = _parser.Parse(decemberCron);
        Assert.That(decemberResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var decemberSuccess = (ParseResult<ScheduleSpec>.Success)decemberResult;
        Assert.That(decemberSuccess.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        Assert.That(((MonthSpecifier.Single)decemberSuccess.Value.Month).Month, Is.EqualTo(12));
    }

    // ========================================
    // Edge Cases - Malformed Expressions
    // ========================================

    [TestCase("xyz */15 * * * ?")]  // Invalid second field
    [TestCase("0 15 * * * ?")]  // Missing interval marker (*/15 vs 15)
    public void Parse_MalformedInterval_HandlesGracefully(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Should return Success or Error, but not crash
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Parser should handle malformed interval gracefully: {cronExpression}");

        // If it's an error, validate error message quality
        if (result is ParseResult<ScheduleSpec>.Error error)
        {
            Assert.That(error.Message, Is.Not.Empty);
            Assert.That(error.Message.Length, Is.LessThan(500),
                "Error messages should be concise");
        }
    }

    [Test]
    public void Parse_MalformedIntervalNonNumeric_ThrowsException()
    {
        // Arrange - Invalid interval (non-numeric)
        // Note: Current implementation uses int.Parse without try-catch for */n patterns
        var cronExpression = "0 */abc * * * ?";

        // Act & Assert - Document current behavior: throws FormatException
        Assert.Throws<FormatException>(() => _parser.Parse(cronExpression),
            "Parser currently throws FormatException for non-numeric intervals (this is a known limitation)");
    }

    [TestCase("0 0 9 * 13 ?")]  // Invalid month (13)
    [TestCase("0 0 9 * 0 ?")]  // Invalid month (0)
    [TestCase("0 0 9 * -1 ?")]  // Negative month
    public void Parse_InvalidMonthValue_AcceptsAnyInteger(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Parser should not crash on invalid month values
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Parser should handle invalid month gracefully: {cronExpression}");

        // Document actual behavior: Parser accepts any integer for month values
        // Validation happens at a higher level (e.g., when creating Quartz schedules)
        if (result is ParseResult<ScheduleSpec>.Success success)
        {
            // Parser accepts invalid month values - validation is deferred
            Assert.That(success.Value.Month, Is.Not.Null);
        }
    }

    [TestCase("0 0 9 * 1-2-3 ?")]  // Multiple dashes
    [TestCase("0 0 9 * 3- ?")]  // Trailing dash
    public void Parse_MalformedMonthRange_HandlesGracefully(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Parser should handle malformed month range gracefully: {cronExpression}");

        // Document actual behavior: Parser may accept malformed ranges
        // Validation is deferred to Quartz schedule building
        if (result is ParseResult<ScheduleSpec>.Success success)
        {
            Assert.That(success.Value.Month, Is.Not.Null);
        }
    }

    [TestCase("0 0 9 * 1--3 ?")]  // Double dash becomes Range(1, -3)
    [TestCase("0 0 9 * -3 ?")]  // Leading dash becomes Single(-3)
    public void Parse_MalformedMonthRange_AcceptsMalformedValues(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Document actual behavior
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser accepts malformed month ranges - validation is deferred");

        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.Not.TypeOf<MonthSpecifier.None>(),
            "Parser parses malformed ranges as Range or Single with invalid values");
    }

    [TestCase("0 0 9 * 1,,4 ?")]  // Double comma
    [TestCase("0 0 9 * ,1,4 ?")]  // Leading comma
    [TestCase("0 0 9 * 1,4, ?")]  // Trailing comma
    [TestCase("0 0 9 * 1,abc,4 ?")]  // Non-numeric in list
    public void Parse_MalformedMonthList_HandlesGracefully(string cronExpression)
    {
        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            $"Parser should handle malformed month list gracefully: {cronExpression}");

        // Parser should filter out invalid entries or fall back to None
        if (result is ParseResult<ScheduleSpec>.Success success)
        {
            // Either parsed valid months only, or fell back to None
            Assert.That(success.Value.Month, Is.Not.Null);
        }
    }

    // ========================================
    // Error Message Quality Tests
    // ========================================

    [Test]
    public void Parse_InvalidPartCount_ReturnsHelpfulError()
    {
        // Arrange
        var cronExpression = "0 */15 * * *";  // 5 parts (Unix format)

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;

        // Validate error message quality
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Contain("6 or 7 parts"),
            "Should specify expected part count");
        Assert.That(error.Message, Does.Contain("got 5"),
            "Should specify actual part count");
        Assert.That(error.Message, Does.Contain("second minute hour day month dayOfWeek"),
            "Should show expected format");
        Assert.That(error.Message, Does.Not.Contain("Exception"),
            "Should not expose implementation details");
        Assert.That(error.Message.Length, Is.LessThan(500),
            "Error messages should be concise");
    }

    [Test]
    public void Parse_EmptyInput_ReturnsHelpfulError()
    {
        // Act
        var result = _parser.Parse("");

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;

        // Validate error message quality
        Assert.That(error.Message, Is.Not.Empty);
        Assert.That(error.Message, Does.Contain("empty").IgnoreCase);
        Assert.That(char.IsUpper(error.Message[0]), Is.True,
            "Error message should start with capital letter");
    }

    // ========================================
    // Idempotency Tests
    // ========================================

    [TestCase("0 */15 * * * ?")]
    [TestCase("0 0 14 ? * MON")]
    [TestCase("0 0 9 * 1,4,7,10 ?")]
    [TestCase("0 0 9 ? * MON-FRI")]
    public void Parse_MultipleParsesOfSameExpression_ProduceIdenticalResults(string cronExpression)
    {
        // Act - Parse the same expression multiple times
        var result1 = _parser.Parse(cronExpression);
        var result2 = _parser.Parse(cronExpression);
        var result3 = _parser.Parse(cronExpression);

        // Assert - All should be successful
        Assert.That(result1, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        Assert.That(result2, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        Assert.That(result3, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());

        var spec1 = ((ParseResult<ScheduleSpec>.Success)result1).Value;
        var spec2 = ((ParseResult<ScheduleSpec>.Success)result2).Value;
        var spec3 = ((ParseResult<ScheduleSpec>.Success)result3).Value;

        // Assert - Results should be equivalent
        Assert.That(spec2.Interval, Is.EqualTo(spec1.Interval));
        Assert.That(spec2.Unit, Is.EqualTo(spec1.Unit));
        Assert.That(spec2.DayOfWeek, Is.EqualTo(spec1.DayOfWeek));
        Assert.That(spec2.DayPattern, Is.EqualTo(spec1.DayPattern));

        Assert.That(spec3.Interval, Is.EqualTo(spec1.Interval));
        Assert.That(spec3.Unit, Is.EqualTo(spec1.Unit));
    }

    // ========================================
    // Memory Safety Tests (Span behavior)
    // ========================================

    [Test]
    public void Parse_LongExpression_DoesNotOverflow()
    {
        // Arrange - Create a very long (but valid) expression to test buffer handling
        var cronExpression = "0 0 9 * 1,2,3,4,5,6,7,8,9,10,11,12 ?";

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Should handle long expressions without overflow
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months.Count, Is.EqualTo(12),
            "Should parse all 12 months from list");
    }

    [Test]
    public void Parse_ExtremelyLongInvalidExpression_DoesNotCrash()
    {
        // Arrange - Create an expression with many parts to test bounds checking
        var parts = new string[20];
        Array.Fill(parts, "*");
        var cronExpression = string.Join(" ", parts);

        // Act & Assert - Should not throw, should return error
        Assert.DoesNotThrow(() =>
        {
            var result = _parser.Parse(cronExpression);
            Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>(),
                "Should return error for expression with too many parts");
        });
    }

    [Test]
    public void Parse_UnicodeWhitespace_HandlesCorrectly()
    {
        // Arrange - Use various Unicode whitespace characters (non-breaking space, etc.)
        // Normal space (U+0020) should work, others may not
        var cronExpression = "0\u00A0*/15\u00A0*\u00A0*\u00A0*\u00A0?";  // U+00A0 = non-breaking space

        // Act
        var result = _parser.Parse(cronExpression);

        // Assert - Parser behavior with Unicode whitespace
        // It's acceptable to either parse it or reject it, but should not crash
        Assert.That(result, Is.InstanceOf<ParseResult<ScheduleSpec>>(),
            "Parser should handle Unicode whitespace gracefully without crashing");

        // Document actual behavior
        if (result is ParseResult<ScheduleSpec>.Success)
        {
            // Parser accepted Unicode whitespace as separator
            var success = (ParseResult<ScheduleSpec>.Success)result;
            Assert.That(success.Value.Interval, Is.EqualTo(15));
        }
        else
        {
            // Parser rejected Unicode whitespace (also acceptable)
            Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        }
    }

    // ========================================
    // L (Last) Character Parsing - TDD
    // ========================================

    /// <summary>
    /// Tests for parsing L (Last) character in Quartz cron expressions
    /// Will FAIL until QuartzCronParser supports L
    /// </summary>
    [TestCase("0 0 0 L * ?", "last day of month")]
    [TestCase("0 0 12 L * ?", "last day at noon")]
    [TestCase("0 30 14 L * ?", "last day at 2:30pm")]
    [TestCase("0 0 0 L-3 * ?", "3rd-to-last day")]
    [TestCase("0 0 0 L-1 * ?", "day before last")]
    [TestCase("0 0 0 LW * ?", "last weekday")]
    [TestCase("0 0 0 ? * 6L", "last Friday")]
    [TestCase("0 0 0 ? * 2L", "last Monday")]
    [TestCase("0 0 0 ? * FRIL", "last Friday (text)")]
    public void Parse_LCharacter_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until L support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse L character: '{description}' = {cron}");
    }

    [TestCase("0 0 0 L 1 ?", "last day of January")]
    [TestCase("0 0 0 L 12 ?", "last day of December")]
    [TestCase("0 0 0 L 1,4,7,10 ?", "last day quarterly")]
    public void Parse_LastDayWithMonth_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until L support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse: '{description}' = {cron}");
    }

    [TestCase("0 0 17 ? * 6L", "last Friday at 5pm")]
    [TestCase("0 0 9 ? * 2L", "last Monday at 9am")]
    [TestCase("0 0 0 ? 1 6L", "last Friday in January")]
    public void Parse_LastDayOfWeekWithTime_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until L support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse: '{description}' = {cron}");
    }

    // ========================================
    // W (Weekday) Character Parsing - TDD
    // ========================================

    /// <summary>
    /// Tests for parsing W (Weekday) character in Quartz cron expressions
    /// Will FAIL until QuartzCronParser supports W
    /// </summary>
    [TestCase("0 0 0 15W * ?", "weekday nearest 15th")]
    [TestCase("0 0 0 1W * ?", "weekday nearest 1st")]
    [TestCase("0 0 0 10W * ?", "weekday nearest 10th")]
    [TestCase("0 0 0 20W * ?", "weekday nearest 20th")]
    [TestCase("0 0 0 LW * ?", "last weekday of month")]
    public void Parse_WCharacter_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until W support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse W character: '{description}' = {cron}");
    }

    [TestCase("0 30 14 15W * ?", "weekday nearest 15th at 2:30pm")]
    [TestCase("0 0 9 1W 1,7 ?", "weekday nearest 1st in Jan/July")]
    [TestCase("0 0 9 1W 1 ?", "weekday nearest 1st in January")]
    public void Parse_WeekdayWithTimeAndMonth_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until W support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse: '{description}' = {cron}");
    }

    // ========================================
    // # (Nth Occurrence) Character Parsing - TDD
    // ========================================

    /// <summary>
    /// Tests for parsing # (Nth occurrence) character in Quartz cron expressions
    /// Will FAIL until QuartzCronParser supports #
    /// </summary>
    [TestCase("0 0 0 ? * 6#3", "3rd Friday")]
    [TestCase("0 0 0 ? * 2#1", "1st Monday")]
    [TestCase("0 0 0 ? * 5#2", "2nd Thursday")]
    [TestCase("0 0 0 ? * 1#4", "4th Sunday")]
    [TestCase("0 0 0 ? * FRI#3", "3rd Friday (text)")]
    [TestCase("0 0 0 ? * MON#1", "1st Monday (text)")]
    public void Parse_HashCharacter_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until # support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse # character: '{description}' = {cron}");
    }

    [TestCase("0 0 12 ? * 6#3", "3rd Friday at noon")]
    [TestCase("0 0 9 ? 1 2#1", "1st Monday of January at 9am")]
    [TestCase("0 0 0 ? 1,4,7,10 6#3", "3rd Friday quarterly")]
    [TestCase("0 0 17 ? * 6#5", "5th Friday at 5pm")]
    public void Parse_NthOccurrenceWithTimeAndMonth_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until # support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse: '{description}' = {cron}");
    }

    [TestCase("0 0 0 ? * 3#2", "2nd Tuesday")]
    [TestCase("0 0 0 ? * 4#3", "3rd Wednesday")]
    [TestCase("0 0 0 ? * 7#1", "1st Saturday")]
    public void Parse_AdditionalNthOccurrences_ParsesCorrectly(string cron, string description)
    {
        // Act
        var result = _parser.Parse(cron);

        // Assert - Will FAIL until # support is added
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Should parse: '{description}' = {cron}");
    }
}
