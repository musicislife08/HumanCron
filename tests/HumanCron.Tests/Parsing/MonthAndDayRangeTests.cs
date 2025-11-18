using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using HumanCron.Converters.Unix;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Parsing;

/// <summary>
/// Comprehensive tests for month selection and day range features
/// Covers single months, month ranges, month lists, and day ranges
/// </summary>
[TestFixture]
public class MonthAndDayRangeTests
{
    private NaturalLanguageParser _parser = null!;
    private UnixCronBuilder _cronBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new NaturalLanguageParser();
        // Use FakeClock for deterministic testing - set to Jan 15, 2025 at 10:00 UTC
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        _cronBuilder = new UnixCronBuilder(fakeClock, DateTimeZone.Utc);
    }

    // ========================================
    // Single Month Patterns
    // ========================================

    [TestCase("every day in january", 1)]
    [TestCase("every day in february", 2)]
    [TestCase("every day in march", 3)]
    [TestCase("every day in april", 4)]
    [TestCase("every day in may", 5)]
    [TestCase("every day in june", 6)]
    [TestCase("every day in july", 7)]
    [TestCase("every day in august", 8)]
    [TestCase("every day in september", 9)]
    [TestCase("every day in october", 10)]
    [TestCase("every day in november", 11)]
    [TestCase("every day in december", 12)]
    public void Parse_DailyInSpecificMonth_ParsesCorrectly(string input, int expectedMonth)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(expectedMonth));
    }

    [TestCase("every day in jan", 1)]
    [TestCase("every day in feb", 2)]
    [TestCase("every day in mar", 3)]
    [TestCase("every day in apr", 4)]
    [TestCase("every day in may", 5)]
    [TestCase("every day in jun", 6)]
    [TestCase("every day in jul", 7)]
    [TestCase("every day in aug", 8)]
    [TestCase("every day in sep", 9)]
    [TestCase("every day in oct", 10)]
    [TestCase("every day in nov", 11)]
    [TestCase("every day in dec", 12)]
    public void Parse_DailyInSpecificMonth_AcceptsAbbreviations(string input, int expectedMonth)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(expectedMonth));
    }

    [Test]
    public void Parse_DailyInJanuaryAt9am_ParsesCorrectly()
    {
        // Arrange
        var input = "every day in january at 9am";
        var options = new ScheduleParserOptions { TimeZone = DateTimeZone.Utc };

        // Act
        var result = _parser.Parse(input, options);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(1));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_MondayInJanuary_ParsesCorrectly()
    {
        // Arrange
        var input = "every monday in january";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(1));
    }

    [Test]
    public void Parse_MonthOn15InJanuary_ParsesCorrectly()
    {
        // Arrange
        var input = "every month on 15 in january";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(15));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(1));
    }

    // ========================================
    // Month Range Patterns
    // ========================================

    [TestCase("every day between january and march", 1, 3)]
    [TestCase("every day between april and june", 4, 6)]
    [TestCase("every day between july and september", 7, 9)]
    [TestCase("every day between october and december", 10, 12)]
    public void Parse_DailyBetweenMonths_ParsesCorrectly(string input, int startMonth, int endMonth)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Range>());
        var monthSpec = (MonthSpecifier.Range)success.Value.Month;
        Assert.That(monthSpec.Start, Is.EqualTo(startMonth));
        Assert.That(monthSpec.End, Is.EqualTo(endMonth));
    }

    [TestCase("every day between jan and mar", 1, 3)]
    [TestCase("every day between apr and jun", 4, 6)]
    [TestCase("every day between jul and sep", 7, 9)]
    [TestCase("every day between oct and dec", 10, 12)]
    public void Parse_DailyBetweenMonths_AcceptsAbbreviations(string input, int startMonth, int endMonth)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Range>());
        var monthSpec = (MonthSpecifier.Range)success.Value.Month;
        Assert.That(monthSpec.Start, Is.EqualTo(startMonth));
        Assert.That(monthSpec.End, Is.EqualTo(endMonth));
    }

    [Test]
    public void Parse_DailyBetweenJanuaryAndMarchAt9am_ParsesCorrectly()
    {
        // Arrange
        var input = "every day between january and march at 9am";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Range>());
        var monthSpec = (MonthSpecifier.Range)success.Value.Month;
        Assert.That(monthSpec.Start, Is.EqualTo(1));
        Assert.That(monthSpec.End, Is.EqualTo(3));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_MondayBetweenJanuaryAndMarch_ParsesCorrectly()
    {
        // Arrange
        var input = "every monday between january and march";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Range>());
        var monthSpec = (MonthSpecifier.Range)success.Value.Month;
        Assert.That(monthSpec.Start, Is.EqualTo(1));
        Assert.That(monthSpec.End, Is.EqualTo(3));
    }

    // ========================================
    // Month List Patterns
    // ========================================

    [Test]
    public void Parse_DailyInQuarterlyMonths_ParsesCorrectly()
    {
        // Arrange
        var input = "every day in january,april,july,october";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 4, 7, 10 }));
    }

    [Test]
    public void Parse_DailyInQuarterlyMonths_AcceptsAbbreviations()
    {
        // Arrange
        var input = "every day in jan,apr,jul,oct";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 4, 7, 10 }));
    }

    [Test]
    public void Parse_DailyInJanuaryJulyAt9am_ParsesCorrectly()
    {
        // Arrange
        var input = "every day in january,july at 9am";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 7 }));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_MondayInJanuaryJuly_ParsesCorrectly()
    {
        // Arrange
        var input = "every monday in january,july";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 7 }));
    }

    // ========================================
    // Day Range Patterns
    // ========================================

    [TestCase("every day between monday and friday")]
    [TestCase("every day between mon and fri")]
    public void Parse_DailyBetweenMondayAndFriday_ParsesAsWeekdays(string input)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays),
            "Monday-Friday range should be recognized as weekdays pattern");
    }

    [TestCase("every day between saturday and sunday")]
    [TestCase("every day between sat and sun")]
    public void Parse_DailyBetweenSaturdayAndSunday_ParsesAsWeekends(string input)
    {
        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            $"Failed to parse '{input}'");
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekends),
            "Saturday-Sunday range should be recognized as weekends pattern");
    }

    [Test]
    public void Parse_DailyBetweenMondayAndFridayAt9am_ParsesCorrectly()
    {
        // Arrange
        var input = "every day between monday and friday at 9am";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_Every6HoursBetweenMondayAndFriday_ParsesCorrectly()
    {
        // Arrange
        var input = "every 6 hours between monday and friday";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Interval, Is.EqualTo(6));
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Hours));
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays));
    }

    // ========================================
    // Unix Cron Conversion Tests
    // ========================================

    [TestCase("every day in january", "0 0 * 1 *")]
    [TestCase("every day in december", "0 0 * 12 *")]
    public void UnixCron_SingleMonth_ConvertsCorrectly(string natural, string expectedCron)
    {
        // Arrange
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(expectedCron));
    }

    [TestCase("every day between january and march", "0 0 * 1-3 *")]
    [TestCase("every day between october and december", "0 0 * 10-12 *")]
    public void UnixCron_MonthRange_ConvertsCorrectly(string natural, string expectedCron)
    {
        // Arrange
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(expectedCron));
    }

    [TestCase("every day in january,april,july,october", "0 0 * 1,4,7,10 *")]
    [TestCase("every day in january,july", "0 0 * 1,7 *")]
    public void UnixCron_MonthList_ConvertsCorrectly(string natural, string expectedCron)
    {
        // Arrange
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(expectedCron));
    }

    [TestCase("every day between monday and friday", "0 0 * * 1-5")]  // Weekdays
    [TestCase("every day between saturday and sunday", "0 0 * * 0,6")]  // Weekends (list format avoids range wrap-around)
    public void UnixCron_DayRange_ConvertsCorrectly(string natural, string expectedCron)
    {
        // Arrange
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo(expectedCron));
    }

    [Test]
    public void UnixCron_MondayInJanuary_ConvertsCorrectly()
    {
        // Arrange
        var parseResult = _parser.Parse("every monday in january", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo("0 0 * 1 1"));  // Minute Hour Day Month DayOfWeek
    }

    [Test]
    public void UnixCron_DailyAt9amInJanuary_ConvertsCorrectly()
    {
        // Arrange
        var options = new ScheduleParserOptions { TimeZone = DateTimeZone.Utc };
        var parseResult = _parser.Parse("every day in january at 9am", options);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo("0 9 * 1 *"));
    }

    // ========================================
    // Complex Combined Patterns
    // ========================================

    [Test]
    public void Parse_ComplexPattern_MonthlyOn15InQuarterlyMonthsAt9am()
    {
        // Arrange
        var input = "every month on 15 in january,april,july,october at 9am";

        // Act
        var result = _parser.Parse(input, new ScheduleParserOptions());

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.Unit, Is.EqualTo(IntervalUnit.Months));
        Assert.That(success.Value.DayOfMonth, Is.EqualTo(15));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthSpec = (MonthSpecifier.List)success.Value.Month;
        Assert.That(monthSpec.Months, Is.EquivalentTo(new[] { 1, 4, 7, 10 }));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void Parse_ComplexPattern_WeekdaysInJanuaryAt9am()
    {
        // Arrange
        var input = "every weekday in january at 9am";
        var options = new ScheduleParserOptions { TimeZone = DateTimeZone.Utc };

        // Act
        var result = _parser.Parse(input, options);

        // Assert
        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var success = (ParseResult<ScheduleSpec>.Success)result;
        Assert.That(success.Value.DayPattern, Is.EqualTo(DayPattern.Weekdays));
        Assert.That(success.Value.Month, Is.TypeOf<MonthSpecifier.Single>());
        var monthSpec = (MonthSpecifier.Single)success.Value.Month;
        Assert.That(monthSpec.Month, Is.EqualTo(1));
        Assert.That(success.Value.TimeOfDay, Is.Not.Null);
        Assert.That(success.Value.TimeOfDay!.Value.Hour, Is.EqualTo(9));
    }

    [Test]
    public void UnixCron_ComplexPattern_WeekdaysInJanuaryAt9am()
    {
        // Arrange
        var options = new ScheduleParserOptions { TimeZone = DateTimeZone.Utc };
        var parseResult = _parser.Parse("every weekday in january at 9am", options);
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var buildResult = _cronBuilder.Build(spec);

        // Assert
        Assert.That(buildResult, Is.TypeOf<ParseResult<string>.Success>());
        var cron = ((ParseResult<string>.Success)buildResult).Value;
        Assert.That(cron, Is.EqualTo("0 9 * 1 1-5"));  // 9am, any day-of-month, January, Monday-Friday
    }
}
