# HumanCron

[![Build Status](https://github.com/musicislife08/HumanCron/workflows/Build%20and%20Test/badge.svg)](https://github.com/musicislife08/HumanCron/actions)
[![NuGet](https://img.shields.io/nuget/v/HumanCron.svg)](https://www.nuget.org/packages/HumanCron)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Human-readable cron expression converter with bidirectional support and timezone awareness for .NET. Parse schedules like `"every 30 minutes"`, `"every day at 2pm"`, or `"every monday at 9am"` into Unix cron expressions or Quartz.NET schedules, and convert cron expressions back to natural language.

## Features

- 🗣️ **Natural Language Parsing** - Human-friendly syntax instead of cryptic cron expressions
- ⏰ **Timezone Aware** - Proper DST handling using NodaTime
- 🔄 **Bidirectional** - Convert to/from cron expressions with full specification compliance
- 🔌 **Quartz.NET Integration** - Direct IScheduleBuilder conversion
- 📅 **Month Support** - Select specific months, ranges, or lists
- 🎯 **Full Cron Spec Support** - Complete Unix (5-field) and Quartz (6-7 field) cron syntax including lists, ranges, steps, named values, and Quartz-specific features (L, W, #)
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
    private readonly IHumanCronConverter _converter;

    public MySchedulingService(IHumanCronConverter converter)
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

### Advanced Features (Quartz Only)

- **Day of Month**: `on the 15th`, `on the 1st and 15th`, `between the 1st and 15th`
- **Combined Month + Day**: `on january 1st`, `on december 25th`, `on april 15th` (more natural than `on the 15th in january`)
- **Last Day**: `on last day`, `on day before last`, `on 3rd to last day`
- **Last Day of Week**: `on last friday`, `on last monday`
- **Nth Occurrence**: `on 3rd friday`, `on 2nd tuesday` (1st-5th)
- **Weekday Nearest**: `on weekday nearest the 15th`, `on last weekday`
- **Hour/Minute Lists**: `at hours 9,12,15,18`, `at minutes 0,15,30,45`
- **Hour/Minute Ranges**: `between hours 9 and 17`, `between minutes 0 and 30`
- **Range with Step**: `every 5 minutes between 0 and 30 of each hour`
- **Year Constraints**: `in year 2025`, `in year 2024`

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
every month on the 15th                  → 0 0 15 * *
every month on january 15th              → 0 0 15 1 *
every month on january 1st at 1am        → 0 1 1 1 *
every month on the 15th in january,april → 0 0 15 1,4 *
every year                               → 0 0 1 1 *

# Advanced Quartz features (require Quartz converter)
every hour at minutes 0,15,30,45         → 0 0,15,30,45 * * * ?
every day at hours 9,12,15,18            → 0 0 9,12,15,18 * * ?
every 5 minutes between 0 and 30 of each hour → 0 0-30/5 * * * ?
every month on last day                  → 0 0 L * ?
every month on 3rd friday                → 0 0 ? * 6#3
every month on last friday               → 0 0 ? * 6L
every month on weekday nearest the 15th  → 0 0 15W * ?
every day at 12pm in year 2025           → 0 0 12 * * ? 2025
```

## Cron Expression Support

HumanCron provides **full specification compliance** for both Unix and Quartz cron formats with bidirectional conversion.

### Unix Cron (5-field format)

**Format**: `minute hour day-of-month month day-of-week`

**Supported Syntax**:
- **Wildcards**: `*` (any value)
- **Specific Values**: `5`, `15`, `MON`, `JAN`
- **Lists**: `0,15,30,45` or `MON,WED,FRI` or `JAN,APR,JUL,OCT`
- **Ranges**: `1-5`, `9-17`, `JAN-MAR`, `MON-FRI`
- **Steps**: `*/15`, `0-30/5`, `9-17/2`
- **Mixed Syntax**: `0-4,8-12,20` (combines ranges and individual values)
- **Named Values**: Case-insensitive month (`JAN`-`DEC`) and day (`SUN`-`SAT`) names

**Smart Compaction**: When building cron expressions, consecutive values are automatically compacted:
- `[1,2,3,5,7,8,9,10,11,12]` → `1-3,5,7-12` (3+ consecutive values become ranges)

**Examples**:
```
*/30 * * * *        → every 30 minutes
0 9-17 * * *        → every hour between 9am and 5pm
0 9,12,15,18 * * *  → at 9am, 12pm, 3pm, and 6pm
0 9-17/2 * * *      → every 2 hours between 9am and 5pm
0 9 * * MON-FRI     → every weekday at 9am
0 9 * JAN,APR * MON → every monday in january and april at 9am
0 0 1,15 * *        → on the 1st and 15th of each month
0 0 1-15/3 * *      → every 3 days between 1st and 15th
```

### Quartz Cron (6-7 field format)

**Format**: `second minute hour day month day-of-week [year]`

Supports **all Unix cron features** plus Quartz-specific advanced features:

**Additional Quartz Features**:
- **`?` (no specific value)**: Required when specifying day-of-week or day-of-month
- **`L` (last)**:
  - Day field: `L` = last day of month
  - Day-of-week field: `6L` = last Friday of month
- **`W` (weekday)**:
  - `15W` = weekday nearest to the 15th
  - `LW` = last weekday of month
- **`#` (nth occurrence)**: `6#3` = 3rd Friday of month (1-5 valid)
- **`L-N` (offset from last)**: `L-3` = 3rd to last day of month
- **Year field**: Optional 7th field (1970-2099): `0 0 12 * * ? 2025`

**Examples**:
```
0 */30 * * * ?           → every 30 minutes
0 0 14 * * ?             → every day at 2pm
0 0 14 ? * MON           → every monday at 2pm
0 0 9 ? * MON-FRI        → every weekday at 9am
0 0 9 L * ?              → last day of month at 9am
0 0 9 L-3 * ?            → 3rd to last day of month at 9am
0 0 9 15W * ?            → weekday nearest to 15th at 9am
0 0 9 LW * ?             → last weekday of month at 9am
0 0 9 ? * 6#3            → 3rd friday of month at 9am
0 0 9 ? * 6L             → last friday of month at 9am
0 0 12 * * ? 2025        → every day at noon in 2025 only
0 0 9-17/2 * * ?         → every 2 hours between 9am-5pm
0 0,15,30,45 * * * ?     → every 15 minutes
0 0-30/5 9-17 * * ?      → every 5 minutes in 0-30 range, 9am-5pm
```

### Bidirectional Conversion

Both parsers support **complete round-trip conversion** with smart compaction and verbose natural language output:

```csharp
// Unix cron → natural language (basic examples)
converter.ToNaturalLanguage("*/30 * * * *")     → "every 30 minutes"
converter.ToNaturalLanguage("0 14 * * *")       → "every day at 2pm"
converter.ToNaturalLanguage("0 9 * * 1-5")      → "every weekday at 9am"

// Lists with smart compaction (3+ consecutive values become ranges)
converter.ToNaturalLanguage("0,15,30,45 * * * *")        → "every hour at minutes 0,15,30,45"
converter.ToNaturalLanguage("0 9,12,15,18 * * *")        → "every day at hours 9,12,15,18"
converter.ToNaturalLanguage("0 0 1,2,3,4,8,9,10 * *")    → "every month on the 1st-4th,8th-10th"
converter.ToNaturalLanguage("0-30/5 * * * *")             → "every 5 minutes between 0 and 30 of each hour"

// Ranges
converter.ToNaturalLanguage("0-30 * * * *")              → "every hour between minutes 0 and 30"
converter.ToNaturalLanguage("0 9-17 * * *")              → "every day between hours 9 and 17"
converter.ToNaturalLanguage("0 0 1-15 * *")              → "every month between the 1st and 15th"

// Quartz cron → natural language (advanced features)
quartzConverter.ToNaturalLanguage("0 0 9 L * ?")         → "every month on last day at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 L-3 * ?")       → "every month on 3rd to last day at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 L-1 * ?")       → "every month on day before last at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 15W * ?")       → "every month on weekday nearest the 15th at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 LW * ?")        → "every month on last weekday at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 ? * 6#3")       → "every month on 3rd friday at 9am"
quartzConverter.ToNaturalLanguage("0 0 9 ? * 6L")        → "every month on last friday at 9am"
quartzConverter.ToNaturalLanguage("0 0 12 * * ? 2025")   → "every day at 12pm in year 2025"

// Natural language → cron → natural language (preserves meaning)
var cron = converter.ToUnixCron("every weekday at 9am");  // "0 9 * * 1-5"
var back = converter.ToNaturalLanguage(cron);             // "every weekday at 9am"

// Combined month+day syntax (more natural phrasing)
var cron1 = converter.ToUnixCron("every month on january 1st at 1am");  // "0 1 1 1 *"
var back1 = converter.ToNaturalLanguage(cron1);                          // "every month on january 1st at 1am"

// Complex patterns with multiple constraints
var cron2 = converter.ToUnixCron("every day at hours 9,12,15,18 in january");  // "0 9,12,15,18 * 1 *"
var back2 = converter.ToNaturalLanguage(cron2);                                 // "every day at hours 9,12,15,18 in january"
```

**Smart Compaction**: The natural language formatter automatically compacts consecutive sequences:
- `[0,1,2,3,4]` → `"0-4"` (5 consecutive values)
- `[0,1,2,8,9,10]` → `"0-2,8-10"` (two sequences of 3+)
- `[0,15,30,45]` → `"0,15,30,45"` (non-consecutive, keep as list)

## Documentation

- **[Architecture](src/HumanCron/ARCHITECTURE.md)** - Design philosophy and implementation details
- **[DSL Specification](src/HumanCron/DSL-SPECIFICATION.md)** - Complete grammar and validation rules
- **[Integration Guide](src/HumanCron/INTEGRATION.md)** - Usage patterns, timezone handling, testing

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
