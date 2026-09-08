# HumanCron.Quartz: Quartz.NET 4 migration

Date: 2026-09-08
Status: approved design

## Decision

HumanCron.Quartz moves to Quartz.NET 4.0.0 and drops Quartz 3.x support. No dual-version
package is shipped. Quartz 3 consumers stay pinned to HumanCron.Quartz 0.8.0 (and therefore
HumanCron 0.8.0, because of the exact core-version pin). If a Quartz 3 package is ever needed
again it is recreated as a separate package from git history.

Why not both: HumanCron.Quartz targets net10.0 only, and Quartz 3.20.1 and 4.0.0 both ship
net10.0 assets, so there is no target-framework axis to split on. Supporting both would mean two
package IDs built from one source tree with a second test project and permanent double
maintenance. The owner's own consumers are moving to Quartz 4 now, so the cost is not justified.

## Scope

Changes are confined to `src/HumanCron.Quartz`, its tests under `tests/HumanCron.Tests`,
`Directory.Packages.props`, `Directory.Build.props` (version), and the docs. The core library,
NCrontab and Hangfire packages are untouched apart from the shared version bump.

## Package and version

- `Quartz` 3.18.2 -> 4.0.0 in `Directory.Packages.props`.
- Before any code change, run the package-currency check and bump every other outdated package
  to latest stable in the same first commit (standing policy).
- `Version` 0.8.0 -> 0.9.0 in `Directory.Build.props`. All packages release together.
- Target framework stays `net10.0`.
- Package description, README and INTEGRATION.md state that HumanCron.Quartz 0.9+ requires
  Quartz 4 and that Quartz 3 users pin 0.8.0.

## Public API changes (HumanCron.Quartz)

### Trigger builder return type

Quartz 4 turned `TriggerBuilder` into a static factory; the builder is `TriggerBuilder<TJob>`.

| Before | After |
| --- | --- |
| `ParseResult<TriggerBuilder> CreateTriggerBuilder(...)` | `ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(...)` |
| `ParseResult<TriggerBuilder> CreateTriggerBuilder(..., ScheduleParserOptions, ...)` | `ParseResult<TriggerBuilder<IJob>> ...` |
| `ParseResult<TriggerBuilder> CreateOneTimeTriggerBuilder(...)` | `ParseResult<TriggerBuilder<IJob>> ...` |

Chained caller code (`.ForJob(...)`, `.WithIdentity(...)`, `.Build()`) is unaffected.

### Misfire instruction parameters

Quartz 4 made the `MisfireInstruction` constants class internal. The public vocabulary is the
per-family enums. The `int misfireInstruction = 0` parameters become enums:

| Method | Parameter type | Default |
| --- | --- | --- |
| `ToQuartzSchedule(string, ...)` (both overloads) | `CronTriggerMisfireInstruction` | `SmartPolicy` |
| `CreateTriggerBuilder(string, ...)` (both overloads) | `CronTriggerMisfireInstruction` | `SmartPolicy` |
| `CreateOneTimeTriggerBuilder(...)` | `SimpleTriggerMisfireInstruction` | `SmartPolicy` |

Recurring methods take the cron-family enum because the caller cannot know in advance whether
the parse produces a cron trigger or a calendar-interval trigger. The two families carry
identical member names and values for the four policies both support (`SmartPolicy`,
`IgnoreMisfires`, `FireAndProceed`, `DoNothing`), so when the result is a calendar-interval
builder the value is cast to `CalendarIntervalTriggerMisfireInstruction`. The XML doc on each
recurring method says the value is applied to whichever recurring trigger family results.

The one-time trigger method always builds a simple trigger, so it takes that family's enum
directly.

`IQuartzScheduleConverter` and the `ScheduleBuilderExtensions` fluent entry points change to
match.

## Internal changes

- `MisfireInstructionHelper`: the int `switch` blocks and their range-check exceptions go
  away. Each family overload calls the builder's single `WithMisfireInstruction(enum)`. The
  `IScheduleBuilder` overload keeps its type dispatch and performs the cron-to-calendar cast.
- `QuartzCalendarIntervalBuilder`: `WithIntervalInDays/Months/Years(n)` become
  `WithInterval(n, IntervalUnit.Day/Month/Year)`. The weeks-expressed-as-days workaround for
  Quartz issue 1035 stays unless a test on 4.0.0 proves the bug fixed; if it is fixed the
  workaround and its comment are removed in the same change.
- `QuartzCronBuilder`: `CronScheduleBuilder.CronSchedule(expr)` becomes
  `CronScheduleBuilder.Create(expr)`.
- `QuartzScheduleConverter`: `TriggerBuilder.Create()` now yields `TriggerBuilder<IJob>`;
  `var` already handles it, only the declared return types change.
- Any further Quartz 4 rename that surfaces at compile time is fixed in place using the
  migration guide's appendix. No `#if`, no reflection, no shims.

## Tests

- `QuartzMisfireInstructionTests` and the one-time-trigger tests in
  `QuartzScheduleConverterTests` switch from `MisfireInstruction.*` ints to the enums, and read
  the typed `MisfireInstruction` property on `ICronTrigger` / `ICalendarIntervalTrigger` /
  `ISimpleTrigger` (or `MisfireInstructionCode` where a raw int comparison is the point).
- A new test covers the calendar-interval path receiving a `CronTriggerMisfireInstruction`
  and the built trigger reporting the equivalent `CalendarIntervalTriggerMisfireInstruction`.
- The two `CronScheduleBuilder.DailyAtHourAndMinute` usages in tests switch to a cron string.
- Round-trip, timezone and DST suites are expected to pass unchanged. Any failure is
  investigated as a behaviour difference in Quartz 4, not patched around.
- The full solution suite runs once, in the background, redirected to a file, before the PR.

## Docs

- README: Quartz section shows the enum-based signatures and the Quartz 4 requirement.
- INTEGRATION.md: `IQuartzScheduleConverter` signatures updated; misfire examples use enums.
- `HumanCron.Quartz.csproj` description mentions Quartz 4.

## Out of scope

- Multi-targeting or a Quartz 3 companion package.
- Any change to the natural-language grammar, core parser, NCrontab or Hangfire packages.
- Adopting new Quartz 4 features (RecurrenceTrigger, TimeProvider-aware `TriggerBuilder.Create(timeProvider)`, CronExpressionBuilder). Those are separate proposals.
