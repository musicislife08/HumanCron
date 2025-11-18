# HumanCron

[![Build Status](https://github.com/musicislife08/HumanCron/workflows/Build%20and%20Test/badge.svg)](https://github.com/musicislife08/HumanCron/actions)
[![NuGet](https://img.shields.io/nuget/v/HumanCron.svg)](https://www.nuget.org/packages/HumanCron)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Human-readable cron expression converter with bidirectional support and timezone awareness for .NET. Parse schedules like `"every 30 minutes"`, `"every day at 2pm"`, or `"every monday at 9am"` into Unix cron expressions or Quartz.NET schedules, and convert cron expressions back to natural language.

## Features

- 🗣️ **Natural Language Parsing** - Human-friendly syntax instead of cryptic cron expressions
- ⏰ **Timezone Aware** - Proper DST handling using NodaTime
- 🔄 **Bidirectional** - Convert to/from cron expressions
- 🔌 **Quartz.NET Integration** - Direct IScheduleBuilder conversion
- 📅 **Month Support** - Select specific months, ranges, or lists
- ✅ **Well Tested** - Comprehensive test coverage including edge cases, DST handling, and timezone conversions
- ⚡ **High Performance** - Zero-allocation Span<T> parsing for minimal memory overhead
- 📦 **Dependency Injection** - First-class DI support

## Installation

```bash
# Core library (Unix cron support)
dotnet add package HumanCron

# Quartz.NET integration (optional)
dotnet add package HumanCron.Quartz
```

## Quick Start

### Basic Unix Cron Conversion

```csharp
using HumanCron.Converters.Unix;

var converter = UnixCronConverter.Create();

// Convert to cron
var result = converter.ToUnixCron("every 30 minutes");
// Returns: "*/30 * * * *"

result = converter.ToUnixCron("every day at 2pm");
// Returns: "0 14 * * *"

result = converter.ToUnixCron("every monday at 9am");
// Returns: "0 9 * * 1"

result = converter.ToUnixCron("every day in january at 9am");
// Returns: "0 9 * 1 *"

result = converter.ToUnixCron("every weekday in january,april,july,october at 9am");
// Returns: "0 9 * 1,4,7,10 1-5"

// Convert back to natural language
var reverseResult = converter.ToNaturalLanguage("0 14 * * *");
// Returns: "every day at 2pm"
```

### Dependency Injection

```csharp
using HumanCron;

// In Program.cs or Startup.cs
builder.Services.AddHumanCron();

// In your service
public class MySchedulingService
{
    private readonly INaturalCronConverter _converter;

    public MySchedulingService(INaturalCronConverter converter)
    {
        _converter = converter;
    }

    public void ScheduleJob(string schedule)
    {
        var cronResult = _converter.ToUnixCron(schedule);
        if (cronResult is ParseResult<string>.Success success)
        {
            // Use success.Value cron expression
        }
    }
}
```

### Quartz.NET Integration

```csharp
using HumanCron.Quartz;

var converter = QuartzScheduleConverterFactory.Create();

// Convert to Quartz IScheduleBuilder
var result = converter.ToQuartzSchedule("every day at 2pm");
if (result is ParseResult<IScheduleBuilder>.Success success)
{
    var trigger = TriggerBuilder.Create()
        .WithSchedule(success.Value)
        .Build();
}

// Multi-week patterns use CalendarIntervalScheduleBuilder
var triggerResult = converter.CreateTriggerBuilder("every 3 weeks on sunday at 12am");
if (triggerResult is ParseResult<TriggerBuilder>.Success triggerSuccess)
{
    var trigger = triggerSuccess.Value
        .WithIdentity("my-trigger")
        .ForJob("my-job")
        .Build();
}
```

## Supported Syntax

### Basic Intervals

All patterns must start with `"every"`:

- **Seconds**: `every 30 seconds`, `every 45 seconds`
- **Minutes**: `every 15 minutes`, `every 30 minutes`, `every 45 minutes`
- **Hours**: `every hour`, `every 6 hours`, `every 12 hours`
- **Days**: `every day`, `every 7 days`
- **Weeks**: `every week`, `every 2 weeks`, `every 3 weeks`
- **Months**: `every month`, `every 3 months`, `every 6 months`
- **Years**: `every year`, `every 2 years`

### Time of Day

- `at 2pm`, `at 14:30`, `at 9am`, `at 3:30am`

### Day Constraints

- **Specific Day**: `every monday`, `every friday`, `on tuesday`
- **Day Patterns**: `every weekday`, `on weekends`
- **Day Ranges**: `between monday and friday`, `between saturday and sunday`

### Month Selection

- **Specific Month**: `in january`, `in december`
- **Month Ranges**: `between january and march`, `between october and december`
- **Month Lists**: `in january,april,july,october` (quarterly)

### Abbreviations

Both full names and abbreviations are accepted on input (output always uses full names):

- **Days**: `mon`, `tue`, `wed`, `thu`, `fri`, `sat`, `sun`
- **Months**: `jan`, `feb`, `mar`, `apr`, `may`, `jun`, `jul`, `aug`, `sep`, `oct`, `nov`, `dec`

### Examples

```
every 30 minutes                         → */30 * * * *
every hour                               → 0 * * * *
every day at 2pm                         → 0 14 * * *
every day at 14:30                       → 30 14 * * *
every monday                             → 0 0 * * 1
every monday at 9am                      → 0 9 * * 1
every 2 weeks on friday at 5pm           → (CalendarInterval schedule)
every weekday at 9am                     → 0 9 * * 1-5
every day in january                     → 0 0 * 1 *
every day in january at 9am              → 0 9 * 1 *
every monday in january                  → 0 0 * 1 1
every day between january and march      → 0 0 * 1-3 *
every day in january,april,july,october  → 0 0 * 1,4,7,10 *
every weekday in january at 9am          → 0 9 * 1 1-5
every month on 15                        → 0 0 15 * *
every month on 15 in january,april       → 0 0 15 1,4 *
every year                               → 0 0 1 1 *
```

## Documentation

- **[Architecture](src/HumanCron/ARCHITECTURE.md)** - Design philosophy and implementation details
- **[DSL Specification](src/HumanCron/DSL-SPECIFICATION.md)** - Complete grammar and validation rules
- **[Integration Guide](src/HumanCron/INTEGRATION.md)** - Usage patterns, timezone handling, testing

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
