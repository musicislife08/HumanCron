# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - TBD

### Added
- Initial public release of HumanCron
- **Human-Readable Syntax** - Intuitive natural language scheduling with verbose, easy-to-read patterns
  - All schedules start with `"every"` for consistency
  - Examples: `"every 30 minutes"`, `"every day at 2pm"`, `"every monday at 9am"`
- **Bidirectional Conversion** - Convert between natural language and cron expressions (both directions)
  - Natural language → Unix cron (5-field format)
  - Unix cron → Natural language
  - Full semantic preservation in round-trip conversions
- **Quartz.NET Integration** (`HumanCron.Quartz` package)
  - Direct conversion to `IScheduleBuilder` for Quartz.NET
  - Automatic schedule builder selection (CronScheduleBuilder vs CalendarIntervalScheduleBuilder)
  - High-level `CreateTriggerBuilder()` API that handles start times automatically
- **Month Support** - Complete Unix cron parity for month selection
  - Single months: `"in january"`, `"in december"`
  - Month ranges: `"between january and march"`, `"between october and december"`
  - Month lists: `"in january,april,july,october"` (quarterly schedules)
  - Accepts both full names and abbreviations (`jan`/`january`, `feb`/`february`, etc.)
  - Output always uses full names for maximum readability
- **Day Patterns and Ranges**
  - Specific days: `"every monday"`, `"every friday"`
  - Day patterns: `"every weekday"`, `"every weekend"`
  - Day ranges: `"between monday and friday"`, `"between saturday and sunday"`
  - Accepts both full names and abbreviations (`mon`/`monday`, `tue`/`tuesday`, etc.)
- **Time Interval Support**
  - Seconds: `"every 30 seconds"`, `"every 45 seconds"`
  - Minutes: `"every 15 minutes"`, `"every 30 minutes"`
  - Hours: `"every hour"`, `"every 6 hours"`
  - Days: `"every day"`, `"every 7 days"`
  - Weeks: `"every week"`, `"every 2 weeks"` (CalendarInterval for multi-week)
  - Months: `"every month"`, `"every 3 months"`
  - Years: `"every year"`, `"every 2 years"`
- **Time of Day** - Flexible time specifications
  - 12-hour format: `"at 9am"`, `"at 2pm"`, `"at 3:30pm"`
  - 24-hour format: `"at 14:00"`, `"at 09:30"`
  - Supports minutes: `"at 3:30am"`, `"at 14:30"`
- **Timezone Awareness** - Proper DST handling using NodaTime
  - Automatic DST gap handling (spring forward)
  - Automatic DST overlap handling (fall back)
  - Configurable server and user timezones
  - Timezone-aware time conversions
- **Discriminated Union Pattern** - Type-safe month specification
  - `MonthSpecifier.None` - No month constraint
  - `MonthSpecifier.Single` - Specific month (e.g., January)
  - `MonthSpecifier.Range` - Month range (e.g., January-March)
  - `MonthSpecifier.List` - Month list (e.g., Jan, Apr, Jul, Oct)
  - Compile-time enforcement of mutual exclusivity
- **Dependency Injection** - First-class DI support
  - `AddNaturalCron()` extension method for service registration
  - Scoped lifetime for converters
  - `INaturalCronConverter` interface for Unix cron
  - `IQuartzScheduleConverter` interface for Quartz.NET
- **Comprehensive Testing** - Extensive test coverage including:
  - All interval types (seconds through years)
  - Single months, month ranges, month lists
  - Day ranges and patterns
  - Time specifications (12-hour and 24-hour formats)
  - Timezone conversions with DST edge cases (spring forward gaps, fall back ambiguities)
  - Fractional timezone offsets (India UTC+5:30, Nepal UTC+5:45)
  - Southern Hemisphere DST (Sydney October/April transitions)
  - Cross-midnight conversions and day boundary handling
  - Span<T> optimization edge cases (whitespace, malformed input, boundary values)
  - Round-trip conversions (natural ⟷ cron ⟷ natural)
  - Edge cases and error conditions
  - Both Unix cron and Quartz.NET conversions
- **Complete Documentation**
  - ARCHITECTURE.md - Design philosophy and implementation details
  - DSL-SPECIFICATION.md - Complete grammar and validation rules
  - INTEGRATION.md - Usage patterns, timezone handling, testing strategies

### Technical Details
- **Framework**: .NET 10.0, C# 14
- **Dependencies**: NodaTime (timezone handling), Quartz.NET (optional, for Quartz integration)
- **Pattern Matching**: Source-generated regex patterns for optimal performance
- **Architecture**: Discriminated unions for type safety, functional ParseResult pattern
- **Performance**: Zero-allocation Span<T> parsing for cron expressions
  - ReadOnlySpan<char> for string parsing (no substring allocations)
  - Stackalloc Range arrays for split operations
  - ValueSpan for regex group extraction
  - Collection expressions for test data (C# 14)

### Notes
- Includes workaround for Quartz.NET bug #1035 (CalendarIntervalScheduleBuilder.WithIntervalInWeeks ignores StartAt)
- Multi-week intervals with day-of-week constraints use day-based intervals internally (e.g., 21 days instead of 3 weeks)
- Output format always uses full names (`"every monday"` not `"every mon"`) for maximum readability
- Input accepts both full names and abbreviations for days and months

[0.1.0]: https://github.com/musicislife08/HumanCron/releases/tag/v0.1.0
