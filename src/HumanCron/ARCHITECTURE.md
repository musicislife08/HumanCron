# HumanCron - Architecture & Implementation Plan (v2)

**Date:** 2025-11-14 (Revised)
**Author:** Kass Eisenmenger
**Status:** Architecture revised - simpler string-to-string API
**Context:** Building bidirectional natural language ↔ cron expression library

---

## Project Vision (REVISED)

Build a .NET Standard 2.0 library that converts between natural language schedule descriptions and cron expressions using a **simple string-to-string API**. Core library targets **Unix 5-part cron** (the industry standard), with Quartz.NET support as an optional extension.

### Core Design Philosophy

**Simple string-to-string conversions:**
```csharp
string naturalLanguage → string unixCron
string unixCron → string naturalLanguage
```

**No intermediate types exposed in public API.** `ScheduleSpec` is an internal implementation detail.

Users don't want to learn domain models - they want: **"give me a string, get back a string"**.

### Key Design Decisions

1. **Production-tested** - Battle-tested in production before public release
2. **.NET 10** - Targets latest .NET for modern C# features and performance
3. **Unix cron as default** - Core library targets 5-part Unix cron (minute hour day month dayOfWeek), the most widely used format
4. **Quartz as extension** - Quartz.NET support in separate namespace/package, not core dependency
5. **String-to-string API** - No exposed intermediate types, simple conversions
6. **Bidirectional** - Text → Cron AND Cron → Text (unlike CronExpressionDescriptor which is one-way)
7. **DI-friendly** - First-class dependency injection support, no static classes

---

## Project Structure

```
HumanCron/                                  # Standalone repository
├── HumanCron.slnx
├── .github/workflows/
│   ├── build-and-test.yml
│   └── publish-nuget.yml
├── src/
│   ├── HumanCron/                          # Core library (Unix 5-part cron)
│   │   ├── HumanCron.csproj
│   │   │
│   │   ├── Abstractions/
│   │   └── IHumanCronConverter.cs          # String → String conversion
│   │
│   ├── Models/
│   │   ├── ParseResult.cs                    # Result<string> for conversions
│   │   │
│   │   └── Internal/                         # NOT exposed in public API
│   │       ├── ScheduleSpec.cs               # Internal parsing representation
│   │       ├── IntervalUnit.cs               # Internal enum
│   │       └── DayPattern.cs                 # Internal enum
│   │
│   ├── Parsing/
│   │   ├── NaturalLanguageParser.cs          # Text → ScheduleSpec (internal)
│   │   └── PatternMatchers.cs                # Regex patterns (internal)
│   │
│   ├── Converters/
│   │   └── Unix/
│   │       ├── UnixCronConverter.cs          # Implements IHumanCronConverter
│   │       ├── UnixCronBuilder.cs            # ScheduleSpec → 5-part cron (internal)
│   │       └── UnixCronParser.cs             # 5-part cron → ScheduleSpec (internal)
│   │
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs    # AddHumanCron() for DI registration
│
├── HumanCron.Quartz/                       # Quartz.NET extension (SEPARATE)
│   ├── HumanCron.Quartz.csproj
│   │
│   ├── Abstractions/
│   │   └── IQuartzScheduleConverter.cs       # String ↔ IScheduleBuilder
│   │
│   ├── Converters/
│   │   ├── QuartzScheduleConverter.cs        # Implements IQuartzScheduleConverter
│   │   ├── QuartzCronBuilder.cs              # ScheduleSpec → CronScheduleBuilder (internal)
│   │   └── QuartzCalendarBuilder.cs          # ScheduleSpec → CalendarIntervalScheduleBuilder (internal)
│   │
│   ├── Parsers/
│   │   └── QuartzScheduleParser.cs           # IScheduleBuilder → ScheduleSpec (internal)
│   │
│   └── Extensions/
│       └── ScheduleBuilderExtensions.cs      # Fluent builder → Quartz schedules
│
└── HumanCron.Tests/                        # Test project
    ├── HumanCron.Tests.csproj
    │
    ├── Core/
    │   ├── UnixCronConverterTests.cs
    │   └── RoundTripTests.cs                 # text → cron → text = same meaning
    │
    └── Quartz/
        ├── QuartzScheduleConverterTests.cs
        └── QuartzRoundTripTests.cs
```

---

## Core API (String-to-String)

### IHumanCronConverter (Core Interface)

```csharp
namespace HumanCron.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and Unix 5-part cron expressions
/// </summary>
/// <remarks>
/// Simple string-to-string API - no intermediate types exposed.
///
/// Examples:
/// - "every day at 2pm" → "0 14 * * *"
/// - "0 14 * * *" → "every day at 2pm"
/// - "every 30 minutes" → "*/30 * * * *"
/// - "every sunday at 3am" → "0 3 * * 0"
/// </remarks>
public interface IHumanCronConverter
{
    /// <summary>
    /// Convert natural language to Unix 5-part cron expression
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "every day at 2pm")</param>
    /// <returns>Unix 5-part cron expression (e.g., "0 14 * * *")</returns>
    ParseResult<string> ToCron(string naturalLanguage);

    /// <summary>
    /// Convert Unix 5-part cron expression back to natural language
    /// </summary>
    /// <param name="cronExpression">Unix 5-part cron (e.g., "0 14 * * *")</param>
    /// <returns>Natural language schedule (e.g., "every day at 2pm")</returns>
    ParseResult<string> ToNaturalLanguage(string cronExpression);
}
```

### ParseResult (Success/Error Result Type)

```csharp
namespace HumanCron.Models;

/// <summary>
/// Result of parsing/conversion operation
/// </summary>
public abstract class ParseResult<T>
{
    public abstract bool IsSuccess { get; }
    public abstract T? Value { get; }
    public abstract string? ErrorMessage { get; }

    /// <summary>
    /// Successful result with value
    /// </summary>
    public sealed class Success : ParseResult<T>
    {
        public override bool IsSuccess => true;
        public override T Value { get; }
        public override string? ErrorMessage => null;

        internal Success(T value) => Value = value;
    }

    /// <summary>
    /// Failed result with error message
    /// </summary>
    public sealed class Error : ParseResult<T>
    {
        public override bool IsSuccess => false;
        public override T? Value => default;
        public override string ErrorMessage { get; }

        internal Error(string errorMessage) => ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Create successful result
    /// </summary>
    public static ParseResult<T> Success(T value) => new Success(value);

    /// <summary>
    /// Create error result
    /// </summary>
    public static ParseResult<T> Error(string errorMessage) => new Error(errorMessage);
}
```

---

## Quartz Extension API (String ↔ Schedule Objects)

### IQuartzScheduleConverter (Extension Interface)

```csharp
namespace HumanCron.Quartz.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and Quartz.NET schedule builders
/// </summary>
/// <remarks>
/// Handles both simple patterns (CronScheduleBuilder) and complex patterns (CalendarIntervalScheduleBuilder).
///
/// Examples:
/// - "every day at 2pm" → CronScheduleBuilder.DailyAtHourAndMinute(14, 0)
/// - "every 2 weeks on sunday at 3am" → CalendarIntervalScheduleBuilder.Create().WithIntervalInWeeks(2)...
/// - CronScheduleBuilder → "every day at 2pm"
/// - CalendarIntervalScheduleBuilder → "every 2 weeks on sunday at 3am"
/// </remarks>
public interface IQuartzScheduleConverter
{
    /// <summary>
    /// Convert natural language to Quartz schedule builder
    /// Returns CronScheduleBuilder for simple patterns, CalendarIntervalScheduleBuilder for complex patterns
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "every 2 weeks on sunday at 3am")</param>
    /// <returns>Quartz IScheduleBuilder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</returns>
    ParseResult<IScheduleBuilder> ToQuartzSchedule(string naturalLanguage);

    /// <summary>
    /// Convert Quartz schedule builder back to natural language
    /// Supports both CronScheduleBuilder and CalendarIntervalScheduleBuilder
    /// </summary>
    /// <param name="scheduleBuilder">Quartz schedule builder</param>
    /// <returns>Natural language schedule</returns>
    ParseResult<string> ToNaturalLanguage(IScheduleBuilder scheduleBuilder);
}
```

---

## Internal Models (NOT Exposed in Public API)

### ScheduleSpec (Internal Parsing Representation)

```csharp
namespace HumanCron.Models.Internal;

/// <summary>
/// Internal representation of parsed schedule (NOT exposed in public API)
/// Used as intermediate format during parsing/conversion
/// </summary>
internal sealed class ScheduleSpec
{
    public int Interval { get; init; }
    public IntervalUnit Unit { get; init; }
    public DayOfWeek? DayOfWeek { get; init; }
    public DayPattern? DayPattern { get; init; }
    public int? DayOfMonth { get; init; }
    public TimeOnly? TimeOfDay { get; init; }
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Utc;
}

internal enum IntervalUnit
{
    Seconds,
    Minutes,
    Hours,
    Days,
    Weeks,
    Months,
    Years
}

internal enum DayPattern
{
    Weekdays,  // Monday-Friday
    Weekends   // Saturday-Sunday
}
```

---

## Supported Natural Language Patterns

### Pattern 1: Interval Only
- `"every 30 minutes"` → `"*/30 * * * *"` (every 30 minutes)
- `"every 6 hours"` → `"0 */6 * * *"` (every 6 hours)
- `"every day"` → `"0 0 * * *"` (daily at midnight)
- `"every week"` → `"0 0 * * 0"` (weekly on Sunday at midnight)

### Pattern 2: Interval at Specific Time
- `"every day at 2pm"` → `"0 14 * * *"` (daily at 2pm)
- `"every week at 3:30am"` → `"30 3 * * 0"` (weekly on Sunday at 3:30am)

### Pattern 3: Specific Day Patterns
- `"every weekday"` → `"0 0 * * 1-5"` (every weekday at midnight)
- `"every weekend"` → `"0 0 * * 0,6"` (every weekend day at midnight)
- `"every monday at 9am"` → `"0 9 * * 1"` (every Monday at 9am)

### Pattern 4: Month Selection
- `"every day in january"` → `"0 0 * 1 *"` (every day in January at midnight)
- `"every day between january and march at 9am"` → `"0 9 * 1-3 *"` (every day Jan-Mar at 9am)
- `"every day in january,april,july,october"` → `"0 0 * 1,4,7,10 *"` (quarterly at midnight)
- `"every 2 weeks on sunday at 1pm"` → **CalendarIntervalSchedule** (cannot be expressed as Unix cron)

---

## Usage Examples

### Example 1: Core Library (Unix Cron)

```csharp
// In Program.cs or Startup.cs
services.AddHumanCron();

// In your component or service
@inject IHumanCronConverter CronConverter

private void OnScheduleChanged(string naturalLanguage)
{
    // Convert natural language → Unix cron
    var result = CronConverter.ToCron(naturalLanguage);

    if (result is ParseResult<string>.Success success)
    {
        var cronExpression = success.Value;  // "0 14 * * *"
        await SaveToDatabase(cronExpression);
    }
    else if (result is ParseResult<string>.Error error)
    {
        _validationError = error.ErrorMessage;
    }
}

private void DisplaySchedule(string cronExpression)
{
    // Convert Unix cron → Natural language
    var result = CronConverter.ToNaturalLanguage(cronExpression);

    if (result is ParseResult<string>.Success success)
    {
        _displayText = success.Value;  // "every day at 2pm"
    }
}
```

### Example 2: Quartz Extension

```csharp
// In Program.cs or Startup.cs
using HumanCron; // Single namespace, auto-discovers Quartz extension

services.AddHumanCron(); // Registers base + Quartz services automatically

// In QuartzSchedulingSyncService.cs
@inject IQuartzScheduleConverter QuartzConverter

private async Task CreateOrUpdateTriggerAsync(string naturalLanguage)
{
    // Convert natural language → Quartz schedule
    var result = QuartzConverter.ToQuartzSchedule(naturalLanguage);

    if (result is ParseResult<IScheduleBuilder>.Success success)
    {
        var scheduleBuilder = success.Value;  // CronScheduleBuilder or CalendarIntervalScheduleBuilder

        var trigger = TriggerBuilder.Create()
            .WithIdentity(jobKey)
            .WithSchedule(scheduleBuilder)
            .StartNow()
            .Build();

        await _scheduler.ScheduleJob(trigger);
    }
}

private string GetNaturalLanguageFromTrigger(ITrigger trigger)
{
    // Convert Quartz schedule → Natural language
    if (trigger.GetScheduleBuilder() is IScheduleBuilder scheduleBuilder)
    {
        var result = QuartzConverter.ToNaturalLanguage(scheduleBuilder);

        if (result is ParseResult<string>.Success success)
        {
            return success.Value;  // "every 2 weeks on sunday at 3am"
        }
    }

    return "Unknown schedule";
}
```

---

## Dependency Injection Setup

### Single Method Registration (Auto-Discovery Pattern)

```csharp
namespace HumanCron; // Main namespace for discoverability

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register HumanCron services (base + auto-discovered extensions)
    /// </summary>
    public static IServiceCollection AddHumanCron(this IServiceCollection services)
    {
        // Register NodaTime dependencies
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.TryAddSingleton<DateTimeZone>(provider =>
            DateTimeZoneProviders.Tzdb.GetSystemDefault());

        // Register base services
        services.AddTransient<IScheduleParser, NaturalLanguageParser>();
        services.AddTransient<IScheduleFormatter, NaturalLanguageFormatter>();
        services.AddTransient<IHumanCronConverter, UnixCronConverter>();

        // Auto-discover extension packages (Quartz, Hangfire, etc.)
        RegisterExtensionServices(services);

        return services;
    }

    private static void RegisterExtensionServices(IServiceCollection services)
    {
        // Scan for HumanCron.* extension assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("HumanCron.") is true
                            && assembly.GetName().Name != "HumanCron");

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract) continue;

                // Auto-register IQuartzScheduleConverter implementations
                var quartzInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.Name == "IQuartzScheduleConverter");

                if (quartzInterface != null)
                {
                    services.AddTransient(quartzInterface, type);
                }
            }
        }
    }
}
```

**Benefits**:
- Single method call (`AddHumanCron()`) regardless of packages installed
- Automatic discovery of Quartz extension if HumanCron.Quartz package is installed
- Extensible for future packages (Hangfire, NCrontab, etc.)
- No namespace confusion - always `using HumanCron;`

---

## NuGet Dependencies

### HumanCron.csproj (Core)

```xml
<ItemGroup>
  <!-- DI abstractions (no concrete implementation) -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />

  <!-- Cron validation and next-run calculation -->
  <PackageReference Include="Cronos" Version="0.8.4" />
</ItemGroup>
```

### HumanCron.Quartz.csproj (Extension)

```xml
<ItemGroup>
  <!-- Reference core library -->
  <ProjectReference Include="..\HumanCron\HumanCron.csproj" />

  <!-- Quartz.NET dependency -->
  <PackageReference Include="Quartz" Version="3.15.1" />
</ItemGroup>
```

### HumanCron.Tests.csproj

```xml
<ItemGroup>
  <PackageReference Include="NUnit" Version="4.3.1" />
  <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="FluentAssertions" Version="7.0.0" />
</ItemGroup>
```

---

## Testing Strategy

### Unit Tests (High Coverage)

1. **Core Library Tests** (`UnixCronConverterTests.cs`)
   - Natural language → Unix cron conversion for all patterns
   - Unix cron → Natural language reverse conversion
   - Invalid patterns and error messages
   - Edge cases: midnight, special characters

2. **Round-Trip Tests** (`RoundTripTests.cs`)
   - Text → Cron → Text produces semantically equivalent result
   - Verify no information loss during conversion

3. **Quartz Extension Tests** (`QuartzScheduleConverterTests.cs`)
   - Natural language → Quartz schedule builder conversion
   - Quartz schedule builder → Natural language reverse conversion
   - CronScheduleBuilder for simple patterns
   - CalendarIntervalScheduleBuilder for complex patterns (multi-week)

---

## Implementation Phases

### Phase 1: Core Library (Unix Cron)
1. Create `HumanCron.csproj` (.NET Standard 2.0)
2. Implement `IHumanCronConverter` interface
3. Create internal `ScheduleSpec` model
4. Implement `NaturalLanguageParser` (text → ScheduleSpec)
5. Implement `UnixCronBuilder` (ScheduleSpec → 5-part cron)
6. Implement `UnixCronParser` (5-part cron → ScheduleSpec)
7. Implement `UnixCronConverter` (ties everything together)
8. Add comprehensive unit tests
9. Add DI registration (`AddHumanCron()`)

### Phase 2: Quartz Extension
1. Create `HumanCron.Quartz.csproj` (.NET Standard 2.0)
2. Implement `IQuartzScheduleConverter` interface
3. Implement `QuartzCronBuilder` (ScheduleSpec → CronScheduleBuilder)
4. Implement `QuartzCalendarBuilder` (ScheduleSpec → CalendarIntervalScheduleBuilder)
5. Implement `QuartzScheduleParser` (IScheduleBuilder → ScheduleSpec)
6. Implement `QuartzScheduleConverter` (ties everything together)
7. Add comprehensive unit tests
8. Extension auto-discovered via assembly scanning in `AddHumanCron()`

### Phase 3: Production Integration Example
1. Install `HumanCron` from NuGet
2. Install `HumanCron.Quartz` for Quartz.NET integration
3. Update scheduling service to use `IQuartzScheduleConverter`
4. Update `BackgroundJobs.razor` to use `IHumanCronConverter`
5. Test in production use case

**Total Estimated Effort:** 12-18 hours

---

## Success Criteria

### v0.1.0 (Initial Public Release)
- ✅ Core library supports Unix 5-part cron
- ✅ Quartz extension supports both CronScheduleBuilder and CalendarIntervalScheduleBuilder
- ✅ Simple string-to-string API (no exposed intermediate types)
- ✅ Bidirectional conversion (lossless round-trips)
- ✅ 90%+ test coverage
- ✅ Works in production (BackgroundJobs.razor + QuartzSchedulingSyncService)

### v1.0.0 (Standalone NuGet Packages)
- ✅ `HumanCron` package (core Unix cron support)
- ✅ `HumanCron.Quartz` package (optional Quartz extension)
- ✅ Extracted to separate repo
- ✅ Comprehensive README with examples
- ✅ Published to NuGet.org
- ✅ 95%+ test coverage

---

## Notes & Decisions

### Why Unix Cron as Default?
- **Industry standard**: Unix/Linux crontab, GitHub Actions, GitLab CI, AWS EventBridge, Google Cloud Scheduler all use 5-part cron
- **Maximum compatibility**: Most widely supported format across platforms
- **Simplicity**: Users expect standard cron format, not Quartz-specific

### Why Quartz as Extension?
- **Quartz is niche**: Only used in .NET ecosystem, not universal
- **Adds complexity**: 6-part format (with seconds), CalendarIntervalSchedule for complex patterns
- **Optional dependency**: Core library shouldn't force Quartz dependency on all users

### Why String-to-String API?
- **Simplicity**: Users don't want to learn intermediate types
- **Mental model**: Matches user expectation ("convert this string to that string")
- **Ease of use**: No need to understand `ScheduleSpec`, `IntervalUnit`, etc.
- **Flexibility**: Internal implementation can change without breaking API

### Why Keep ScheduleSpec Internal?
- **Implementation detail**: Users don't need to see parsing internals
- **Future flexibility**: Can refactor parsing logic without breaking API
- **Simplicity**: Fewer types for users to learn

---

## Related Documentation

- [Original Research Document](/tmp/natural-language-scheduling-research.md)
- [Cronos Library](https://www.nuget.org/packages/Cronos)
- [Quartz.NET Documentation](https://www.quartz-scheduler.net/)

---

**End of Architecture Plan (v2)**
