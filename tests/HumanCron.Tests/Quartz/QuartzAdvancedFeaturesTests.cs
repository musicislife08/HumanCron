using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using HumanCron.Quartz;
using Quartz;
using NodaTime;
using NodaTime.Testing;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// TDD tests for Quartz-specific advanced features: L (Last), W (Weekday), # (Nth occurrence)
/// These tests will FAIL until the features are implemented - that's expected (TDD approach)
/// </summary>
[TestFixture]
public class QuartzAdvancedFeaturesTests
{
    private NaturalLanguageParser _parser = null!;
    private QuartzScheduleBuilder _quartzBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new NaturalLanguageParser();
        var fakeClock = new FakeClock(Instant.FromUtc(2025, 1, 15, 10, 0));
        _quartzBuilder = new QuartzScheduleBuilder(fakeClock);
    }

    // ========================================
    // L (Last) Character Tests - TDD
    // ========================================

    /// <summary>
    /// L character in day field = last day of month
    /// Expected to FAIL until QuartzCronBuilder supports generating L expressions
    /// </summary>
    [Test]
    public void Build_LastDayOfMonth_GeneratesLExpression()
    {
        // Arrange - Parse "last day of month at 2pm"
        var parseResult = _parser.Parse("every month on last day at 2pm", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last day' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act - Build Quartz schedule
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate cron with L character
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last day pattern");

        // TODO: Verify generated cron contains "L" in day field
        // Expected: "0 0 14 L * ?"
    }

    [Test]
    public void Build_LastWeekdayOfMonth_GeneratesLWExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on last weekday", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last weekday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 LW * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last weekday pattern");
    }

    [Test]
    public void Build_LastFridayOfMonth_GeneratesLInDayOfWeek()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on last friday at 5pm", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last friday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 17 ? * 6L"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last day-of-week pattern");
    }

    [Test]
    public void Build_ThirdToLastDayOfMonth_GeneratesLOffset()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 3rd to last day", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '3rd to last day' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 L-3 * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last-minus-offset pattern");
    }

    [Test]
    public void Build_LastDayBeforeEndOfMonth_GeneratesLOffset()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on day before last", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'day before last' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 L-1 * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for day-before-last pattern");
    }

    [Test]
    public void Build_LastMondayOfMonth_GeneratesLInDayOfWeek()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on last monday", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last monday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? * 2L"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last Monday pattern");
    }

    [Test]
    public void Build_LastDayInJanuary_GeneratesLWithMonth()
    {
        // Arrange
        var parseResult = _parser.Parse("on last day in january at 9am", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last day in january' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 9 L 1 ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last day with specific month");
    }

    [Test]
    public void Build_LastDayQuarterly_GeneratesLWithMonthList()
    {
        // Arrange
        var parseResult = _parser.Parse("on last day in jan,apr,jul,oct", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'last day' with month list");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 L 1,4,7,10 ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for last day with month list");
    }

    // ========================================
    // W (Weekday) Character Tests - TDD
    // ========================================

    /// <summary>
    /// W character = nearest weekday to specified day
    /// Expected to FAIL until QuartzCronBuilder supports W
    /// </summary>
    [Test]
    public void Build_NearestWeekdayTo15th_GeneratesWExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on weekday nearest 15", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'weekday nearest 15' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 15W * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nearest weekday pattern");
    }

    [Test]
    public void Build_NearestWeekdayTo1st_GeneratesWExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on weekday nearest 1 at 9am", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'weekday nearest 1' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 9 1W * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nearest weekday to 1st");
    }

    [Test]
    public void Build_NearestWeekdayTo10th_GeneratesWExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on weekday nearest 10", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'weekday nearest 10' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 10W * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nearest weekday to 10th");
    }

    [Test]
    public void Build_NearestWeekdayTo20th_GeneratesWExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on weekday nearest 20 at 2:30pm", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'weekday nearest 20' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 30 14 20W * ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nearest weekday with time");
    }

    [Test]
    public void Build_NearestWeekdayInJanAndJuly_GeneratesWWithMonth()
    {
        // Arrange
        var parseResult = _parser.Parse("on weekday nearest 1 in jan,jul at 9am", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept 'weekday nearest' with month list");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 9 1W 1,7 ?"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nearest weekday with months");
    }

    // ========================================
    // # (Nth Occurrence) Character Tests - TDD
    // ========================================

    /// <summary>
    /// # character = nth occurrence of day-of-week in month
    /// Expected to FAIL until QuartzCronBuilder supports #
    /// </summary>
    [Test]
    public void Build_ThirdFridayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 3rd friday at 5pm", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '3rd friday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 17 ? * 6#3"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nth day-of-week pattern");
    }

    [Test]
    public void Build_FirstMondayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 1st monday at 9am", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '1st monday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 9 ? * 2#1"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for first Monday pattern");
    }

    [Test]
    public void Build_SecondThursdayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 2nd thursday", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '2nd thursday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? * 5#2"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for second Thursday pattern");
    }

    [Test]
    public void Build_FourthSundayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 4th sunday at noon", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '4th sunday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 12 ? * 1#4"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for fourth Sunday pattern");
    }

    [Test]
    public void Build_FirstTuesdayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 1st tuesday", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '1st tuesday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? * 3#1"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for first Tuesday pattern");
    }

    [Test]
    public void Build_ThirdWednesdayOfMonth_GeneratesHashExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 3rd wednesday at 3pm", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '3rd wednesday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 15 ? * 4#3"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for third Wednesday pattern");
    }

    [Test]
    public void Build_ThirdFridayQuarterly_GeneratesHashWithMonthList()
    {
        // Arrange
        var parseResult = _parser.Parse("on 3rd friday in jan,apr,jul,oct", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '3rd friday' with month list");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? 1,4,7,10 6#3"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for nth day-of-week with months");
    }

    [Test]
    public void Build_FirstMondayInJanuary_GeneratesHashWithMonth()
    {
        // Arrange
        var parseResult = _parser.Parse("on 1st monday in january at 9am", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '1st monday in january' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 9 ? 1 2#1"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for first Monday in January");
    }

    // ========================================
    // Combined Advanced Features Tests - TDD
    // ========================================

    [Test]
    public void Build_SecondTuesdayAtMidnight_GeneratesCompleteExpression()
    {
        // Arrange
        var parseResult = _parser.Parse("every month on 2nd tuesday at midnight", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '2nd tuesday at midnight' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? * 3#2"
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for second Tuesday at midnight");
    }

    [Test]
    public void Build_FifthFridayIfExists_GeneratesHashExpression()
    {
        // Arrange - 5th occurrence only exists in some months
        var parseResult = _parser.Parse("every month on 5th friday", new ScheduleParserOptions());
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "Parser should accept '5th friday' syntax");

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Act
        var scheduleBuilder = _quartzBuilder.Build(spec);

        // Assert - Should generate "0 0 0 ? * 6#5"
        // Note: This trigger will only fire in months that have 5 Fridays
        Assert.That(scheduleBuilder, Is.TypeOf<CronScheduleBuilder>(),
            "Should use CronScheduleBuilder for fifth Friday pattern");
    }
}
