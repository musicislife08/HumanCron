# Quartz.NET 4 Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move HumanCron.Quartz from Quartz.NET 3.x to 4.0.0, with enum-based misfire parameters and `TriggerBuilder<IJob>` return types, and release everything as 0.9.0.

**Architecture:** HumanCron.Quartz is a thin adapter: natural language → `ScheduleSpec` (core) → Quartz `IScheduleBuilder` / `TriggerBuilder`, plus the reverse. Only the adapter's Quartz-facing edges change: the misfire helper, the two schedule builders, the converter's public signatures, the trigger parser, and their tests. Core, NCrontab and Hangfire are untouched except for the shared version bump.

**Tech Stack:** .NET 10, C# (latest, `TreatWarningsAsErrors`), Quartz.NET 4.0.0, NodaTime, NUnit 4, Central Package Management (`Directory.Packages.props`).

**Spec:** `docs/superpowers/specs/2026-09-08-quartz-4-migration-design.md`

## Global Constraints

- Branch: `feature/quartz-4` (already created; the spec is committed on it). Work directly on the branch; no worktrees.
- Task 1 bumps every package to latest stable but holds `Quartz` at `3.20.1`; Task 2 bumps `Quartz` to `4.0.0`. Nothing else starts until Task 1 is green.
- Package versions are changed with `dotnet add <project> package <id> --version <v>` (updates `Directory.Packages.props` under CPM). Never hand-edit the XML.
- Target framework stays `net10.0`. No `#if`, no reflection, no Quartz 3 compatibility shims.
- Public API after Task 2 (exact):
  - `ParseResult<IScheduleBuilder> ToQuartzSchedule(string naturalLanguage, CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)`
  - `ParseResult<IScheduleBuilder> ToQuartzSchedule(string naturalLanguage, ScheduleParserOptions options, CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)`
  - `ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(string naturalLanguage, CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)`
  - `ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(string naturalLanguage, ScheduleParserOptions options, CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)`
  - `ParseResult<TriggerBuilder<IJob>> CreateOneTimeTriggerBuilder(string duration, DateTimeOffset? anchor = null, DateTimeZone? timeZone = null, SimpleTriggerMisfireInstruction misfireInstruction = SimpleTriggerMisfireInstruction.SmartPolicy)`
- Version: `0.8.0` → `0.9.0` in `Directory.Build.props` (Task 4).
- Tests: the authoritative run is ONE solution-level `dotnet test HumanCron.slnx`, in the background, redirected to a file in the scratchpad, then grepped. Never `--no-build`, never pipe to `tail`/`head`. Per-task verification runs may use `--filter`, but still redirect to a file.
- Commit messages end with `Co-Authored-By: Claude <noreply@anthropic.com>`. Do not push and do not open a PR until the owner asks.
- Verified Quartz 4.0.0 facts this plan relies on (probed by reflection against the published package on 2026-09-08):
  - Enums: `CronTriggerMisfireInstruction { SmartPolicy=0, FireAndProceed=1, DoNothing=2, IgnoreMisfires=-1 }`, `CalendarIntervalTriggerMisfireInstruction` (same four members and values), `SimpleTriggerMisfireInstruction { SmartPolicy=0, FireNow=1, NowWithExistingCount=2, NowWithRemainingCount=3, NextWithRemainingCount=4, NextWithExistingCount=5, IgnoreMisfires=-1 }`.
  - Builders: `CronScheduleBuilder.Create(string)`, `.InTimeZone(TimeZoneInfo)`, `.WithMisfireInstruction(CronTriggerMisfireInstruction)`; `CalendarIntervalScheduleBuilder.Create()`, `.WithInterval(int, IntervalUnit)`, `.InTimeZone(TimeZoneInfo)`, `.WithMisfireInstruction(CalendarIntervalTriggerMisfireInstruction)`; `SimpleScheduleBuilder.Create()`, `.WithRepeatCount(int)`, `.WithMisfireInstruction(SimpleTriggerMisfireInstruction)`.
  - `TriggerBuilder.Create(TimeProvider? timeProvider = null)` returns `TriggerBuilder<IJob>`; instance methods `WithIdentity`, `WithSchedule(IScheduleBuilder)`, `StartAt`, `StartNow`, `ForJob`, `Build`.
  - `ITrigger.MisfireInstructionCode` (int); typed `MisfireInstruction` on `ICronTrigger`, `ICalendarIntervalTrigger`, `ISimpleTrigger`. `ITrigger.GetFireTimeAfter(DateTimeOffset)` still exists.
  - An out-of-range misfire enum value is accepted by the builder and rejected by `TriggerBuilder.Build()` with `ArgumentException: The misfire instruction code is invalid for this type of trigger.` HumanCron does not re-validate.
  - `CalendarIntervalScheduleBuilder.Create().WithInterval(2, IntervalUnit.Week)` with `StartAt(<future Sunday>)` fires first exactly at `StartAt`. Quartz issue 1035 (weeks ignoring StartAt) no longer reproduces.

---

## File Structure

| File | Change |
| --- | --- |
| `Directory.Packages.props` | Task 1: all packages latest stable, Quartz 3.20.1. Task 2: Quartz 4.0.0. |
| `Directory.Build.props` | Task 4: `Version` 0.9.0. |
| `src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs` | Task 2: int switches → enum pass-through with cron→calendar cast. |
| `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs` | Task 2: enum parameters, `TriggerBuilder<IJob>` returns, XML docs. |
| `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs` | Task 2: match the interface. |
| `src/HumanCron.Quartz/Builders/QuartzCronBuilder.cs` | Task 2: `CronSchedule(...)` → `Create(...)`. |
| `src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs` | Task 2: `WithIntervalInX(n)` → `WithInterval(n, unit)`. Task 3: weeks emitted as weeks. |
| `src/HumanCron.Quartz/Builders/QuartzScheduleParser.cs` | Task 3: remove the days-multiple-of-7 → weeks reverse mapping. |
| `src/HumanCron.Quartz/HumanCron.Quartz.csproj` | Task 4: description mentions Quartz 4. |
| `tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs` | Task 2: rewritten for enums. |
| `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs` | Task 2: `TriggerBuilder<IJob>`, `Create("0 0 14 * * ?")`, one-time misfire enum. |
| `tests/HumanCron.Tests/Quartz/QuartzScheduleBuilderTests.cs` | Task 3: week-interval expectations, StartAt regression test. |
| `README.md`, `INTEGRATION.md`, `ARCHITECTURE.md` | Task 4: signatures, Quartz 4 requirement, misfire example. |

---

### Task 1: Update all packages to latest stable (Quartz held at 3.20.1)

**Files:**
- Modify: `Directory.Packages.props` (via `dotnet add package`)

**Interfaces:**
- Consumes: nothing.
- Produces: a green solution on current packages, so Task 2's breakage is attributable to Quartz 4 alone.

- [ ] **Step 1: Confirm the outdated list**

Run:
```bash
dotnet list HumanCron.slnx package --outdated
dotnet list HumanCron.slnx package --vulnerable
```
Expected (as of 2026-09-08; re-check the "Latest" column and use whatever it says today, these are the numbers seen when the plan was written):

| Package | Current | Latest stable |
| --- | --- | --- |
| Hangfire.Core | 1.8.23 | 1.8.25 |
| Microsoft.Extensions.DependencyInjection | 10.0.9 | 10.0.11 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.9 | 10.0.11 |
| Microsoft.NET.Test.Sdk | 18.7.0 | 18.9.0 |
| NodaTime | 3.3.2 | 3.3.3 |
| NodaTime.Testing | 3.3.2 | 3.3.3 |
| NUnit3TestAdapter | 6.2.0 | 6.3.0 |
| Quartz | 3.18.2 | 4.0.0 (use **3.20.1** in this task) |

No vulnerable packages expected.

- [ ] **Step 2: Bump each package with the CLI**

Run (one project that references each package is enough; CPM updates the shared version):
```bash
dotnet add src/HumanCron.Hangfire/HumanCron.Hangfire.csproj package Hangfire.Core --version 1.8.25
dotnet add tests/HumanCron.Tests/HumanCron.Tests.csproj package Microsoft.Extensions.DependencyInjection --version 10.0.11
dotnet add src/HumanCron/HumanCron.csproj package Microsoft.Extensions.DependencyInjection.Abstractions --version 10.0.11
dotnet add tests/HumanCron.Tests/HumanCron.Tests.csproj package Microsoft.NET.Test.Sdk --version 18.9.0
dotnet add src/HumanCron/HumanCron.csproj package NodaTime --version 3.3.3
dotnet add tests/HumanCron.Tests/HumanCron.Tests.csproj package NodaTime.Testing --version 3.3.3
dotnet add tests/HumanCron.Tests/HumanCron.Tests.csproj package NUnit3TestAdapter --version 6.3.0
dotnet add src/HumanCron.Quartz/HumanCron.Quartz.csproj package Quartz --version 3.20.1
```
Then confirm only `Directory.Packages.props` changed (no `Version=` attributes leaked into a csproj):
```bash
git status --short
git diff --stat
```
Expected: only `Directory.Packages.props` in the diff. If a csproj gained a `Version=` attribute, remove that attribute and set the version in `Directory.Packages.props` instead.

- [ ] **Step 3: Build**

Run:
```bash
dotnet build HumanCron.slnx > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task1-build.txt 2>&1; grep -E "error|Warn|Build succeeded" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task1-build.txt
```
Expected: `Build succeeded.` with 0 warnings and 0 errors. If a bump breaks something, fix the code on this branch now (the bump is never reverted).

- [ ] **Step 4: Run the full suite**

Run in the background:
```bash
dotnet test HumanCron.slnx > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task1-test.txt 2>&1
```
When it finishes:
```bash
grep -E "Passed!|Failed!|Total tests|error" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task1-test.txt
```
Expected: `Passed!` with 0 failed. Any failure is investigated and fixed before continuing.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore(deps): bump all packages to latest stable (Quartz held at 3.20.1)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Move HumanCron.Quartz to Quartz.NET 4.0.0

**Files:**
- Modify: `Directory.Packages.props` (via `dotnet add package`)
- Modify: `src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs` (whole file)
- Modify: `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs` (whole file)
- Modify: `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs:47-90, 188-283`
- Modify: `src/HumanCron.Quartz/Builders/QuartzCronBuilder.cs:21`
- Modify: `src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs:1-8, 39-48`
- Modify: `tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs` (whole file)
- Modify: `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs:166, 500-512` plus `ParseResult<TriggerBuilder>` occurrences

**Interfaces:**
- Consumes: green Task 1 build.
- Produces: the public API listed in Global Constraints; `MisfireInstructionHelper` overloads:
  - `CronScheduleBuilder ApplyMisfireInstruction(CronScheduleBuilder builder, CronTriggerMisfireInstruction misfireInstruction)`
  - `CalendarIntervalScheduleBuilder ApplyMisfireInstruction(CalendarIntervalScheduleBuilder builder, CronTriggerMisfireInstruction misfireInstruction)`
  - `SimpleScheduleBuilder ApplyMisfireInstruction(SimpleScheduleBuilder builder, SimpleTriggerMisfireInstruction misfireInstruction)`
  - `IScheduleBuilder ApplyMisfireInstruction(IScheduleBuilder builder, CronTriggerMisfireInstruction misfireInstruction)`

This task cannot be split into compile-green sub-steps: the package bump breaks the build and the fixes below restore it. Write all fixes, then build once. The rewritten misfire tests are the failing tests for this task; they fail (do not compile) until the code matches.

- [ ] **Step 1: Bump Quartz to 4.0.0**

```bash
dotnet add src/HumanCron.Quartz/HumanCron.Quartz.csproj package Quartz --version 4.0.0
git diff Directory.Packages.props
```
Expected: `<PackageVersion Include="Quartz" Version="4.0.0" />`.

- [ ] **Step 2: Rewrite the misfire tests for the enum API**

Replace the entire content of `tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs` with:

```csharp
using HumanCron.Models;
using HumanCron.Quartz;
using HumanCron.Quartz.Abstractions;
using HumanCron.Quartz.Helpers;
using Quartz;

namespace HumanCron.Tests.Quartz;

/// <summary>
/// Tests for Quartz misfire instruction support.
/// Recurring schedules accept <see cref="CronTriggerMisfireInstruction"/> and apply it to whichever
/// trigger family (cron or calendar-interval) the parse produces. One-time triggers take
/// <see cref="SimpleTriggerMisfireInstruction"/> directly because that family is fixed.
/// </summary>
[TestFixture]
public class QuartzMisfireInstructionTests
{
    private IQuartzScheduleConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = QuartzScheduleConverterFactory.Create();
    }

    private static ITrigger BuildTrigger(IScheduleBuilder scheduleBuilder) =>
        TriggerBuilder.Create().WithIdentity("test").WithSchedule(scheduleBuilder).Build();

    private static IScheduleBuilder SuccessSchedule(ParseResult<IScheduleBuilder> result)
    {
        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
        return ((ParseResult<IScheduleBuilder>.Success)result).Value;
    }

    private static ITrigger SuccessTrigger(ParseResult<TriggerBuilder<IJob>> result)
    {
        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Success>());
        return ((ParseResult<TriggerBuilder<IJob>>.Success)result).Value.Build();
    }

    #region ToQuartzSchedule - cron family

    [Test]
    public void ToQuartzSchedule_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule("every day at 2pm")));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(CronTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every hour", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every 30 minutes", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every day at 9am", CronTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every day at 10am", CronTriggerMisfireInstruction.SmartPolicy)]
    public void ToQuartzSchedule_CronSchedule_AppliesInstruction(string natural, CronTriggerMisfireInstruction instruction)
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule(natural, instruction)));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(instruction));
    }

    #endregion

    #region ToQuartzSchedule - calendar-interval family

    [Test]
    public void ToQuartzSchedule_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule("every 2 weeks")));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction,
            Is.EqualTo(CalendarIntervalTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every 3 months", CronTriggerMisfireInstruction.DoNothing, CalendarIntervalTriggerMisfireInstruction.DoNothing)]
    [TestCase("every year", CronTriggerMisfireInstruction.IgnoreMisfires, CalendarIntervalTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every 2 weeks on sunday", CronTriggerMisfireInstruction.FireAndProceed, CalendarIntervalTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every 2 weeks on sunday at 2pm", CronTriggerMisfireInstruction.DoNothing, CalendarIntervalTriggerMisfireInstruction.DoNothing)]
    public void ToQuartzSchedule_CalendarInterval_MapsCronInstructionToCalendarFamily(
        string natural,
        CronTriggerMisfireInstruction given,
        CalendarIntervalTriggerMisfireInstruction expected)
    {
        var trigger = BuildTrigger(SuccessSchedule(_converter.ToQuartzSchedule(natural, given)));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction, Is.EqualTo(expected));
        Assert.That(trigger.GetFireTimeAfter(DateTimeOffset.UtcNow), Is.Not.Null);
    }

    #endregion

    #region CreateTriggerBuilder

    [Test]
    public void CreateTriggerBuilder_CronSchedule_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder("every day at 2pm"));

        Assert.That(trigger, Is.InstanceOf<ICronTrigger>());
        Assert.That(((ICronTrigger)trigger).MisfireInstruction, Is.EqualTo(CronTriggerMisfireInstruction.SmartPolicy));
    }

    [Test]
    public void CreateTriggerBuilder_CalendarInterval_DefaultMisfire_UsesSmartPolicy()
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder("every 2 weeks on monday"));

        Assert.That(trigger, Is.InstanceOf<ICalendarIntervalTrigger>());
        Assert.That(((ICalendarIntervalTrigger)trigger).MisfireInstruction,
            Is.EqualTo(CalendarIntervalTriggerMisfireInstruction.SmartPolicy));
    }

    [TestCase("every hour", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every 15 minutes", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every day at 2pm", CronTriggerMisfireInstruction.FireAndProceed)]
    [TestCase("every week at 5pm", CronTriggerMisfireInstruction.SmartPolicy)]
    [TestCase("every 3 months", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every year", CronTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase("every 2 weeks on monday", CronTriggerMisfireInstruction.DoNothing)]
    [TestCase("every month at 9am", CronTriggerMisfireInstruction.IgnoreMisfires)]
    public void CreateTriggerBuilder_AppliesInstructionCodeToResultingFamily(
        string natural,
        CronTriggerMisfireInstruction instruction)
    {
        var trigger = SuccessTrigger(_converter.CreateTriggerBuilder(natural, instruction));

        Assert.That(trigger.MisfireInstructionCode, Is.EqualTo((int)instruction));
        Assert.That(trigger.GetFireTimeAfter(DateTimeOffset.UtcNow), Is.Not.Null,
            $"Trigger for '{natural}' with {instruction} should have a next fire time");
    }

    #endregion

    #region Input validation still runs before misfire handling

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void CreateTriggerBuilder_WithInvalidInput_ReturnsError(string? invalidInput)
    {
        var result = _converter.CreateTriggerBuilder(invalidInput!, CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Contain("empty").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_WithInputExceedingMaxLength_ReturnsError()
    {
        var result = _converter.CreateTriggerBuilder(new string('a', 1001), CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Contain("maximum length").IgnoreCase);
    }

    [Test]
    public void ToQuartzSchedule_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        var result = _converter.ToQuartzSchedule("every 999 potato chips", CronTriggerMisfireInstruction.DoNothing);

        Assert.That(result, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        Assert.That(((ParseResult<IScheduleBuilder>.Error)result).Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    [Test]
    public void CreateTriggerBuilder_InvalidScheduleWithValidMisfire_ReturnsParseError()
    {
        var result = _converter.CreateTriggerBuilder("every 42 bananas at midnight", CronTriggerMisfireInstruction.IgnoreMisfires);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder<IJob>>.Error>());
        Assert.That(((ParseResult<TriggerBuilder<IJob>>.Error)result).Message, Does.Not.Contain("misfire").IgnoreCase);
    }

    #endregion

    #region SimpleScheduleBuilder (one-time trigger)

    [TestCase(SimpleTriggerMisfireInstruction.SmartPolicy)]
    [TestCase(SimpleTriggerMisfireInstruction.IgnoreMisfires)]
    [TestCase(SimpleTriggerMisfireInstruction.FireNow)]
    [TestCase(SimpleTriggerMisfireInstruction.NowWithExistingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NowWithRemainingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NextWithRemainingCount)]
    [TestCase(SimpleTriggerMisfireInstruction.NextWithExistingCount)]
    public void ApplyMisfireInstruction_SimpleSchedule_AppliesInstruction(SimpleTriggerMisfireInstruction instruction)
    {
        var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

        var result = MisfireInstructionHelper.ApplyMisfireInstruction(builder, instruction);

        var trigger = (ISimpleTrigger)TriggerBuilder.Create().WithSchedule(result).Build();
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(instruction));
    }

    #endregion
}
```

The deleted tests (`*_WithInvalidMisfireValue_*`, `*_WithInvalidNegativeMisfireValue_*`, `*_WithInvalidPositiveMisfireValue_*`, `ApplyMisfireInstruction_SimpleSchedule_UnknownValue_ThrowsArgumentOutOfRangeException`, `ToQuartzSchedule_WithRawIntValues_AppliesCorrectly`) tested HumanCron's own int range check, which no longer exists: the enum is the range, and Quartz 4 rejects a forged out-of-range value itself at `Build()`.

- [ ] **Step 3: Update the other test file touchpoints**

In `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs`:

Line 166, replace:
```csharp
        var cronBuilder = CronScheduleBuilder.DailyAtHourAndMinute(14, 0);
```
with:
```csharp
        var cronBuilder = CronScheduleBuilder.Create("0 0 14 * * ?");
```

In `CreateOneTimeTriggerBuilder_MisfireInstruction_AppliesToTrigger` (around lines 500-512), replace:
```csharp
        var result = _converter.CreateOneTimeTriggerBuilder(
            "2 hours", anchor, misfireInstruction: MisfireInstruction.IgnoreMisfirePolicy);
```
with:
```csharp
        var result = _converter.CreateOneTimeTriggerBuilder(
            "2 hours", anchor, misfireInstruction: SimpleTriggerMisfireInstruction.IgnoreMisfires);
```
and replace:
```csharp
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
```
with:
```csharp
        Assert.That(trigger.MisfireInstruction, Is.EqualTo(SimpleTriggerMisfireInstruction.IgnoreMisfires));
```

Then rename the trigger-builder result type everywhere in tests and source (a mechanical, safe replacement: the token `ParseResult<TriggerBuilder>` occurs only for this type):
```bash
grep -rl 'ParseResult<TriggerBuilder>' src tests | xargs sed -i 's/ParseResult<TriggerBuilder>/ParseResult<TriggerBuilder<IJob>>/g'
grep -rn 'ParseResult<TriggerBuilder>' src tests
```
Expected: the second grep prints nothing.

- [ ] **Step 4: Rewrite `MisfireInstructionHelper`**

Replace the entire content of `src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs` with:

```csharp
using Quartz;
using System;

namespace HumanCron.Quartz.Helpers;

/// <summary>
/// Applies Quartz misfire instructions to schedule builders.
/// </summary>
/// <remarks>
/// A recurring HumanCron schedule produces either a <see cref="CronScheduleBuilder"/> or a
/// <see cref="CalendarIntervalScheduleBuilder"/>, and the caller cannot know which in advance.
/// Both families share the same four policies with identical names and values
/// (SmartPolicy, IgnoreMisfires, FireAndProceed, DoNothing), so the public API accepts
/// <see cref="CronTriggerMisfireInstruction"/> and this helper casts it when the schedule
/// turns out to be calendar-interval based. Quartz validates the value when the trigger is built.
/// </remarks>
internal static class MisfireInstructionHelper
{
    /// <summary>
    /// Apply a misfire instruction to a <see cref="CronScheduleBuilder"/>.
    /// </summary>
    public static CronScheduleBuilder ApplyMisfireInstruction(
        CronScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction(misfireInstruction);
    }

    /// <summary>
    /// Apply a cron-family misfire instruction to a <see cref="CalendarIntervalScheduleBuilder"/>.
    /// The two families share names and values, so the cast is exact.
    /// </summary>
    public static CalendarIntervalScheduleBuilder ApplyMisfireInstruction(
        CalendarIntervalScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction((CalendarIntervalTriggerMisfireInstruction)misfireInstruction);
    }

    /// <summary>
    /// Apply a misfire instruction to a <see cref="SimpleScheduleBuilder"/> (one-time triggers).
    /// </summary>
    public static SimpleScheduleBuilder ApplyMisfireInstruction(
        SimpleScheduleBuilder builder,
        SimpleTriggerMisfireInstruction misfireInstruction)
    {
        return builder.WithMisfireInstruction(misfireInstruction);
    }

    /// <summary>
    /// Apply a recurring-schedule misfire instruction to whichever recurring builder was produced.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the builder is not a cron or calendar-interval builder.</exception>
    public static IScheduleBuilder ApplyMisfireInstruction(
        IScheduleBuilder builder,
        CronTriggerMisfireInstruction misfireInstruction)
    {
        return builder switch
        {
            CronScheduleBuilder cronBuilder => ApplyMisfireInstruction(cronBuilder, misfireInstruction),
            CalendarIntervalScheduleBuilder calendarBuilder => ApplyMisfireInstruction(calendarBuilder, misfireInstruction),
            _ => throw new NotSupportedException(
                $"Misfire instruction application is not supported for builder type: {builder.GetType().Name}")
        };
    }
}
```

- [ ] **Step 5: Rewrite `IQuartzScheduleConverter`**

Replace the entire content of `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs` with:

```csharp
using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;
using Quartz;
using System;

namespace HumanCron.Quartz.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and Quartz.NET schedule builders
/// </summary>
/// <remarks>
/// Handles both simple patterns (CronScheduleBuilder) and complex patterns (CalendarIntervalScheduleBuilder).
/// Provides string-to-schedule and schedule-to-string conversion for Quartz.NET integration.
///
/// Examples:
/// - "1d at 2pm" → CronScheduleBuilder.Create("0 0 14 * * ?")
/// - "2w on sunday at 3am" → CalendarIntervalScheduleBuilder with 2-week interval
/// - CronScheduleBuilder → "1d at 2pm"
/// - CalendarIntervalScheduleBuilder → "2w on sunday at 3am"
///
/// Misfire instructions: recurring schedules take a <see cref="CronTriggerMisfireInstruction"/>.
/// The caller cannot know whether a given phrase yields a cron trigger or a calendar-interval
/// trigger, and the two families share the same four policies with identical names and values,
/// so the value is applied to whichever recurring trigger family results. One-time triggers
/// always build a simple trigger and take <see cref="SimpleTriggerMisfireInstruction"/> directly.
/// </remarks>
public interface IQuartzScheduleConverter
{
    /// <summary>
    /// Convert natural language to Quartz schedule builder
    /// Returns CronScheduleBuilder for simple patterns, CalendarIntervalScheduleBuilder for complex patterns
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "2w on sunday at 3am")</param>
    /// <param name="misfireInstruction">
    /// Misfire policy (default: SmartPolicy). Applied to whichever recurring trigger family results;
    /// a calendar-interval result receives the equivalent <see cref="CalendarIntervalTriggerMisfireInstruction"/>.
    /// </param>
    /// <returns>ParseResult with IScheduleBuilder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</returns>
    /// <example>
    /// <code>
    /// // Use default misfire handling (SmartPolicy)
    /// var result = converter.ToQuartzSchedule("1d at 2pm");
    ///
    /// // Skip missed executions
    /// var result = converter.ToQuartzSchedule("1d at 2pm", CronTriggerMisfireInstruction.DoNothing);
    ///
    /// if (result is ParseResult&lt;IScheduleBuilder&gt;.Success success)
    /// {
    ///     var trigger = TriggerBuilder.Create()
    ///         .WithSchedule(success.Value)
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);

    /// <summary>
    /// Convert natural language to Quartz schedule builder, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "2w on sunday at 3am")</param>
    /// <param name="options">
    /// Parser options. Unlike the misfire-only overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself, matching System.Text.Json's
    /// JsonSerializerOptions.
    /// </param>
    /// <param name="misfireInstruction">
    /// Misfire policy (default: SmartPolicy). Applied to whichever recurring trigger family results.
    /// </param>
    /// <returns>ParseResult with IScheduleBuilder, or an Error if MinInterval is set and violated</returns>
    /// <example>
    /// <code>
    /// var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };
    /// var result = converter.ToQuartzSchedule("every 5 minutes", options); // Error: runs more often than 15 minutes
    ///
    /// if (result is ParseResult&lt;IScheduleBuilder&gt;.Success success)
    /// {
    ///     var trigger = TriggerBuilder.Create()
    ///         .WithSchedule(success.Value)
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        ScheduleParserOptions options,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);

    /// <summary>
    /// Convert Quartz schedule builder back to natural language
    /// Supports both CronScheduleBuilder and CalendarIntervalScheduleBuilder
    /// </summary>
    /// <param name="scheduleBuilder">Quartz schedule builder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</param>
    /// <returns>ParseResult with natural language schedule string</returns>
    /// <example>
    /// <code>
    /// var builder = CronScheduleBuilder.Create("0 0 14 * * ?");
    /// var result = converter.ToNaturalLanguage(builder);
    /// if (result is ParseResult&lt;string&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // "1d at 2pm"
    /// }
    /// </code>
    /// </example>
    ParseResult<string> ToNaturalLanguage(IScheduleBuilder? scheduleBuilder);

    /// <summary>
    /// Create a pre-configured TriggerBuilder with schedule, start time, and misfire handling already set
    /// Convenience method that handles start time calculation for CalendarInterval schedules automatically
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "3w on sunday at 2pm")</param>
    /// <param name="misfireInstruction">
    /// Misfire policy (default: SmartPolicy). Applied to whichever recurring trigger family results.
    /// </param>
    /// <returns>ParseResult with TriggerBuilder ready for job-specific configuration</returns>
    /// <example>
    /// <code>
    /// // Use default misfire handling (SmartPolicy)
    /// var result = converter.CreateTriggerBuilder("3w on sunday at 2pm");
    ///
    /// // Skip missed executions
    /// var result = converter.CreateTriggerBuilder("every day at 2pm", CronTriggerMisfireInstruction.DoNothing);
    ///
    /// // Fire all missed runs
    /// var result = converter.CreateTriggerBuilder("every hour", CronTriggerMisfireInstruction.IgnoreMisfires);
    ///
    /// if (result is ParseResult&lt;TriggerBuilder&lt;IJob&gt;&gt;.Success success)
    /// {
    ///     var trigger = success.Value
    ///         .WithIdentity("myTrigger", "myGroup")
    ///         .ForJob("myJob", "myJobGroup")
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);

    /// <summary>
    /// Create a pre-configured TriggerBuilder, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "3w on sunday at 2pm")</param>
    /// <param name="options">
    /// Parser options. Unlike the misfire-only overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself, matching System.Text.Json's
    /// JsonSerializerOptions.
    /// </param>
    /// <param name="misfireInstruction">
    /// Misfire policy (default: SmartPolicy). Applied to whichever recurring trigger family results.
    /// </param>
    /// <returns>ParseResult with TriggerBuilder, or an Error if MinInterval is set and violated</returns>
    /// <example>
    /// <code>
    /// var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };
    /// var result = converter.CreateTriggerBuilder("every day at 2pm", options);
    ///
    /// if (result is ParseResult&lt;TriggerBuilder&lt;IJob&gt;&gt;.Success success)
    /// {
    ///     var trigger = success.Value
    ///         .WithIdentity("myTrigger", "myGroup")
    ///         .ForJob("myJob", "myJobGroup")
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        ScheduleParserOptions options,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);

    /// <summary>
    /// Create a pre-configured one-time TriggerBuilder for a relative duration from an anchor
    /// No repeating schedule - fires exactly once at anchor + duration
    /// </summary>
    /// <param name="duration">Natural language duration (e.g., "2 hours", "1d 2h 30m")</param>
    /// <param name="anchor">Optional anchor instant (null = now)</param>
    /// <param name="timeZone">
    /// Optional timezone for DST-aware calendar (month/year) math (null = naive/offset-preserving,
    /// see HumanCron.Abstractions.IHumanDurationConverter.ToFutureTime)
    /// </param>
    /// <param name="misfireInstruction">
    /// Simple-trigger misfire policy (default: SmartPolicy). One-time triggers are always simple triggers.
    /// </param>
    /// <returns>ParseResult with TriggerBuilder pre-configured with StartAt and no repeating schedule</returns>
    /// <example>
    /// <code>
    /// // Schedule a one-time job 2 hours from now
    /// var result = converter.CreateOneTimeTriggerBuilder("2 hours");
    /// if (result is ParseResult&lt;TriggerBuilder&lt;IJob&gt;&gt;.Success success)
    /// {
    ///     var trigger = success.Value
    ///         .WithIdentity("unfreezeTrigger", "myGroup")
    ///         .ForJob("unfreezeJob", "myJobGroup")
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<TriggerBuilder<IJob>> CreateOneTimeTriggerBuilder(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null,
        SimpleTriggerMisfireInstruction misfireInstruction = SimpleTriggerMisfireInstruction.SmartPolicy);
}
```

- [ ] **Step 6: Update `QuartzScheduleConverter` signatures**

In `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs`, the `ParseResult<TriggerBuilder>` → `ParseResult<TriggerBuilder<IJob>>` rename already happened in Step 3. Now change the five `int misfireInstruction = 0` parameters. Make these exact edits:

Lines 47-49 (public `ToQuartzSchedule`), replace:
```csharp
    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        int misfireInstruction = 0)
```
with:
```csharp
    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)
```

Lines 63-65 (XML doc of the internal overload), replace:
```csharp
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (default: 0 = SmartPolicy)
    /// </param>
```
with:
```csharp
    /// <param name="misfireInstruction">
    /// Misfire policy (default: SmartPolicy), applied to whichever recurring trigger family results
    /// </param>
```

Lines 75-78 (internal `ToQuartzSchedule` with `DateTimeZone?`), replace:
```csharp
    internal ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        DateTimeZone? userTimezone,
        int misfireInstruction = 0)
```
with:
```csharp
    internal ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        DateTimeZone? userTimezone,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)
```

Lines 87-90 (options overload), replace:
```csharp
    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        int misfireInstruction = 0)
```
with:
```csharp
    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)
```

`CreateTriggerBuilder` (first overload), replace:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        int misfireInstruction = 0)
```
with:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)
```

`CreateTriggerBuilder` (options overload), replace:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        int misfireInstruction = 0)
```
with:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateTriggerBuilder(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy)
```

`CreateOneTimeTriggerBuilder`, replace:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateOneTimeTriggerBuilder(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null,
        int misfireInstruction = 0)
```
with:
```csharp
    public ParseResult<TriggerBuilder<IJob>> CreateOneTimeTriggerBuilder(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null,
        SimpleTriggerMisfireInstruction misfireInstruction = SimpleTriggerMisfireInstruction.SmartPolicy)
```

The method bodies do not change: `TriggerBuilder.Create()` still compiles (its `TimeProvider` parameter is optional) and is assigned to `var`; `MisfireInstructionHelper.ApplyMisfireInstruction(scheduleBuilder, misfireInstruction)` and `MisfireInstructionHelper.ApplyMisfireInstruction(SimpleScheduleBuilder.Create().WithRepeatCount(0), misfireInstruction)` resolve to the new overloads.

- [ ] **Step 7: Fix the two schedule builders**

`src/HumanCron.Quartz/Builders/QuartzCronBuilder.cs` line 21, replace:
```csharp
        var builder = CronScheduleBuilder.CronSchedule(cronExpression);
```
with:
```csharp
        var builder = CronScheduleBuilder.Create(cronExpression);
```

`src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs`: the file is inside `namespace HumanCron.Quartz`, so a bare `Quartz.IntervalUnit` would resolve to `HumanCron.Quartz` and fail; use an alias like the parser does. Replace the using block (lines 1-5):
```csharp
using HumanCron.Models.Internal;
using Quartz;
using System;
using HumanCron.Quartz.Helpers;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;
```
with:
```csharp
using HumanCron.Models.Internal;
using Quartz;
using System;
using HumanCron.Quartz.Helpers;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;
using QuartzIntervalUnit = Quartz.IntervalUnit;
```
and replace the interval switch (lines 42-48):
```csharp
        builder = spec.Unit switch
        {
            NaturalIntervalUnit.Weeks => builder.WithIntervalInDays(spec.Interval * 7),
            NaturalIntervalUnit.Months => builder.WithIntervalInMonths(spec.Interval),
            NaturalIntervalUnit.Years => builder.WithIntervalInYears(spec.Interval),
            _ => throw new InvalidOperationException($"CalendarInterval does not support unit: {spec.Unit}")
        };
```
with:
```csharp
        builder = spec.Unit switch
        {
            NaturalIntervalUnit.Weeks => builder.WithInterval(spec.Interval * 7, QuartzIntervalUnit.Day),
            NaturalIntervalUnit.Months => builder.WithInterval(spec.Interval, QuartzIntervalUnit.Month),
            NaturalIntervalUnit.Years => builder.WithInterval(spec.Interval, QuartzIntervalUnit.Year),
            _ => throw new InvalidOperationException($"CalendarInterval does not support unit: {spec.Unit}")
        };
```
Leave the `WORKAROUND` comment above it in place; Task 3 removes the workaround as its own change.

- [ ] **Step 8: Build and fix anything Quartz 4 renamed that this plan did not list**

Run:
```bash
dotnet build HumanCron.slnx > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task2-build.txt 2>&1; grep -E "error|Warn|Build succeeded" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task2-build.txt | sort -u
```
Expected: `Build succeeded.` with 0 warnings. If an error remains, look the old name up in the migration guide appendix (https://www.quartz-scheduler.net/documentation/quartz-4.x/migration-guide.html#appendix-what-happened-to-a-name) and apply the documented replacement in place. Do not add `#if`, reflection or suppressions. Record any such extra rename in the commit message body.

- [ ] **Step 9: Run the Quartz test suites**

Run in the background:
```bash
dotnet test HumanCron.slnx --filter "FullyQualifiedName~HumanCron.Tests.Quartz|FullyQualifiedName~QuartzScheduleConverterTests|FullyQualifiedName~TimeZone" > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task2-test.txt 2>&1
```
Then:
```bash
grep -E "Passed!|Failed!|Failed |Total tests" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task2-test.txt
```
Expected: `Passed!`, 0 failed. A failure in a round-trip, timezone or DST test is a Quartz 4 behaviour difference: read the failing assertion, reproduce it in isolation, and fix the HumanCron code or update the expectation only when the new Quartz behaviour is demonstrably correct. Do not loosen assertions.

- [ ] **Step 10: Commit**

```bash
git add Directory.Packages.props src/HumanCron.Quartz tests/HumanCron.Tests
git commit -m "feat(quartz)!: move HumanCron.Quartz to Quartz.NET 4.0.0

Misfire parameters are the Quartz 4 per-family enums: recurring methods take
CronTriggerMisfireInstruction (cast to the calendar-interval family when that
trigger results), one-time triggers take SimpleTriggerMisfireInstruction.
Trigger-builder methods return TriggerBuilder<IJob>. HumanCron no longer
range-checks misfire values; Quartz rejects an invalid code at Build().

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Emit week intervals as weeks (retire the Quartz issue 1035 workaround)

**Files:**
- Modify: `tests/HumanCron.Tests/Quartz/QuartzScheduleBuilderTests.cs:132-156, 230-279`
- Modify: `src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs:39-48`
- Modify: `src/HumanCron.Quartz/Builders/QuartzScheduleParser.cs:76-87`

**Interfaces:**
- Consumes: Task 2's `QuartzCalendarIntervalBuilder.Build(ScheduleSpec)` and `QuartzScheduleParser.ParseCalendarIntervalTrigger`.
- Produces: week specs build `RepeatIntervalUnit == IntervalUnit.Week`; the parser maps only `Week`, `Month`, `Year`.

Background: HumanCron 0.8 expressed "every N weeks" as N×7 days because Quartz 3's week interval ignored `StartAt` (quartznet/quartznet issue 1035). Against Quartz 4.0.0 a week interval respects `StartAt` (verified, see Global Constraints), so the builder can say what it means and the parser no longer needs to guess that 14 days meant 2 weeks.

- [ ] **Step 1: Change the builder test expectations (they will fail first)**

In `tests/HumanCron.Tests/Quartz/QuartzScheduleBuilderTests.cs`, in `CalendarInterval_Build3WeekInterval_SetsRepeatIntervalTo3Weeks`, replace:
```csharp
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(21));
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Day));
```
with:
```csharp
        Assert.That(calendarTrigger.RepeatInterval, Is.EqualTo(3));
        Assert.That(calendarTrigger.RepeatIntervalUnit, Is.EqualTo(QuartzIntervalUnit.Week));
```

Delete the whole test `CalendarInterval_Build3WeekInterval_ConvertsToDaysToWorkaroundQuartzBug1035` including its `/// <summary>` block (from the line `/// WORKAROUND TEST for Quartz.NET bug #1035:` through the method's closing brace). In its place add the regression test that guards the original symptom:

```csharp
    /// <summary>
    /// Quartz.NET issue 1035 (a week interval ignoring StartAt) forced HumanCron 0.8 and earlier
    /// to express week intervals as days. Quartz 4 respects StartAt for week intervals, so the
    /// builder emits weeks again. This test guards the original symptom: the first fire must be
    /// exactly the StartAt instant and the second one interval later.
    /// </summary>
    [Test]
    public void CalendarInterval_WeekIntervalWithFutureStartAt_FirstFireIsStartAt()
    {
        // Arrange
        var spec = new ScheduleSpec
        {
            Interval = 3,
            Unit = IntervalUnit.Weeks,
            TimeZone = DateTimeZone.Utc
        };
        var startAt = new DateTimeOffset(2030, 1, 6, 14, 0, 0, TimeSpan.Zero);

        // Act
        var trigger = TriggerBuilder.Create()
            .WithSchedule(new QuartzCalendarIntervalBuilder().Build(spec))
            .StartAt(startAt)
            .Build();
        var first = trigger.GetFireTimeAfter(startAt.AddSeconds(-1));
        var second = trigger.GetFireTimeAfter(first!.Value);

        // Assert
        Assert.That(first, Is.EqualTo(startAt));
        Assert.That(second, Is.EqualTo(startAt.AddDays(21)));
    }
```

- [ ] **Step 2: Run the builder tests to see them fail**

Run in the background:
```bash
dotnet test HumanCron.slnx --filter "FullyQualifiedName~QuartzScheduleBuilderTests" > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task3-red.txt 2>&1
```
Then:
```bash
grep -E "Failed |Passed!|Failed!" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task3-red.txt
```
Expected: `CalendarInterval_Build3WeekInterval_SetsRepeatIntervalTo3Weeks` fails (expected 3 but was 21). `CalendarInterval_WeekIntervalWithFutureStartAt_FirstFireIsStartAt` already passes, because a 21-day interval and a 3-week interval produce the same fire times from a fixed StartAt. That is expected: it is a regression guard for the behaviour that motivated the workaround, not the driver for this change.

- [ ] **Step 3: Make the builder emit weeks**

In `src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs`, replace:
```csharp
        // Set the interval unit and value
        // WORKAROUND: Quartz bug #1035 - WithIntervalInWeeks() ignores StartAt
        // Convert weeks to days to properly respect StartAt time
        builder = spec.Unit switch
        {
            NaturalIntervalUnit.Weeks => builder.WithInterval(spec.Interval * 7, QuartzIntervalUnit.Day),
```
with:
```csharp
        // Set the interval unit and value
        builder = spec.Unit switch
        {
            NaturalIntervalUnit.Weeks => builder.WithInterval(spec.Interval, QuartzIntervalUnit.Week),
```

- [ ] **Step 4: Remove the parser's reverse mapping**

In `src/HumanCron.Quartz/Builders/QuartzScheduleParser.cs`, replace:
```csharp
        var (interval, unit) = calendarTrigger.RepeatIntervalUnit switch
        {
            // WORKAROUND: Quartz bug #1035 - we convert weeks to days in the builder
            // When parsing back, recognize day intervals that are multiples of 7 as weeks
            QuartzIntervalUnit.Day when calendarTrigger.RepeatInterval % 7 == 0
                => (calendarTrigger.RepeatInterval / 7, NaturalIntervalUnit.Weeks),

            QuartzIntervalUnit.Week => (calendarTrigger.RepeatInterval, NaturalIntervalUnit.Weeks),
```
with:
```csharp
        var (interval, unit) = calendarTrigger.RepeatIntervalUnit switch
        {
            QuartzIntervalUnit.Week => (calendarTrigger.RepeatInterval, NaturalIntervalUnit.Weeks),
```

- [ ] **Step 5: Run the Quartz suites green**

Run in the background:
```bash
dotnet test HumanCron.slnx --filter "FullyQualifiedName~HumanCron.Tests.Quartz|FullyQualifiedName~QuartzScheduleConverterTests|FullyQualifiedName~RoundTrip" > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task3-green.txt 2>&1
```
Then:
```bash
grep -E "Passed!|Failed!|Failed " /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task3-green.txt
```
Expected: `Passed!`, 0 failed. If a round-trip test that fed a day-based calendar trigger fails, it was relying on the reverse mapping; update it to build the trigger with `IntervalUnit.Week`, since day-based multi-week triggers are no longer something HumanCron produces.

- [ ] **Step 6: Confirm no workaround references remain in code or tests**

```bash
git grep -n -i '1035\|weeks to days\|weeks converted' -- src tests
```
Expected: no output.

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron.Quartz/Builders/QuartzCalendarIntervalBuilder.cs src/HumanCron.Quartz/Builders/QuartzScheduleParser.cs tests/HumanCron.Tests/Quartz/QuartzScheduleBuilderTests.cs
git commit -m "fix(quartz): emit week intervals as weeks now that Quartz 4 honours StartAt

Retires the days-multiple-of-7 workaround for quartznet/quartznet#1035 in the
builder and its reverse mapping in the parser; adds a StartAt regression guard.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Version 0.9.0 and documentation

**Files:**
- Modify: `Directory.Build.props:14`
- Modify: `src/HumanCron.Quartz/HumanCron.Quartz.csproj:6`
- Modify: `README.md:33-34, 161-186`
- Modify: `INTEGRATION.md:696-702, 755-767`
- Modify: `ARCHITECTURE.md:234-235`

**Interfaces:**
- Consumes: the public API from Task 2.
- Produces: docs that match it.

- [ ] **Step 1: Bump the version**

In `Directory.Build.props`, replace:
```xml
    <Version>0.8.0</Version>
```
with:
```xml
    <Version>0.9.0</Version>
```

- [ ] **Step 2: Package description**

In `src/HumanCron.Quartz/HumanCron.Quartz.csproj`, in the `<Description>` element, replace the trailing sentence:
```
Requires exact version match with HumanCron core package.</Description>
```
with:
```
Requires Quartz.NET 4.x (Quartz 3 users: stay on HumanCron.Quartz 0.8.0) and an exact version match with the HumanCron core package.</Description>
```

- [ ] **Step 3: README**

In `README.md`, replace:
```bash
# Quartz.NET integration (optional)
dotnet add package HumanCron.Quartz
```
with:
```bash
# Quartz.NET integration (optional) - requires Quartz.NET 4.x; Quartz 3 users pin HumanCron.Quartz 0.8.0
dotnet add package HumanCron.Quartz
```

Replace the Quartz.NET Integration example block:
```csharp
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
with:
```csharp
// Multi-week patterns use CalendarIntervalScheduleBuilder
var triggerResult = converter.CreateTriggerBuilder("every 3 weeks on sunday at 12am");
if (triggerResult is ParseResult<TriggerBuilder<IJob>>.Success triggerSuccess)
{
    var trigger = triggerSuccess.Value
        .WithIdentity("my-trigger")
        .ForJob("my-job")
        .Build();
}

// Misfire policy: pass the Quartz cron-family enum; it is applied to whichever
// trigger family (cron or calendar-interval) the phrase produces
var skipMissed = converter.CreateTriggerBuilder("every day at 2pm", CronTriggerMisfireInstruction.DoNothing);
```

- [ ] **Step 4: INTEGRATION.md**

Replace the Quartz lines in the options-taking overloads block:
```csharp
ParseResult<IScheduleBuilder> IQuartzScheduleConverter.ToQuartzSchedule(
    string naturalLanguage, ScheduleParserOptions options, int misfireInstruction = 0);

ParseResult<TriggerBuilder> IQuartzScheduleConverter.CreateTriggerBuilder(
    string naturalLanguage, ScheduleParserOptions options, int misfireInstruction = 0);
```
with:
```csharp
ParseResult<IScheduleBuilder> IQuartzScheduleConverter.ToQuartzSchedule(
    string naturalLanguage, ScheduleParserOptions options,
    CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);

ParseResult<TriggerBuilder<IJob>> IQuartzScheduleConverter.CreateTriggerBuilder(
    string naturalLanguage, ScheduleParserOptions options,
    CronTriggerMisfireInstruction misfireInstruction = CronTriggerMisfireInstruction.SmartPolicy);
```

Immediately after the paragraph that begins `Unlike the `DateTimeZone?` overloads, `options.TimeZone` is used exactly as given` (and before the `#### Rejecting Schedules That Fire Too Often (MinInterval)` heading), insert:

```markdown
#### Quartz misfire instructions

HumanCron.Quartz 0.9+ targets Quartz.NET 4.x, whose misfire vocabulary is the per-family enums
(`CronTriggerMisfireInstruction`, `CalendarIntervalTriggerMisfireInstruction`,
`SimpleTriggerMisfireInstruction`). A natural-language phrase may produce either a cron trigger or a
calendar-interval trigger, and you cannot tell which in advance, so the recurring methods take
`CronTriggerMisfireInstruction` and apply it to whichever family results. The two families share
the same four members with identical values, so nothing is lost in the mapping. One-time triggers
(`CreateOneTimeTriggerBuilder`) always build a simple trigger and take `SimpleTriggerMisfireInstruction`.

Quartz 3 consumers should stay on HumanCron.Quartz 0.8.0 (and therefore HumanCron 0.8.0).
```

In the "Quartz one-time triggers" section, replace:
```csharp
if (result is ParseResult<TriggerBuilder>.Success success)
```
with:
```csharp
if (result is ParseResult<TriggerBuilder<IJob>>.Success success)
```

- [ ] **Step 5: ARCHITECTURE.md**

Replace:
```
/// - "every day at 2pm" → CronScheduleBuilder.DailyAtHourAndMinute(14, 0)
/// - "every 2 weeks on sunday at 3am" → CalendarIntervalScheduleBuilder.Create().WithIntervalInWeeks(2)...
```
with:
```
/// - "every day at 2pm" → CronScheduleBuilder.Create("0 0 14 * * ?")
/// - "every 2 weeks on sunday at 3am" → CalendarIntervalScheduleBuilder.Create().WithInterval(2, IntervalUnit.Week)...
```

- [ ] **Step 6: Check nothing stale is left**

```bash
git grep -n 'ParseResult<TriggerBuilder>\|MisfireInstruction\.CronTrigger\|MisfireInstruction\.CalendarIntervalTrigger\|MisfireInstruction\.SimpleTrigger\|IgnoreMisfirePolicy\|DailyAtHourAndMinute\|WithIntervalIn\|int misfireInstruction' -- ':!docs/superpowers'
```
Expected: no output. Fix any hit in place.

- [ ] **Step 7: Build and pack to prove the packages still produce**

```bash
dotnet pack HumanCron.slnx -c Release -o /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/nupkg > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task4-pack.txt 2>&1; grep -E "error|Successfully created|Build succeeded" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/task4-pack.txt
ls /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/nupkg
unzip -p /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/nupkg/HumanCron.Quartz.0.9.0.nupkg HumanCron.Quartz.nuspec | grep -E 'dependency id="(Quartz|HumanCron)"'
```
Expected: four `0.9.0` packages (HumanCron, HumanCron.Quartz, HumanCron.NCrontab, HumanCron.Hangfire) plus symbol packages; the Quartz nuspec shows `Quartz` `4.0.0` and `HumanCron` `[0.9.0]`.

- [ ] **Step 8: Commit**

```bash
git add Directory.Build.props src/HumanCron.Quartz/HumanCron.Quartz.csproj README.md INTEGRATION.md ARCHITECTURE.md
git commit -m "chore: bump version to 0.9.0 and document the Quartz 4 API

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Full-suite verification

**Files:** none modified unless a failure is found.

- [ ] **Step 1: Run the whole solution once, in the background**

```bash
dotnet test HumanCron.slnx > /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/final-test.txt 2>&1
```

- [ ] **Step 2: Read the result from the file**

```bash
grep -E "Passed!|Failed!|Total tests|Failed |Skipped" /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/final-test.txt
grep -c "Failed " /tmp/claude-1000/-DataPool-Repos-kass-HumanCron/606ca28b-7dbc-4534-8372-02597c7c2d56/scratchpad/final-test.txt
```
Expected: `Passed!`, 0 failed, 0 skipped. Compare the total against Task 1's total: it should be lower by the count of deleted misfire tests plus the deleted workaround test, and higher by the new TestCase rows and the StartAt guard. Any unexpected difference is investigated.

- [ ] **Step 3: Review the branch diff as a whole**

```bash
git log --oneline master..HEAD
git diff master..HEAD --stat
```
Expected commits, oldest first: spec (3 commits), deps bump, Quartz 4 move, weeks fix, version+docs. Read `git diff master..HEAD -- src` once end to end and confirm: no `#if`, no reflection, no leftover `WORKAROUND` comment, no `int misfireInstruction`.

- [ ] **Step 4: Report**

Do not push and do not open a PR. Report to the owner: the commit list, the final test totals, and any Quartz 4 renames that Step 8 of Task 2 had to handle beyond those listed in this plan.
