using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;
using HumanCron.Quartz;
using HumanCron.Quartz.Abstractions;
using NodaTime;

namespace HumanCron.Tests.RoundTrip;

/// <summary>
/// Comprehensive tests for bidirectional conversion of all cron features
/// Tests both: Natural Language ↔ Cron and Cron ↔ Natural Language
/// </summary>
[TestFixture]
public class CompleteBidirectionalTests
{
    private NaturalLanguageParser _parser = null!;
    private NaturalLanguageFormatter _formatter = null!;
    private UnixCronConverter _unixConverter = null!;
    private IQuartzScheduleConverter _quartzConverter = null!;

    [SetUp]
    public void SetUp()
    {
        _parser = new NaturalLanguageParser();
        _formatter = new NaturalLanguageFormatter();
        _unixConverter = UnixCronConverter.Create();
        _quartzConverter = QuartzScheduleConverterFactory.Create();
    }

    // ===========================================
    // Quartz-Specific Advanced Features
    // ===========================================

    [Test]
    public void RoundTrip_LastDay_FormatsAndParsesCorrectly()
    {
        // Natural → Spec → Natural
        var natural = "every month on the last day";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.IsLastDay, Is.True);
    }

    [Test]
    public void RoundTrip_LastFriday_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the last friday";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.IsLastDayOfWeek, Is.True);
        Assert.That(spec.DayOfWeek, Is.EqualTo(DayOfWeek.Friday));
    }

    [Test]
    public void RoundTrip_ThirdToLastDay_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the 3rd to last day";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.LastDayOffset, Is.EqualTo(3));
    }

    [Test]
    public void RoundTrip_DayBeforeLast_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the day before last";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.LastDayOffset, Is.EqualTo(1));
    }

    [Test]
    public void RoundTrip_WeekdayNearest15th_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the weekday nearest the 15th";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.IsNearestWeekday, Is.True);
        Assert.That(spec.DayOfMonth, Is.EqualTo(15));
    }

    [Test]
    public void RoundTrip_LastWeekday_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the last weekday";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.IsLastDay, Is.True);
        Assert.That(spec.IsNearestWeekday, Is.True);
    }

    [Test]
    public void RoundTrip_ThirdFriday_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the 3rd friday";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.NthOccurrence, Is.EqualTo(3));
        Assert.That(spec.DayOfWeek, Is.EqualTo(DayOfWeek.Friday));
    }

    // ===========================================
    // Minute/Hour/Day Lists
    // ===========================================

    [Test]
    public void RoundTrip_MinuteList_FormatsAndParsesCorrectly()
    {
        var natural = "every hour at minutes 0,15,30,45";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.MinuteList, Is.EquivalentTo(new[] { 0, 15, 30, 45 }));
    }

    [Test]
    public void RoundTrip_HourList_FormatsAndParsesCorrectly()
    {
        var natural = "every day at hours 9,12,15,18";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.HourList, Is.EquivalentTo(new[] { 9, 12, 15, 18 }));
    }

    [Test]
    public void RoundTrip_HourListCompactNotation_ExpandsAndParses()
    {
        var natural = "every day at hours 9-12,14-17,20";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should expand and re-compact
        Assert.That(formatted, Is.EqualTo("every day at hours 9-12,14-17,20"));
        Assert.That(spec.HourList, Is.EquivalentTo(new[] { 9, 10, 11, 12, 14, 15, 16, 17, 20 }));
    }

    [Test]
    public void RoundTrip_DayList_FormatsAndParsesCorrectly()
    {
        var natural = "every month on the 1st, 15th, 30th";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo("every month on the 1st, 15th, 30th"));
        Assert.That(spec.DayList, Is.EquivalentTo(new[] { 1, 15, 30 }));
    }

    [Test]
    public void RoundTrip_DayListCompactNotation_ExpandsAndParses()
    {
        var natural = "every month on the 1-7,15-21,30";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should expand and re-compact (numeric notation, consistent with hours/minutes)
        Assert.That(formatted, Is.EqualTo("every month on the 1-7,15-21,30"));
        Assert.That(spec.DayList, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 15, 16, 17, 18, 19, 20, 21, 30 }));
    }

    [Test]
    public void RoundTrip_MinuteListCompactNotation_ExpandsAndParses()
    {
        var natural = "every hour at minutes 0-4,8-12,20";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should expand and re-compact
        Assert.That(formatted, Is.EqualTo("every hour at minutes 0-4,8-12,20"));
        Assert.That(spec.MinuteList, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 8, 9, 10, 11, 12, 20 }));
    }

    // ===========================================
    // Minute/Hour/Day Ranges
    // ===========================================

    [Test]
    public void RoundTrip_MinuteRange_FormatsAndParsesCorrectly()
    {
        var natural = "every hour between minutes 0 and 30";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.MinuteStart, Is.EqualTo(0));
        Assert.That(spec.MinuteEnd, Is.EqualTo(30));
    }

    [Test]
    public void RoundTrip_HourRange_FormatsAndParsesCorrectly()
    {
        var natural = "every day between hours 9 and 17";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.HourStart, Is.EqualTo(9));
        Assert.That(spec.HourEnd, Is.EqualTo(17));
    }

    [Test]
    public void RoundTrip_DayRangeWithOrdinals_FormatsAndParsesCorrectly()
    {
        var natural = "every month between the 1st and 15th";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.DayStart, Is.EqualTo(1));
        Assert.That(spec.DayEnd, Is.EqualTo(15));
    }

    // ===========================================
    // Range + Step
    // ===========================================

    [Test]
    public void RoundTrip_MinuteRangeStep_FormatsAndParsesCorrectly()
    {
        var natural = "every 5 minutes between 0 and 30 of each hour";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.MinuteStart, Is.EqualTo(0));
        Assert.That(spec.MinuteEnd, Is.EqualTo(30));
        Assert.That(spec.MinuteStep, Is.EqualTo(5));
    }

    [Test]
    public void RoundTrip_HourRangeStep_FormatsAndParsesCorrectly()
    {
        var natural = "every 2 hours between 9 and 17 of each day";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.HourStart, Is.EqualTo(9));
        Assert.That(spec.HourEnd, Is.EqualTo(17));
        Assert.That(spec.HourStep, Is.EqualTo(2));
    }

    [Test]
    public void RoundTrip_DayRangeStepWithOrdinals_FormatsAndParsesCorrectly()
    {
        var natural = "every 3 days between the 1st and 15th of each month";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.DayStart, Is.EqualTo(1));
        Assert.That(spec.DayEnd, Is.EqualTo(15));
        Assert.That(spec.DayStep, Is.EqualTo(3));
    }

    // ===========================================
    // Year Constraint
    // ===========================================

    [Test]
    public void RoundTrip_YearConstraint_FormatsAndParsesCorrectly()
    {
        var natural = "every day at 2pm in year 2025";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.Year, Is.EqualTo(2025));
    }

    [Test]
    public void RoundTrip_YearWithMonthConstraint_FormatsAndParsesCorrectly()
    {
        // Test that logical syntax round-trips correctly
        const string natural = "every year on january 15th in year 2025";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        using (Assert.EnterMultipleScope())
        {
            // Logical syntax should round-trip perfectly
            Assert.That(formatted, Is.EqualTo(natural));
            Assert.That(spec.Year, Is.EqualTo(2025));
            Assert.That(spec.Month, Is.TypeOf<MonthSpecifier.Single>());
        }
    }

    [Test]
    public void Format_IllogicalMonthlyWithSingleMonth_CorrectsToYearly()
    {
        // Test that illogical "every month ... in january" gets corrected to logical "every year on january 15th"
        const string illogicalInput = "every month on the 15th in january in year 2025";
        var parseResult = _parser.Parse(illogicalInput, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        var formatted = _formatter.Format(spec);

        using (Assert.EnterMultipleScope())
        {
            // Formatter corrects illogical "every month ... in january" to "on january 15th"
            Assert.That(formatted, Is.EqualTo("on january 15th in year 2025"));
            Assert.That(spec.Year, Is.EqualTo(2025));
            Assert.That(spec.Month, Is.TypeOf<MonthSpecifier.Single>());

            // Verify the corrected output can be parsed back
            var reparsed = _parser.Parse(formatted, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
            Assert.That(reparsed, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        }
    }

    // ===========================================
    // Ordinal Support
    // ===========================================

    [Test]
    public void Format_IllogicalMonthlyWithSingleMonth_VariousPatterns_CorrectsToYearly()
    {
        // Test various illogical "monthly + single month" patterns that should be corrected to simpler forms
        var testCases = new[]
        {
            // Day-of-week patterns
            ("every month on monday in january", "every monday in january"),

            // Nth occurrence patterns
            ("every month on 3rd friday in january", "on the 3rd friday in january"),

            // Last day patterns
            ("every month on last day in january", "on the last day in january"),

            // Day list patterns
            ("every month on the 1st, 15th in january", "on the 1st and 15th in january"),

            // Day range patterns
            ("every month between the 1st and 15th in january", "every day between the 1st and 15th in january"),

            // Day-of-month patterns (should use combined month+day syntax)
            ("every month on the 15th in january", "on january 15th"),
            ("every month on the 1st in january at 12am", "on january 1st at 12am"),
        };

        foreach (var (illogicalInput, expectedOutput) in testCases)
        {
            var parseResult = _parser.Parse(illogicalInput, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
            Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
                $"Failed to parse: {illogicalInput}");
            var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
            var formatted = _formatter.Format(spec);

            Assert.That(formatted, Is.EqualTo(expectedOutput),
                $"Input: '{illogicalInput}' should format to '{expectedOutput}' but got '{formatted}'");

            // Verify the corrected output can be parsed back
            var reparsed = _parser.Parse(formatted, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
            Assert.That(reparsed, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
                $"Failed to reparse corrected output: {formatted}");
        }
    }

    [Test]
    public void RoundTrip_DayOfMonthWithOrdinal_FormatsCorrectly()
    {
        // Parse with ordinal
        const string natural = "every month on the 15th";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should format with ordinal
        Assert.That(formatted, Is.EqualTo("every month on the 15th"));
        Assert.That(spec.DayOfMonth, Is.EqualTo(15));
    }

    [TestCase("1st")]
    [TestCase("2nd")]
    [TestCase("3rd")]
    [TestCase("4th")]
    [TestCase("21st")]
    [TestCase("22nd")]
    [TestCase("23rd")]
    [TestCase("31st")]
    public void RoundTrip_VariousOrdinals_ParseCorrectly(string ordinal)
    {
        var natural = $"every month on the {ordinal}";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should preserve ordinal in output
        Assert.That(formatted, Is.EqualTo(natural));
    }

    // ===========================================
    // Complex Combinations
    // ===========================================

    [Test]
    public void RoundTrip_ListWithMonthSelection_FormatsCorrectly()
    {
        var natural = "every day at hours 9,12,15,18 in january";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.HourList, Is.EquivalentTo(new[] { 9, 12, 15, 18 }));
        Assert.That(spec.Month, Is.TypeOf<MonthSpecifier.Single>());
    }

    [Test]
    public void RoundTrip_MonthListCompactNotation_ExpandsAndParses()
    {
        var natural = "every day in january-march,july,october-december";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should expand and re-compact
        Assert.That(formatted, Is.EqualTo("every day in january-march,july,october-december"));
        Assert.That(spec.Month, Is.TypeOf<MonthSpecifier.List>());
        var monthList = (MonthSpecifier.List)spec.Month;
        Assert.That(monthList.Months, Is.EquivalentTo(new[] { 1, 2, 3, 7, 10, 11, 12 }));
    }

    [Test]
    public void RoundTrip_RangeStepWithMonthAndYear_FormatsCorrectly()
    {
        var natural = "every 2 hours between 9 and 17 of each day in january in year 2025";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        Assert.That(formatted, Is.EqualTo(natural));
        Assert.That(spec.HourStart, Is.EqualTo(9));
        Assert.That(spec.HourEnd, Is.EqualTo(17));
        Assert.That(spec.HourStep, Is.EqualTo(2));
        Assert.That(spec.Month, Is.TypeOf<MonthSpecifier.Single>());
        Assert.That(spec.Year, Is.EqualTo(2025));
    }

    [Test]
    public void RoundTrip_DayOfWeekList_FormatsAndParsesCorrectly()
    {
        var natural = "every monday,wednesday,friday at 9am";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should format as list of days
        Assert.That(formatted, Is.EqualTo(natural));
        // Note: DayOfWeekList might not exist yet - this test will reveal the structure
    }

    [Test]
    public void RoundTrip_DayOfWeekCustomRange_FormatsAndParsesCorrectly()
    {
        var natural = "every tuesday-thursday at 2pm";
        var parseResult = _parser.Parse(natural, new ScheduleParserOptions { TimeZone = DateTimeZone.Utc });
        Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;
        var formatted = _formatter.Format(spec);

        // Should format as range of days
        Assert.That(formatted, Is.EqualTo(natural));
        // Note: DayOfWeekStart/End might not exist yet - this test will reveal the structure
    }
}
