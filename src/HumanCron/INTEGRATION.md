# NaturalCron Integration Guide

## Overview

NaturalCron is a natural language schedule parser with Quartz.NET integration. It converts human-readable schedule descriptions like "every 2 weeks on sunday at 2pm" into Quartz triggers.

## Features

✅ **Natural Language Parsing**: "every 30 seconds", "every 15 minutes", "every 6 hours", "every day", "every week", "every 2 weeks", "every month", "every year"
✅ **Time Specifications**: "at 2pm", "at 14:30", "at 9:30am"
✅ **Day-of-Week**: "every monday", "every sunday at 2pm"
✅ **Day Patterns**: "every weekday", "every weekend"
✅ **Day-of-Month**: "every month on 15" (with monthly/yearly intervals)
✅ **Multi-Week with Day-of-Week**: "every 2 weeks on sunday" (calculates aligned start time)
✅ **Monthly with Day-of-Month**: "every 3 months on 15 at 2pm"
✅ **Timezone Support**: All schedules respect timezone configuration
✅ **Quartz.NET Integration**: Converts to CronSchedule or CalendarIntervalSchedule
✅ **Start Time Calculation**: Automatically aligns multi-interval schedules with constraints

## Installation

### 1. Add Project Reference

```bash
dotnet add reference path/to/NaturalCron/NaturalCron.csproj
```

### 2. Register Services

```csharp
using NaturalCron;

// In Program.cs or Startup.cs
services.AddNaturalCron(); // Auto-discovers Quartz extension if installed
```

This registers:
- `IScheduleParser` → Parses natural language to ScheduleSpec
- `IQuartzScheduleBuilder` → Converts ScheduleSpec to Quartz schedules

## Basic Usage

### Dependency Injection

```csharp
using NaturalCron.Abstractions;
using NaturalCron.Models;
using NaturalCron.Parsing;
using NaturalCron.Quartz;
using Quartz;

public class MyBackgroundService
{
    private readonly IScheduleParser _parser;
    private readonly IQuartzScheduleBuilder _quartzBuilder;

    public MyBackgroundService(
        IScheduleParser parser,
        IQuartzScheduleBuilder quartzBuilder)
    {
        _parser = parser;
        _quartzBuilder = quartzBuilder;
    }

    public ITrigger CreateTrigger(string naturalLanguage)
    {
        // 1. Parse natural language
        var parseResult = _parser.Parse(naturalLanguage, new ScheduleParserOptions
        {
            TimeZone = TimeZoneInfo.Utc
        });

        // 2. Handle parse result
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            throw new ArgumentException($"Invalid schedule: {error.Message}");
        }

        // 3. Build Quartz schedule
        var scheduleBuilder = _quartzBuilder.Build(success.Value);

        // 4. Calculate start time (for multi-interval schedules with constraints)
        var startTime = _quartzBuilder.CalculateStartTime(success.Value);

        // 5. Create trigger
        return TriggerBuilder.Create()
            .WithIdentity("my-trigger")
            .WithSchedule(scheduleBuilder)
            .StartAt(startTime ?? DateTimeOffset.UtcNow)
            .Build();
    }
}
```

## Pattern Support

### Simple Intervals (CronSchedule)

| Pattern | Description | Quartz Type |
|---------|-------------|-------------|
| `every 30 seconds` | Every 30 seconds | CronSchedule |
| `every 15 minutes` | Every 15 minutes | CronSchedule |
| `every 6 hours` | Every 6 hours | CronSchedule |
| `every day` | Every day at midnight | CronSchedule |
| `every week` | Every week (Sunday at midnight) | CronSchedule |

### Time-of-Day (CronSchedule)

| Pattern | Description | Quartz Type |
|---------|-------------|-------------|
| `every day at 2pm` | Daily at 2:00 PM | CronSchedule |
| `every day at 14:30` | Daily at 2:30 PM | CronSchedule |
| `every day at 9:30am` | Daily at 9:30 AM | CronSchedule |

### Day-of-Week (CronSchedule)

| Pattern | Description | Quartz Type |
|---------|-------------|-------------|
| `every sunday` | Weekly on Sunday at midnight | CronSchedule |
| `every monday at 9am` | Weekly on Monday at 9 AM | CronSchedule |
| `every weekday` | Daily on weekdays only | CronSchedule |
| `every weekend` | Daily on weekends only | CronSchedule |

### Multi-Week with Day-of-Week (CalendarInterval + StartAt)

| Pattern | Description | Quartz Type | Start Time |
|---------|-------------|-------------|------------|
| `every 2 weeks on sunday` | Every 2 weeks on Sunday | CalendarInterval | Next Sunday |
| `every 2 weeks on sunday at 2pm` | Every 2 weeks on Sunday at 2pm | CalendarInterval | Next Sunday 2pm |
| `every 3 weeks on monday` | Every 3 weeks on Monday | CalendarInterval | Next Monday |

**How it works**: The `CalculateStartTime()` method finds the next occurrence of the target day/time and sets it as the trigger's start time. CalendarIntervalSchedule then repeats every N weeks from that aligned start point.

### Monthly/Yearly with Day-of-Month (CalendarInterval + StartAt)

| Pattern | Description | Quartz Type | Start Time |
|---------|-------------|-------------|------------|
| `every month on 15` | Monthly on the 15th | CalendarInterval | Next 15th |
| `every 3 months on 15 at 2pm` | Quarterly on the 15th at 2pm | CalendarInterval | Next 15th 2pm |
| `every year on 1` | Yearly on the 1st | CalendarInterval | Next 1st |

**Edge case handling**: For day 31 in February, it automatically uses day 28 (or 29 in leap years).

### Unsupported Patterns

❌ **Multi-interval with day patterns**: "every 2 weeks on weekdays", "every 3 months on weekends"
**Reason**: CalendarIntervalSchedule cannot skip specific days within an interval
**Workaround**: Use daily intervals: "every weekday"

## Advanced Patterns

### Pattern Matching on Results

```csharp
var result = _parser.Parse("every 2 weeks on sunday at 2pm", options);

var trigger = result switch
{
    ParseResult<ScheduleSpec>.Success(var spec) => CreateTrigger(spec),
    ParseResult<ScheduleSpec>.Error(var message) => throw new ArgumentException(message),
    _ => throw new InvalidOperationException("Unexpected parse result type")
};
```

### Custom Timezone

```csharp
var options = new ScheduleParserOptions
{
    TimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]
};

var result = _parser.Parse("every day at 9am", options);
// Schedule fires at 9am Eastern Time, converted to server timezone
```

**Important**: NaturalCron uses **NodaTime** for timezone handling, not BCL `TimeZoneInfo`. Use `DateTimeZoneProviders.Tzdb[id]` to get timezone instances.

### Timezone Conversion Examples

#### Same Timezone (No Conversion)

```csharp
// User and server both in UTC
var options = new ScheduleParserOptions
{
    TimeZone = DateTimeZone.Utc
};

var converter = UnixCronConverter.Create();
var result = converter.ToCron("every day at 2pm", options);
// Output: "0 14 * * *" (2pm, no conversion)
```

#### Cross-Timezone Conversion

```csharp
// User in Pacific (UTC-8 winter), server in UTC
var userOptions = new ScheduleParserOptions
{
    TimeZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"]
};

var converter = UnixCronConverter.Create(); // Server timezone from system
var result = converter.ToCron("every day at 2pm", userOptions);
// Output: "0 22 * * *" (2pm Pacific = 22:00 UTC)
```

**How it works:**
1. User specifies "2pm" in their timezone (Pacific)
2. Converter calculates offset: Pacific (UTC-8) → UTC = +8 hours
3. Cron expression generated: 14:00 + 8 = 22:00 UTC
4. Job executes at 22:00 UTC, which is 2pm Pacific ✅

#### Minutes Preserved During Conversion

```csharp
// User in Eastern (UTC-5), server in UTC
var options = new ScheduleParserOptions
{
    TimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]
};

var result = converter.ToCron("every day at 2:30pm", options);
// Output: "30 19 * * *" (2:30pm Eastern = 19:30 UTC)
// Minutes (:30) are preserved across timezone boundaries
```

### DST (Daylight Saving Time) Caveats

**⚠️ Critical Limitation**: Unix cron expressions are **static** and cannot adapt to DST changes.

#### The Problem

When you generate a schedule with a timezone that observes DST:

```csharp
// Generated on March 8, 2025 (before spring forward)
// User in Pacific (PST = UTC-8), server in UTC
var options = new ScheduleParserOptions
{
    TimeZone = DateTimeZoneProviders.Tzdb["America/Los_Angeles"]
};

var result = converter.ToCron("every day at 2pm", options);
// Generates: "0 22 * * *" (2pm PST = 22:00 UTC, using UTC-8 offset)

// ✅ March 8: Runs at 22:00 UTC = 2pm PST (correct)
// ❌ March 10+: Still runs at 22:00 UTC = 3pm PDT (wrong!)
//              Should be 21:00 UTC for 2pm PDT (UTC-7)
```

**What happens:**
- March 9, 2:00am: Pacific switches from PST (UTC-8) to PDT (UTC-7)
- Cron expression remains `0 22 * * *` (old offset)
- Job now runs 1 hour late (3pm instead of 2pm)

#### Workarounds

**Option 1: Use UTC for scheduling** (Recommended)

```csharp
// Schedule in UTC - no DST issues
var options = new ScheduleParserOptions
{
    TimeZone = DateTimeZone.Utc
};

var result = converter.ToCron("1d at 14:00", options); // 2pm UTC
// Always runs at 14:00 UTC, regardless of DST
```

**Option 2: Regenerate schedules after DST**

```csharp
// Regenerate jobs twice per year (spring forward, fall back)
public async Task RegenerateSchedulesForDSTAsync()
{
    var jobs = await GetAllScheduledJobsAsync();

    foreach (var job in jobs)
    {
        // Re-parse with current date
        var result = _parser.Parse(job.NaturalLanguage, job.Options);
        var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;

        // Rebuild with current timezone offset
        var newSchedule = _quartzBuilder.Build(spec);

        // Reschedule job
        await _scheduler.RescheduleJob(job.TriggerKey, newTrigger);
    }
}
```

**Option 3: Use CalendarIntervalSchedule** (for supported patterns)

```csharp
// Multi-week, monthly, yearly patterns use CalendarIntervalSchedule
// which IS DST-aware and adjusts automatically
var result = _parser.Parse("every 2 weeks on sunday at 2pm", options);
// Uses CalendarIntervalSchedule - respects DST changes ✅
```

**CalendarIntervalSchedule patterns** (DST-aware):
- Multi-week: `every 2 weeks`, `every 3 weeks`, `every 4 weeks`
- Monthly: `every month`, `every 3 months`, `every 6 months`
- Yearly: `every year`

**CronSchedule patterns** (DST-unaware):
- Sub-day: `every 30 seconds`, `every 15 minutes`, `every 6 hours`
- Daily: `every day`, `every day at 2pm`
- Weekly: `every week`, `every monday`

#### DST Transition Dates (2025)

**Spring Forward** (Clocks move ahead 1 hour):
- **US/Canada**: March 9, 2025, 2:00 AM → 3:00 AM
- **Europe**: March 30, 2025, 1:00 AM → 2:00 AM

**Fall Back** (Clocks move back 1 hour):
- **US/Canada**: November 2, 2025, 2:00 AM → 1:00 AM
- **Europe**: October 26, 2025, 2:00 AM → 1:00 AM

#### Testing DST Scenarios

```csharp
// Test generation before DST
var beforeDst = new FakeClock(Instant.FromUtc(2025, 3, 8, 10, 0));
var converter1 = new UnixCronConverter(parser, formatter, beforeDst, DateTimeZone.Utc);

var result1 = converter1.ToCron("every day at 2pm", pacificOptions);
// Output: "0 22 * * *" (PST offset: UTC-8)

// Test generation after DST
var afterDst = new FakeClock(Instant.FromUtc(2025, 3, 10, 10, 0));
var converter2 = new UnixCronConverter(parser, formatter, afterDst, DateTimeZone.Utc);

var result2 = converter2.ToCron("every day at 2pm", pacificOptions);
// Output: "0 21 * * *" (PDT offset: UTC-7) ✅ Correct offset
```

### Manual Start Time Calculation

```csharp
var spec = /* ... parsed spec ... */;

// Calculate start time relative to a specific reference time
var referenceTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);

if (startTime.HasValue)
{
    Console.WriteLine($"First execution will be: {startTime.Value:yyyy-MM-dd HH:mm}");
}
```

## Integration Patterns

### Quartz.NET Job Registration

```csharp
services.AddQuartz(q =>
{
    // Add your jobs
    q.AddJob<MyJob>(opts => opts.WithIdentity("my-job"));

    // Use NaturalCron to create trigger
    q.AddTrigger(opts =>
    {
        var parser = serviceProvider.GetRequiredService<IScheduleParser>();
        var quartzBuilder = serviceProvider.GetRequiredService<IQuartzScheduleBuilder>();

        var parseResult = parser.Parse("every 2 weeks on sunday at 2pm", new ScheduleParserOptions());
        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        var schedule = quartzBuilder.Build(spec);
        var startTime = quartzBuilder.CalculateStartTime(spec);

        opts.ForJob("my-job")
            .WithIdentity("my-trigger")
            .WithSchedule(schedule)
            .StartAt(startTime ?? DateTimeOffset.UtcNow);
    });
});
```

### User-Configurable Schedules

```csharp
public class JobConfiguration
{
    public string JobName { get; set; }
    public string Schedule { get; set; } // Natural language: "every 2 weeks on sunday"
}

public class DynamicJobScheduler
{
    private readonly IScheduleParser _parser;
    private readonly IQuartzScheduleBuilder _quartzBuilder;
    private readonly IScheduler _scheduler;

    public async Task ScheduleJobAsync(JobConfiguration config)
    {
        // Parse user's natural language schedule
        var parseResult = _parser.Parse(config.Schedule, new ScheduleParserOptions());

        if (parseResult is ParseResult<ScheduleSpec>.Error error)
        {
            throw new ArgumentException($"Invalid schedule '{config.Schedule}': {error.Message}");
        }

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Build Quartz trigger
        var schedule = _quartzBuilder.Build(spec);
        var startTime = _quartzBuilder.CalculateStartTime(spec);

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{config.JobName}-trigger")
            .ForJob(config.JobName)
            .WithSchedule(schedule)
            .StartAt(startTime ?? DateTimeOffset.UtcNow)
            .Build();

        await _scheduler.ScheduleJob(trigger);
    }
}
```

## Error Handling

```csharp
var result = _parser.Parse(userInput, options);

if (result is ParseResult<ScheduleSpec>.Error error)
{
    // Error messages are user-friendly and explain what's wrong
    Console.WriteLine($"Parse error: {error.Message}");

    // Examples of error messages:
    // - "Invalid interval number: abc (expected a positive integer)"
    // - "Invalid hour for 12-hour format: 13 (must be 1-12 with am/pm)"
    // - "Day-of-month (on 15) is only valid with monthly (M) or yearly (y) intervals"
    return;
}

try
{
    var spec = ((ParseResult<ScheduleSpec>.Success)result).Value;
    var schedule = _quartzBuilder.Build(spec);
}
catch (NotSupportedException ex)
{
    // Thrown for patterns that Quartz cannot express
    // Example: "2w on weekdays" - cannot skip days within interval
    Console.WriteLine($"Unsupported pattern: {ex.Message}");
}
```

## Testing Integration

```csharp
[Test]
public void Integration_UserEntersSchedule_CreatesValidTrigger()
{
    // Arrange
    var parser = new NaturalLanguageParser();
    var quartzBuilder = new QuartzScheduleBuilder();
    var userInput = "every 2 weeks on sunday at 2pm";

    // Act - Parse
    var parseResult = parser.Parse(userInput, new ScheduleParserOptions());
    Assert.That(parseResult, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());

    var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

    // Act - Build trigger
    var schedule = quartzBuilder.Build(spec);
    var startTime = quartzBuilder.CalculateStartTime(spec);

    var trigger = TriggerBuilder.Create()
        .WithSchedule(schedule)
        .StartAt(startTime ?? DateTimeOffset.UtcNow)
        .Build();

    // Assert - Verify execution times
    var firstFire = trigger.GetFireTimeAfter(DateTimeOffset.UtcNow);
    var secondFire = trigger.GetFireTimeAfter(firstFire!.Value);

    Assert.That(firstFire!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
    Assert.That(secondFire!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
    Assert.That((secondFire.Value - firstFire.Value).Days, Is.EqualTo(14));
}
```

## API Reference

### IScheduleParser

```csharp
public interface IScheduleParser
{
    ParseResult<ScheduleSpec> Parse(string naturalLanguage, ScheduleParserOptions options);
}
```

### IQuartzScheduleBuilder

```csharp
public interface IQuartzScheduleBuilder
{
    IScheduleBuilder Build(ScheduleSpec spec);
    DateTimeOffset? CalculateStartTime(ScheduleSpec spec, DateTimeOffset? referenceTime = null);
}
```

### ScheduleParserOptions

```csharp
public sealed class ScheduleParserOptions
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}
```

### ParseResult<T>

```csharp
public abstract record ParseResult<T>
{
    public sealed record Success(T Value) : ParseResult<T>;
    public sealed record Error(string Message) : ParseResult<T>;
}
```

## Integration Ideas

1. **Background Job Scheduling**: Replace string cron expressions with natural language
2. **UI for Schedule Input**: Allow users to enter "every 2 weeks on sunday at 2pm" instead of cron syntax
3. **Validation API**: API endpoint to validate natural language schedules before saving
4. **Schedule Preview**: Show users when their job will next execute (using `CalculateStartTime`)
5. **Migration**: Convert existing cron strings to natural language equivalents

## Production Ready

✅ Comprehensive test coverage including edge cases, DST handling, and timezone conversions
✅ DI registration with auto-discovery
✅ Full Quartz.NET integration
✅ Multi-week with day-of-week support
✅ Timezone-aware calculations
✅ Comprehensive error messages
✅ Zero-allocation Span<T> parsing for optimal performance

The library is battle-tested and ready for production use!
