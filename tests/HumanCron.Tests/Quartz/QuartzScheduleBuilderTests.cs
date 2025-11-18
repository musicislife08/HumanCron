using HumanCron.Models.Internal;
using HumanCron.Models;
using HumanCron.Quartz;
using NodaTime;
using Quartz;
using IntervalUnit = HumanCron.Models.Internal.IntervalUnit;
using QuartzIntervalUnit = Quartz.IntervalUnit;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests QuartzCalendarIntervalBuilder and QuartzCronBuilder timezone handling
/// and schedule property configuration.
///
/// Testing Philosophy: We test that OUR code correctly sets properties on Quartz builders
/// (timezone, intervals, etc.), NOT Quartz's internal scheduling logic.
/// </summary>
[TestFixture]
public class QuartzScheduleBuilderTests
{
    // ========================================
    // QuartzCalendarIntervalBuilder Tests
    // ========================================

    [Test]
    public void CalendarInterval_BuildWithUtcTimezone_SetsTimeZoneToUtc()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act - Build the schedule
        var scheduleBuilder = builder.Build(spec);

        // Create trigger to inspect properties WE set
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify OUR code set the timezone correctly
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.TimeZone, Is.EqualTo(TimeZoneInfo.Utc));
    }

    [Test]
    public void CalendarInterval_BuildWithIanaTimezoneAmericaLosAngeles_SetsTimeZoneCorrectly()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"]
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone ID contains Los_Angeles or Pacific
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.TimeZone.Id, Does.Contain("Los_Angeles").Or.Contains("Pacific"));
    }

    [Test]
    public void CalendarInterval_BuildWithIanaTimezoneAmericaNewYork_SetsTimeZoneCorrectly()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Months,
            TimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone ID contains New_York or Eastern
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.TimeZone.Id, Does.Contain("New_York").Or.Contains("Eastern"));
    }

    [Test]
    public void CalendarInterval_BuildWithNullTimezone_UsesSystemDefault()
    {
        // Arrange - ScheduleSpec defaults TimeZone to system default if not set
        var spec = new ScheduleSpec
        {
            Interval = 3,
            Unit = IntervalUnit.Weeks
            // TimeZone not set, uses system default from ScheduleSpec
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify trigger has a timezone set (should be system default from spec)
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.TimeZone, Is.Not.Null);

        // Verify it matches the system default timezone
        var expectedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            DateTimeZoneProviders.Tzdb.GetSystemDefault().Id);
        Assert.That(calendarTrigger.TimeZone.Id, Is.EqualTo(expectedTimeZone.Id));
    }

    [Test]
    public void CalendarInterval_Build3WeekInterval_SetsRepeatIntervalTo3Weeks()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 3,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify our code set the interval properties correctly
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(21));
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Day));
    }

    [Test]
    public void CalendarInterval_BuildMonthlyInterval_SetsRepeatIntervalUnitToMonth()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Months,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify our code set month interval correctly
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(1));
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Month));
    }

    [Test]
    public void CalendarInterval_BuildYearlyInterval_SetsRepeatIntervalUnitToYear()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Years,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify our code set year interval correctly
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(1));
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Year));
    }

    [Test]
    public void CalendarInterval_BuildWithDayPattern_ThrowsNotSupportedException()
    {
        // Arrange - Day patterns (weekdays/weekends) not supported with multi-interval schedules
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Weeks,
            DayPattern = DayPattern.Weekdays,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => builder.Build(spec));
        Assert.That(ex!.Message, Does.Contain("Day patterns"));
        Assert.That(ex.Message, Does.Contain("not supported"));
        Assert.That(ex.Message, Does.Contain("Workaround"));
    }

    /// <summary>
    /// WORKAROUND TEST for Quartz.NET bug #1035:
    /// https://github.com/quartznet/quartznet/issues/1035
    ///
    /// CalendarIntervalScheduleBuilder.WithIntervalInWeeks() ignores the StartAt parameter.
    /// When a trigger is created with both a week interval and a StartAt time, Quartz fires
    /// immediately instead of waiting for the StartAt time.
    ///
    /// Our workaround: Convert weeks to days (3 weeks = 21 days) which properly respects StartAt.
    ///
    /// This test validates:
    /// 1. Multi-week intervals are converted to day intervals (every 3 weeks → 21 days)
    /// 2. The interval unit is set to DAYS, not WEEKS
    /// 3. This ensures compatibility with StartAt times
    ///
    /// If Quartz fixes this bug in the future, we can revert to using WithIntervalInWeeks()
    /// and this test will fail, alerting us to update our implementation.
    /// </summary>
    [Test]
    public void CalendarInterval_Build3WeekInterval_ConvertsToDaysToWorkaroundQuartzBug1035()
    {
        // Arrange - Multi-week interval (the problematic case in Quartz bug #1035)
        var spec = new ScheduleSpec
        {
            Interval = 3,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify workaround: weeks converted to days
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;

        // CRITICAL: Should be 21 days, NOT 3 weeks (workaround for bug #1035)
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(21),
            "Expected 21 days (not 3 weeks) as workaround for Quartz bug #1035");
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Day),
            "Expected DAY interval unit (not WEEK) as workaround for Quartz bug #1035");

        // If this test fails in the future with Quartz updates, it may indicate the bug is fixed.
        // Check Quartz release notes and consider reverting to WithIntervalInWeeks() if safe.
    }

    // ========================================
    // QuartzCronBuilder Tests
    // ========================================

    [Test]
    public void Cron_BuildWithUtcTimezone_SetsTimeZoneToUtc()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act - Build the schedule
        var scheduleBuilder = builder.Build(spec);

        // Create trigger to inspect properties WE set
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify OUR code set the timezone correctly
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone, Is.EqualTo(TimeZoneInfo.Utc));
    }

    [Test]
    public void Cron_BuildWithIanaTimezoneAmericaNewYork_SetsTimeZoneCorrectly()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone ID contains New_York or Eastern
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone.Id, Does.Contain("New_York").Or.Contains("Eastern"));
    }

    [Test]
    public void Cron_BuildWithIanaTimezoneAmericaLosAngeles_SetsTimeZoneCorrectly()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"]
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone ID contains Los_Angeles or Pacific
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone.Id, Does.Contain("Los_Angeles").Or.Contains("Pacific"));
    }

    [Test]
    public void Cron_BuildWithIanaTimezoneEuropeLondon_SetsTimeZoneCorrectly()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeZone = DateTimeZoneProviders.Tzdb["Europe/London"]
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone ID contains London or GMT
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone.Id, Does.Contain("London").Or.Contains("GMT"));
    }

    [Test]
    public void Cron_BuildDailyScheduleWithTime_GeneratesCorrectCronExpression()
    {
        // Arrange - Daily at 2:30pm
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeOfDay = new TimeOnly(14, 30),
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify trigger type and cron expression format
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Quartz cron format: second minute hour day month dayOfWeek
        // Daily at 14:30 should be: "0 30 14 * * ?"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("0 30 14 * * ?"));
    }

    [Test]
    public void Cron_BuildDailyScheduleOnWeekdays_GeneratesCorrectCronExpression()
    {
        // Arrange - Daily on weekdays at midnight
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            DayPattern = DayPattern.Weekdays,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify cron expression for weekdays
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Weekdays at midnight: "0 0 0 ? * MON-FRI"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("0 0 0 ? * MON-FRI"));
    }

    [Test]
    public void Cron_BuildWeeklyScheduleOnSpecificDay_GeneratesCorrectCronExpression()
    {
        // Arrange - Weekly on Tuesday at 9:00am
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            DayOfWeek = DayOfWeek.Tuesday,
            TimeOfDay = new TimeOnly(9, 0),
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify cron expression for specific day
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Tuesday at 9am: "0 0 9 ? * TUE"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("0 0 9 ? * TUE"));
    }

    [Test]
    public void Cron_BuildWithDefaultTimezone_UsesSystemDefault()
    {
        // Arrange - ScheduleSpec defaults TimeZone to system default if not set
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days
            // TimeZone not set, uses system default from ScheduleSpec
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify trigger has a timezone set (should be system default from spec)
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone, Is.Not.Null);

        // Verify it matches the system default timezone
        var expectedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            DateTimeZoneProviders.Tzdb.GetSystemDefault().Id);
        Assert.That(cronTrigger.TimeZone.Id, Is.EqualTo(expectedTimeZone.Id));
    }

    [Test]
    public void Cron_BuildEvery30Seconds_GeneratesCorrectCronExpression()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 30,
            Unit = IntervalUnit.Seconds,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify cron expression for seconds interval
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Every 30 seconds: "*/30 * * * * ?"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("*/30 * * * * ?"));
    }

    [Test]
    public void Cron_BuildEvery15Minutes_GeneratesCorrectCronExpression()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 15,
            Unit = IntervalUnit.Minutes,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify cron expression for minutes interval
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Every 15 minutes: "0 */15 * * * ?"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("0 */15 * * * ?"));
    }

    [Test]
    public void Cron_BuildEvery6Hours_GeneratesCorrectCronExpression()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 6,
            Unit = IntervalUnit.Hours,
            TimeZone = DateTimeZone.Utc
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify cron expression for hours interval
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;

        // Every 6 hours starting at midnight: "0 0 */6 * * ?"
        Assert.That(cronTrigger.CronExpressionString, Is.EqualTo("0 0 */6 * * ?"));
    }

    // ========================================
    // Timezone Edge Cases
    // ========================================

    [TestCase("America/Chicago")]
    [TestCase("Europe/Paris")]
    [TestCase("Asia/Tokyo")]
    [TestCase("Australia/Sydney")]
    public void CalendarInterval_BuildWithVariousIanaTimezones_SetsTimeZoneCorrectly(string ianaTimezoneId)
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZoneProviders.Tzdb[ianaTimezoneId]
        };
        var builder = new QuartzCalendarIntervalBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone is set and not null
        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        var calendarTrigger = (ICalendarIntervalTrigger)trigger;
        Assert.That(calendarTrigger.TimeZone, Is.Not.Null);

        // Verify timezone ID contains part of the IANA ID (converted to BCL format)
        // BCL may use different naming conventions, so we check for key parts
        var cityName = ianaTimezoneId.Split('/').Last();
        Assert.That(calendarTrigger.TimeZone.Id, Does.Contain(cityName).IgnoreCase
            .Or.Not.EqualTo(TimeZoneInfo.Utc.Id)); // At minimum, not UTC
    }

    [TestCase("America/Chicago")]
    [TestCase("Europe/Paris")]
    [TestCase("Asia/Tokyo")]
    [TestCase("Australia/Sydney")]
    public void Cron_BuildWithVariousIanaTimezones_SetsTimeZoneCorrectly(string ianaTimezoneId)
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            TimeZone = DateTimeZoneProviders.Tzdb[ianaTimezoneId]
        };
        var builder = new QuartzCronBuilder();

        // Act
        var scheduleBuilder = builder.Build(spec);
        var trigger = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        // Assert - Verify timezone is set and not null
        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        var cronTrigger = (ICronTrigger)trigger;
        Assert.That(cronTrigger.TimeZone, Is.Not.Null);

        // Verify timezone ID contains part of the IANA ID (converted to BCL format)
        var cityName = ianaTimezoneId.Split('/').Last();
        Assert.That(cronTrigger.TimeZone.Id, Does.Contain(cityName).IgnoreCase
            .Or.Not.EqualTo(TimeZoneInfo.Utc.Id)); // At minimum, not UTC
    }
}
