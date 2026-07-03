# MinInterval Floor Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let callers set a `MinInterval` floor (via `ScheduleParserOptions`) so schedules parsed by any HumanCron converter that fire more often than the floor are rejected with a clear error, instead of only being bounded by the current unit-blind 1–1000 interval check.

**Architecture:** A new internal pure function, `TightestGapCalculator.Calculate(ScheduleSpec) -> TimeSpan`, computes the tightest possible gap between consecutive firings for any parsed schedule. `NaturalLanguageParser.Parse` calls it once, right before returning `Success`, when `options.MinInterval` is set — every converter (Unix, Quartz, NCrontab) inherits the check for free because they all funnel through the same `Parse` call. `ScheduleParserOptions` becomes a `record` and gains `MinInterval`, and each converter gets one new additive overload taking `ScheduleParserOptions` directly (mirroring `JsonSerializerOptions`) — no DI plumbing, no `IOptions<T>`, no new package.

**Tech Stack:** C# / .NET 10, NUnit, NodaTime. Spec: `docs/superpowers/specs/2026-07-02-min-interval-floor-design.md`.

## Global Constraints

- **Inclusive floor boundary**: a schedule is valid when `tightestGap >= MinInterval`; rejected only when `tightestGap < MinInterval`. Equal is always allowed.
- **No new dependencies**: no `Microsoft.Extensions.Options`, no `IOptions<T>`, no `AddHumanCron(configure)` DI changes.
- **Additive API only**: every new overload sits alongside existing overloads; nothing is removed or renamed. Existing 1011 tests must keep passing unchanged.
- **`MinInterval = null` (default)**: never triggers the check — zero behavior change for existing callers who don't opt in.
- **`MinInterval = TimeSpan.Zero` or negative**: mathematically inert (`gap < 0` or `gap < TimeSpan.Zero` is never true for a valid gap), not specially validated or rejected.
- **Error format** (exact, safe to show in a UI): `'{naturalLanguage}' runs more often than the minimum allowed interval of {floor}`.
- **`ScheduleParserOptions.TimeZone` semantics differ slightly between overloads by design**: the existing `DateTimeZone?`-taking overloads keep "null → converter's configured local timezone" behavior. The new `ScheduleParserOptions`-taking overloads use `options.TimeZone` exactly as given (default = system timezone) — same as `JsonSerializerOptions`, there is no hidden per-converter fallback once you pass the options object yourself. This only differs from today's behavior for DI-registered converters whose injected `DateTimeZone` differs from the system default; for the common `Create()` factory path they're identical.
- **`MaxInterval` ceiling is out of scope** — confirmed dropped during brainstorming; do not add it as part of this work.

---

### Task 1: `ScheduleParserOptions` becomes a record, gains `MinInterval`

**Files:**
- Modify: `src/HumanCron/Parsing/ScheduleParserOptions.cs`
- Test: `tests/HumanCron.Tests/Parsing/ScheduleParserOptionsTests.cs` (new file)

**Interfaces:**
- Produces: `public sealed record ScheduleParserOptions { DateTimeZone TimeZone { get; init; } TimeSpan? MinInterval { get; init; } }` — every later task constructs and reads this type.

- [ ] **Step 1: Write the failing test**

Create `tests/HumanCron.Tests/Parsing/ScheduleParserOptionsTests.cs`:

```csharp
using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class ScheduleParserOptionsTests
{
    [Test]
    public void MinInterval_DefaultsToNull()
    {
        var options = new ScheduleParserOptions();

        Assert.That(options.MinInterval, Is.Null);
    }

    [Test]
    public void WithExpression_OverridesMinIntervalWithoutMutatingOriginal()
    {
        var defaults = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var perCall = defaults with { MinInterval = TimeSpan.FromMinutes(5) };

        Assert.That(defaults.MinInterval, Is.EqualTo(TimeSpan.FromMinutes(15)));
        Assert.That(perCall.MinInterval, Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(perCall.TimeZone, Is.EqualTo(defaults.TimeZone));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~ScheduleParserOptionsTests"`
Expected: FAIL to compile — `with` expression is not valid on a non-record `ScheduleParserOptions` (a `sealed class` has no synthesized `Clone`/`with` support), so this fails as a build error rather than a runtime assertion failure.

- [ ] **Step 3: Convert `ScheduleParserOptions` to a record and add `MinInterval`**

Replace the full contents of `src/HumanCron/Parsing/ScheduleParserOptions.cs`:

```csharp
using NodaTime;

namespace HumanCron.Parsing;

/// <summary>
/// Options for parsing natural language schedules
/// </summary>
public sealed record ScheduleParserOptions
{
    /// <summary>
    /// The timezone for interpreting times in the schedule
    /// Default: Local system timezone (via DateTimeZoneProviders.Tzdb.GetSystemDefault())
    /// Note: Default is evaluated at instance creation time
    ///
    /// For Unix cron: Times are converted to this timezone for output
    /// For Quartz: This timezone is applied via .InTimeZone() for DST-aware scheduling
    ///
    /// Examples:
    /// - Local (default): "1d at 2pm" runs at 2pm in server's timezone
    /// - User timezone: "1d at 2pm" runs at 2pm in user's timezone (converted to server time for Unix cron)
    /// - UTC: "1d at 2pm" runs at 2pm UTC
    ///
    /// Use IANA timezone IDs (e.g., "America/New_York", "Europe/London")
    /// Get timezone: DateTimeZoneProviders.Tzdb["America/New_York"]
    /// </summary>
    public DateTimeZone TimeZone { get; init; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();

    /// <summary>
    /// Optional floor on the tightest allowed gap between consecutive firings.
    /// Null (default) = no floor, matching today's behavior.
    ///
    /// Inclusive boundary: a schedule is valid when its tightest gap is greater than or
    /// equal to MinInterval. Only schedules that fire strictly more often than this are
    /// rejected. For example, with MinInterval = 15 minutes, "every 15 minutes" is allowed
    /// and "every 5 minutes" is rejected.
    ///
    /// Reuse a shared instance across calls (like System.Text.Json's JsonSerializerOptions)
    /// rather than looking for a global/DI-configured default - there isn't one:
    /// <code>
    /// private static readonly ScheduleParserOptions Defaults = new() { MinInterval = TimeSpan.FromMinutes(15) };
    /// converter.ToCron(text, Defaults);
    /// converter.ToCron(text, Defaults with { MinInterval = TimeSpan.FromMinutes(5) }); // per-call override
    /// </code>
    /// </summary>
    public TimeSpan? MinInterval { get; init; }
}
```

Note the added `using System;` is not needed — `TimeSpan` resolves via the implicit global usings already generated for this SDK-style project (confirmed by `HumanCron.Tests.GlobalUsings.g.cs` existing for the test project; the same applies to `src/HumanCron`). If the build fails with `CS0246: The type or namespace name 'TimeSpan' could not be found`, add `using System;` at the top of the file.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~ScheduleParserOptionsTests"`
Expected: PASS (2 tests)

- [ ] **Step 5: Run the full suite to confirm no regressions from the class->record change**

Run: `dotnet test HumanCron.slnx`
Expected: PASS (1011 existing + 2 new = 1013)

- [ ] **Step 6: Commit**

```bash
git add src/HumanCron/Parsing/ScheduleParserOptions.cs tests/HumanCron.Tests/Parsing/ScheduleParserOptionsTests.cs
git commit -m "feat: add MinInterval to ScheduleParserOptions, convert to record"
```

---

### Task 2: `TightestGapCalculator` — plain intervals and at-most-daily shapes

**Files:**
- Create: `src/HumanCron/Validation/TightestGapCalculator.cs`
- Test: `tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs` (new file)

**Interfaces:**
- Consumes: `HumanCron.Models.Internal.ScheduleSpec` (internal, already exists), `HumanCron.Models.Internal.IntervalUnit` (internal, already exists).
- Produces: `internal static class TightestGapCalculator { internal static TimeSpan Calculate(ScheduleSpec spec); }` — Task 3 extends this same method: Task 4 calls it.

- [ ] **Step 1: Write the failing tests**

Create `tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs`:

```csharp
using HumanCron.Models.Internal;
using HumanCron.Validation;

namespace HumanCron.Tests.Validation;

[TestFixture]
public class TightestGapCalculatorTests
{
    private static ScheduleSpec PlainInterval(int interval, IntervalUnit unit) =>
        new() { Interval = interval, Unit = unit };

    [TestCase(30, IntervalUnit.Seconds, 30)]
    [TestCase(15, IntervalUnit.Minutes, 15 * 60)]
    [TestCase(6, IntervalUnit.Hours, 6 * 3600)]
    [TestCase(1, IntervalUnit.Days, 24 * 3600)]
    [TestCase(2, IntervalUnit.Weeks, 2 * 7 * 24 * 3600)]
    [TestCase(3, IntervalUnit.Months, 3 * 28 * 24 * 3600)]
    [TestCase(1, IntervalUnit.Years, 365 * 24 * 3600)]
    public void Calculate_PlainInterval_ReturnsIntervalTimesUnitLength(int interval, IntervalUnit unit, int expectedSeconds)
    {
        var spec = PlainInterval(interval, unit);

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }

    [Test]
    public void Calculate_DayOfWeek_ReturnsFlat24Hours()
    {
        // "1w on monday" - Interval/Unit alone would say 7 days, but the DayOfWeek
        // constraint is what actually governs an at-most-daily gap.
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Weeks, DayOfWeek = DayOfWeek.Monday };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_WeekdayPattern_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Days, DayPattern = DayPattern.Weekdays };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_DayOfMonth_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Months, DayOfMonth = 15 };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_LastDayOfMonth_ReturnsFlat24Hours()
    {
        var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Months, IsLastDay = true };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~TightestGapCalculatorTests"`
Expected: FAIL to compile — `HumanCron.Validation.TightestGapCalculator` does not exist yet.

- [ ] **Step 3: Create the calculator (plain interval + at-most-daily branches only)**

Create `src/HumanCron/Validation/TightestGapCalculator.cs`:

```csharp
using System;
using HumanCron.Models.Internal;

namespace HumanCron.Validation;

/// <summary>
/// Computes the tightest possible gap between consecutive firings of a parsed schedule.
/// INTERNAL: Used by NaturalLanguageParser to enforce ScheduleParserOptions.MinInterval.
/// Pessimistic by design: when a shape's true tightest gap can't be pinned down exactly,
/// this returns a value no larger than reality, since underestimating the gap is the safe
/// error direction for a floor (it only over-rejects, never lets a too-fast schedule through).
/// </summary>
internal static class TightestGapCalculator
{
    internal static TimeSpan Calculate(ScheduleSpec spec)
    {
        if (IsAtMostDaily(spec))
        {
            return TimeSpan.FromHours(24);
        }

        return PlainIntervalGap(spec.Interval, spec.Unit);
    }

    // Day-of-week/day-of-month/weekday constraints can never fire twice on the same
    // calendar day, so 24h is an exact lower bound here, not just a pessimistic guess.
    private static bool IsAtMostDaily(ScheduleSpec spec) =>
        spec.DayOfWeek.HasValue
        || spec.DayPattern.HasValue
        || spec.DayOfWeekList is { Count: > 0 }
        || spec.DayOfWeekStart.HasValue
        || spec.DayOfMonth.HasValue
        || spec.IsLastDay
        || spec.IsLastDayOfWeek
        || spec.IsNearestWeekday
        || spec.NthOccurrence.HasValue
        || spec.DayList is { Count: > 0 }
        || spec.DayStart.HasValue;

    private static TimeSpan PlainIntervalGap(int interval, IntervalUnit unit) => unit switch
    {
        IntervalUnit.Seconds => TimeSpan.FromSeconds(interval),
        IntervalUnit.Minutes => TimeSpan.FromMinutes(interval),
        IntervalUnit.Hours => TimeSpan.FromHours(interval),
        IntervalUnit.Days => TimeSpan.FromDays(interval),
        IntervalUnit.Weeks => TimeSpan.FromDays(interval * 7),
        // Months/years approximate to the shortest possible calendar length (28d/365d) -
        // underestimating is the safe direction for a floor. Only matters above ~4 weeks.
        IntervalUnit.Months => TimeSpan.FromDays(interval * 28),
        IntervalUnit.Years => TimeSpan.FromDays(interval * 365),
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~TightestGapCalculatorTests"`
Expected: PASS (11 tests)

- [ ] **Step 5: Commit**

```bash
git add src/HumanCron/Validation/TightestGapCalculator.cs tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs
git commit -m "feat: add TightestGapCalculator for plain interval and at-most-daily shapes"
```

---

### Task 3: `TightestGapCalculator` — minute/hour list and range/step shapes

**Files:**
- Modify: `src/HumanCron/Validation/TightestGapCalculator.cs`
- Modify: `tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs`

**Interfaces:**
- Produces: `TightestGapCalculator.Calculate` now also handles `MinuteList`, `MinuteStart`/`MinuteEnd`/`MinuteStep`, `HourList`, `HourStart`/`HourEnd`/`HourStep` — this is the complete, final behavior of `Calculate` that Task 4 wires up.

- [ ] **Step 1: Write the failing tests**

Add to `tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs` (inside the existing `TightestGapCalculatorTests` class):

```csharp
    [Test]
    public void Calculate_MinuteListEvenlySpaced_ReturnsMinAdjacentDifference()
    {
        // "at minutes 0,15,30,45" - evenly spaced, tightest gap is 15 minutes both
        // forward and via wraparound (45 -> 60/0 is also 15).
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [0, 15, 30, 45]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }

    [Test]
    public void Calculate_MinuteListUnevenlySpaced_ReturnsSmallestGap()
    {
        // "at minutes 0,10,20,30,40,50" - every 10 minutes
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [0, 10, 20, 30, 40, 50]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Calculate_MinuteListSingleEntry_ReturnsFullHourCycle()
    {
        // "at minutes 30" - fires once per hour. Min-adjacent-difference is undefined
        // on a single-element list; this must not fall through to a gap of zero.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [30]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(60)));
    }

    [Test]
    public void Calculate_MinuteListWraparound_ReturnsWraparoundGap()
    {
        // "at minutes 5,55" - 55 -> 5 wrapping past the hour is 10 minutes, tighter
        // than the forward gap of 50 minutes.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Hours,
            MinuteList = [5, 55]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Calculate_MinuteListKnownPessimisticWraparound_OverRejectsContrivedPattern()
    {
        // Documented, accepted imprecision from the design: "at minutes 0,59 at hours 9
        // and 15" computes a 1-minute wraparound gap (0 and 59 are adjacent mod 60) even
        // though hours 9 and 15 are not themselves adjacent, so the true tightest gap is
        // 59 minutes. This is safe (over-rejects, never under-rejects) but is worth a
        // test that pins the current, intentionally conservative behavior.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteList = [0, 59],
            HourList = [9, 15]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Calculate_MinuteRangeWithStep_ReturnsStepValue()
    {
        // "every 5 minutes between 0 and 30 of each hour"
        var spec = new ScheduleSpec
        {
            Interval = 5,
            Unit = IntervalUnit.Minutes,
            MinuteStart = 0,
            MinuteEnd = 30,
            MinuteStep = 5
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void Calculate_MinuteRangeWithoutExplicitStep_DefaultsToOneMinute()
    {
        // "every day between minutes 0 and 30" - a bare cron minute range with no
        // step fires every minute in the window (cron "0-30 * * * *").
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteStart = 0,
            MinuteEnd = 30
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Calculate_HourListEvenlySpaced_ReturnsMinAdjacentDifference()
    {
        // "at hours 0,6,12,18"
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourList = [0, 6, 12, 18]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(6)));
    }

    [Test]
    public void Calculate_HourListSingleEntry_ReturnsFullDayCycle()
    {
        // "at hours 9" - fires once per day
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourList = [9]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void Calculate_HourRangeWithStep_ReturnsStepValue()
    {
        // "every 2 hours between 9am and 5pm of each day"
        var spec = new ScheduleSpec
        {
            Interval = 2,
            Unit = IntervalUnit.Hours,
            HourStart = 9,
            HourEnd = 17,
            HourStep = 2
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void Calculate_HourRangeWithoutExplicitStep_DefaultsToOneHour()
    {
        // "every day between hours 9 and 17" - fires once per hour in the window
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            HourStart = 9,
            HourEnd = 17
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void Calculate_MinuteListTakesPrecedenceOverHourList()
    {
        // A minute-level list is a finer-grained signal than an hour list; the
        // calculator must pick the tightest (most sub-day-capable) evidence present.
        var spec = new ScheduleSpec
        {
            Interval = 1,
            Unit = IntervalUnit.Days,
            MinuteList = [0, 20, 40],
            HourList = [9]
        };

        var gap = TightestGapCalculator.Calculate(spec);

        Assert.That(gap, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~TightestGapCalculatorTests"`
Expected: FAIL — the new list/range branches don't exist yet, so these fall through to the plain-interval or at-most-daily path and produce wrong values (e.g. 24h instead of 15m).

- [ ] **Step 3: Extend the calculator with minute/hour list and range branches**

Replace the body of `Calculate` and add the new private helpers in `src/HumanCron/Validation/TightestGapCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;
using HumanCron.Models.Internal;

namespace HumanCron.Validation;

/// <summary>
/// Computes the tightest possible gap between consecutive firings of a parsed schedule.
/// INTERNAL: Used by NaturalLanguageParser to enforce ScheduleParserOptions.MinInterval.
/// Pessimistic by design: when a shape's true tightest gap can't be pinned down exactly,
/// this returns a value no larger than reality, since underestimating the gap is the safe
/// error direction for a floor (it only over-rejects, never lets a too-fast schedule through).
/// </summary>
internal static class TightestGapCalculator
{
    internal static TimeSpan Calculate(ScheduleSpec spec)
    {
        if (spec.MinuteList is { Count: > 0 } minuteList)
        {
            return MinuteListGap(minuteList);
        }

        if (spec.MinuteStart.HasValue)
        {
            return TimeSpan.FromMinutes(spec.MinuteStep ?? 1);
        }

        if (spec.HourList is { Count: > 0 } hourList)
        {
            return HourListGap(hourList);
        }

        if (spec.HourStart.HasValue)
        {
            return TimeSpan.FromHours(spec.HourStep ?? 1);
        }

        if (IsAtMostDaily(spec))
        {
            return TimeSpan.FromHours(24);
        }

        return PlainIntervalGap(spec.Interval, spec.Unit);
    }

    // Day-of-week/day-of-month/weekday constraints can never fire twice on the same
    // calendar day, so 24h is an exact lower bound here, not just a pessimistic guess.
    private static bool IsAtMostDaily(ScheduleSpec spec) =>
        spec.DayOfWeek.HasValue
        || spec.DayPattern.HasValue
        || spec.DayOfWeekList is { Count: > 0 }
        || spec.DayOfWeekStart.HasValue
        || spec.DayOfMonth.HasValue
        || spec.IsLastDay
        || spec.IsLastDayOfWeek
        || spec.IsNearestWeekday
        || spec.NthOccurrence.HasValue
        || spec.DayList is { Count: > 0 }
        || spec.DayStart.HasValue;

    private static TimeSpan PlainIntervalGap(int interval, IntervalUnit unit) => unit switch
    {
        IntervalUnit.Seconds => TimeSpan.FromSeconds(interval),
        IntervalUnit.Minutes => TimeSpan.FromMinutes(interval),
        IntervalUnit.Hours => TimeSpan.FromHours(interval),
        IntervalUnit.Days => TimeSpan.FromDays(interval),
        IntervalUnit.Weeks => TimeSpan.FromDays(interval * 7),
        // Months/years approximate to the shortest possible calendar length (28d/365d) -
        // underestimating is the safe direction for a floor. Only matters above ~4 weeks.
        IntervalUnit.Months => TimeSpan.FromDays(interval * 28),
        IntervalUnit.Years => TimeSpan.FromDays(interval * 365),
    };

    private static TimeSpan MinuteListGap(IReadOnlyList<int> sortedMinutes) =>
        TimeSpan.FromMinutes(sortedMinutes.Count == 1 ? 60 : CyclicMinGap(sortedMinutes, 60));

    private static TimeSpan HourListGap(IReadOnlyList<int> sortedHours) =>
        TimeSpan.FromHours(sortedHours.Count == 1 ? 24 : CyclicMinGap(sortedHours, 24));

    // sortedValues must already be sorted ascending (ParseListNotation guarantees this).
    private static int CyclicMinGap(IReadOnlyList<int> sortedValues, int cycleLength)
    {
        var minGap = cycleLength - sortedValues[^1] + sortedValues[0]; // wraparound baseline
        for (var i = 1; i < sortedValues.Count; i++)
        {
            minGap = Math.Min(minGap, sortedValues[i] - sortedValues[i - 1]);
        }

        return minGap;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~TightestGapCalculatorTests"`
Expected: PASS (23 tests)

- [ ] **Step 5: Commit**

```bash
git add src/HumanCron/Validation/TightestGapCalculator.cs tests/HumanCron.Tests/Validation/TightestGapCalculatorTests.cs
git commit -m "feat: extend TightestGapCalculator with minute/hour list and range shapes"
```

---

### Task 4: Wire the floor check into `NaturalLanguageParser.Parse`

**Files:**
- Modify: `src/HumanCron/Parsing/NaturalLanguageParser.cs`
- Modify: `src/HumanCron/Parsing/NaturalLanguageParser.Helpers.cs` (add `FormatFloor`)
- Test: `tests/HumanCron.Tests/Parsing/MinIntervalFloorTests.cs` (new file)

**Interfaces:**
- Consumes: `TightestGapCalculator.Calculate(ScheduleSpec)` (Task 3), `ScheduleParserOptions.MinInterval` (Task 1).
- Produces: `NaturalLanguageParser.Parse(string, ScheduleParserOptions)` now returns `ParseResult<ScheduleSpec>.Error` when the floor is violated — this is the behavior every converter task (5-7) relies on and tests against through the public API.

- [ ] **Step 1: Write the failing tests**

Create `tests/HumanCron.Tests/Parsing/MinIntervalFloorTests.cs`:

```csharp
using HumanCron.Models;
using HumanCron.Models.Internal;
using HumanCron.Parsing;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class MinIntervalFloorTests
{
    private readonly NaturalLanguageParser _parser = new();

    [Test]
    public void Parse_NoMinInterval_AlwaysSucceedsRegardlessOfSpeed()
    {
        var result = _parser.Parse("every 1 seconds", new ScheduleParserOptions());

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_GapExactlyAtFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 15 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A gap exactly equal to the floor must be allowed (inclusive boundary)");
    }

    [Test]
    public void Parse_GapOneUnitBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 14 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
    }

    [Test]
    public void Parse_GapOneUnitAboveFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 16 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_ViolatingFloor_ReturnsExactErrorMessage()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 5 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message,
            Is.EqualTo("'every 5 minutes' runs more often than the minimum allowed interval of 15 minutes"));
    }

    [Test]
    public void Parse_MinuteListAtFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every hour at minutes 0,15,30,45", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>());
    }

    [Test]
    public void Parse_MinuteListBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every hour at minutes 0,10,20,30,40,50", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
    }

    [Test]
    public void Parse_RangeStepBurstBelowFloor_Fails()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _parser.Parse("every 5 minutes between 0 and 30 of each hour", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>(),
            "Bursts every 5 minutes within the window must be rejected even though the overall pattern is not a plain 5-minute interval");
    }

    [Test]
    public void Parse_WeekdayPatternAtTwentyFourHourFloor_Succeeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromHours(24) };

        var result = _parser.Parse("every weekday at 9am", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "At-most-daily patterns must be allowed at exactly a 24-hour floor (inclusive boundary)");
    }

    [Test]
    public void Parse_ZeroMinInterval_AlwaysSucceeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.Zero };

        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A zero floor forbids nothing");
    }

    [Test]
    public void Parse_NegativeMinInterval_AlwaysSucceeds()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(-5) };

        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Success>(),
            "A negative floor is inert - gap < negative is never true - not validated against");
    }

    [TestCase(30, "30 seconds")]
    [TestCase(60, "1 minute")]
    [TestCase(6 * 3600, "6 hours")]
    [TestCase(2 * 24 * 3600, "2 days")]
    public void Parse_ErrorMessage_FormatsFloorInLargestWholeUnit(int floorSeconds, string expectedFloorPhrase)
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromSeconds(floorSeconds) };

        // "every 1 seconds" is faster than every floor value under test, so this is
        // guaranteed to fail and exercise FormatFloor's seconds/minutes/hours/days branches.
        var result = _parser.Parse("every 1 seconds", options);

        Assert.That(result, Is.TypeOf<ParseResult<ScheduleSpec>.Error>());
        var error = (ParseResult<ScheduleSpec>.Error)result;
        Assert.That(error.Message, Does.Contain(expectedFloorPhrase));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~MinIntervalFloorTests"`
Expected: FAIL — every test that expects an `Error` result currently gets `Success`, since `Parse` doesn't check `MinInterval` yet.

- [ ] **Step 3: Add `FormatFloor` helper**

Add to `src/HumanCron/Parsing/NaturalLanguageParser.Helpers.cs`, inside the existing `NaturalLanguageParser` partial class (after `ParseHour`):

```csharp
    /// <summary>
    /// Format a TimeSpan floor value for the MinInterval violation error message, in the
    /// largest whole unit that evenly divides it (e.g. 15 minutes, 2 hours, 1 day).
    /// Not a general-purpose duration formatter - scoped to this one error message.
    /// </summary>
    private static string FormatFloor(TimeSpan floor)
    {
        if (floor.Ticks % TimeSpan.TicksPerDay == 0 && floor.Ticks / TimeSpan.TicksPerDay >= 1)
        {
            var days = floor.Ticks / TimeSpan.TicksPerDay;
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (floor.Ticks % TimeSpan.TicksPerHour == 0 && floor.Ticks / TimeSpan.TicksPerHour >= 1)
        {
            var hours = floor.Ticks / TimeSpan.TicksPerHour;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        if (floor.Ticks % TimeSpan.TicksPerMinute == 0 && floor.Ticks / TimeSpan.TicksPerMinute >= 1)
        {
            var minutes = floor.Ticks / TimeSpan.TicksPerMinute;
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        var seconds = (long)floor.TotalSeconds;
        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }
```

- [ ] **Step 4: Wire the check into `Parse`**

In `src/HumanCron/Parsing/NaturalLanguageParser.cs`, replace the final block:

```csharp
        return new ParseResult<ScheduleSpec>.Success(new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfMonth = dayOfMonth,
            DayOfWeekList = dayOfWeekList,
            DayOfWeekStart = dayOfWeekStart,
            DayOfWeekEnd = dayOfWeekEnd,
            Month = monthSpecifier,
            TimeOfDay = timeOfDay,
            TimeZone = options.TimeZone,
            IsLastDay = isLastDay,
            IsLastDayOfWeek = isLastDayOfWeek,
            LastDayOffset = lastDayOffset,
            IsNearestWeekday = isNearestWeekday,
            NthOccurrence = nthOccurrence,
            MinuteList = minuteList,
            MinuteStart = minuteStart,
            MinuteEnd = minuteEnd,
            HourList = hourList,
            HourStart = hourStart,
            HourEnd = hourEnd,
            DayList = dayList,
            DayStart = dayStart,
            DayEnd = dayEnd,
            Year = year
        });
    }
}
```

with:

```csharp
        var spec = new ScheduleSpec
        {
            Interval = interval,
            Unit = unit,
            DayOfWeek = dayOfWeek,
            DayPattern = dayPattern,
            DayOfMonth = dayOfMonth,
            DayOfWeekList = dayOfWeekList,
            DayOfWeekStart = dayOfWeekStart,
            DayOfWeekEnd = dayOfWeekEnd,
            Month = monthSpecifier,
            TimeOfDay = timeOfDay,
            TimeZone = options.TimeZone,
            IsLastDay = isLastDay,
            IsLastDayOfWeek = isLastDayOfWeek,
            LastDayOffset = lastDayOffset,
            IsNearestWeekday = isNearestWeekday,
            NthOccurrence = nthOccurrence,
            MinuteList = minuteList,
            MinuteStart = minuteStart,
            MinuteEnd = minuteEnd,
            HourList = hourList,
            HourStart = hourStart,
            HourEnd = hourEnd,
            DayList = dayList,
            DayStart = dayStart,
            DayEnd = dayEnd,
            Year = year
        };

        if (options.MinInterval is { } minInterval)
        {
            var tightestGap = TightestGapCalculator.Calculate(spec);
            if (tightestGap < minInterval)
            {
                return new ParseResult<ScheduleSpec>.Error(
                    $"'{naturalLanguage}' runs more often than the minimum allowed interval of {FormatFloor(minInterval)}");
            }
        }

        return new ParseResult<ScheduleSpec>.Success(spec);
    }
}
```

Also add `using HumanCron.Validation;` to the top of `src/HumanCron/Parsing/NaturalLanguageParser.cs`, alongside the existing `using HumanCron.Models.Internal;` etc., so the unqualified `TightestGapCalculator.Calculate(spec)` call above resolves.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~MinIntervalFloorTests"`
Expected: PASS (15 tests - 11 fixed + 4 TestCase instances of the last one)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: PASS (1013 + 23 + 15 = 1051)

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron/Parsing/NaturalLanguageParser.cs src/HumanCron/Parsing/NaturalLanguageParser.Helpers.cs tests/HumanCron.Tests/Parsing/MinIntervalFloorTests.cs
git commit -m "feat: enforce MinInterval floor in NaturalLanguageParser.Parse"
```

---

### Task 5: Public API — `UnixCronConverter.ToCron(string, ScheduleParserOptions)`

**Files:**
- Modify: `src/HumanCron/Abstractions/IHumanCronConverter.cs`
- Modify: `src/HumanCron/Converters/Unix/UnixCronConverter.cs`
- Modify: `tests/HumanCron.Tests/Converters/UnixCronConverterTests.cs`

**Interfaces:**
- Produces: `IHumanCronConverter.ToCron(string naturalLanguage, ScheduleParserOptions options)` — the shape Task 8's cross-converter regression check exercises for Unix.

- [ ] **Step 1: Write the failing test**

Add to `tests/HumanCron.Tests/Converters/UnixCronConverterTests.cs` (inside the existing `UnixCronConverterTests` class):

```csharp
    [Test]
    public void ToCron_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var tooFast = _converter.ToCron("every 5 minutes", options);
        var atFloor = _converter.ToCron("every 15 minutes", options);

        Assert.That(tooFast, Is.TypeOf<ParseResult<string>.Error>());
        Assert.That(atFloor, Is.TypeOf<ParseResult<string>.Success>());
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~UnixCronConverterTests.ToCron_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor"`
Expected: FAIL to compile — no `ToCron(string, ScheduleParserOptions)` overload exists on `IHumanCronConverter`.

- [ ] **Step 3: Add the interface method**

In `src/HumanCron/Abstractions/IHumanCronConverter.cs`, add `using HumanCron.Parsing;` to the usings, and add this member to `IHumanCronConverter` (after the existing `ToCron(string, DateTimeZone?)`):

```csharp
    /// <summary>
    /// Convert natural language to Unix 5-part cron expression, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "1d at 2pm")</param>
    /// <param name="options">
    /// Parser options. Unlike the DateTimeZone? overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself, matching System.Text.Json's
    /// JsonSerializerOptions.
    /// </param>
    /// <returns>Unix 5-part cron expression, or an Error if MinInterval is set and violated</returns>
    /// <example>
    /// <code>
    /// var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };
    /// var result = converter.ToCron("every 5 minutes", options); // Error: runs more often than 15 minutes
    /// </code>
    /// </example>
    ParseResult<string> ToCron(string naturalLanguage, ScheduleParserOptions options);
```

- [ ] **Step 4: Implement it on `UnixCronConverter`, and have the `DateTimeZone?` overload delegate to it**

In `src/HumanCron/Converters/Unix/UnixCronConverter.cs`, replace the existing `ToCron(string naturalLanguage, DateTimeZone? userTimezone)` method body with:

```csharp
    public ParseResult<string> ToCron(string naturalLanguage, DateTimeZone? userTimezone)
    {
        return ToCron(naturalLanguage, new ScheduleParserOptions { TimeZone = userTimezone ?? _localTimeZone });
    }

    /// <inheritdoc/>
    public ParseResult<string> ToCron(string naturalLanguage, ScheduleParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<string>.Error("Natural language input cannot be empty");
        }

        if (naturalLanguage.Length > MaxInputLength)
        {
            return new ParseResult<string>.Error(
                $"Natural language input exceeds maximum length of {MaxInputLength} characters");
        }

        // Step 1: Parse natural language → ScheduleSpec
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is ParseResult<ScheduleSpec>.Error parseError)
        {
            return new ParseResult<string>.Error($"Failed to parse natural language: {parseError.Message}");
        }

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Step 2: Build Unix cron from ScheduleSpec
        var buildResult = _cronBuilder.Build(spec);
        if (buildResult is ParseResult<string>.Error buildError)
        {
            return new ParseResult<string>.Error($"Failed to convert to cron: {buildError.Message}");
        }

        return buildResult;
    }
```

Remove the now-duplicated validation/parse/build logic that previously lived directly in the `DateTimeZone?` overload (it's shown above already folded into the new core method — this step is a straight cut-and-paste-then-delete, not new logic).

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~UnixCronConverterTests"`
Expected: PASS (all UnixCronConverterTests, including the new one)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron/Abstractions/IHumanCronConverter.cs src/HumanCron/Converters/Unix/UnixCronConverter.cs tests/HumanCron.Tests/Converters/UnixCronConverterTests.cs
git commit -m "feat: add ToCron(string, ScheduleParserOptions) overload to IHumanCronConverter"
```

---

### Task 6: Public API — `NCrontabConverter.ToNCrontab(string, ScheduleParserOptions)`

**Files:**
- Modify: `src/HumanCron.NCrontab/Abstractions/INCrontabConverter.cs`
- Modify: `src/HumanCron.NCrontab/Converters/NCrontabConverter.cs`
- Modify: `tests/HumanCron.Tests/Converters/NCrontabConverterTests.cs`

**Interfaces:**
- Produces: `INCrontabConverter.ToNCrontab(string naturalLanguage, ScheduleParserOptions options)`.

- [ ] **Step 1: Write the failing test**

Add to `tests/HumanCron.Tests/Converters/NCrontabConverterTests.cs` (inside the existing `NCrontabConverterTests` class):

```csharp
    [Test]
    public void ToNCrontab_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var tooFast = _converter.ToNCrontab("every 5 minutes", options);
        var atFloor = _converter.ToNCrontab("every 15 minutes", options);

        Assert.That(tooFast, Is.TypeOf<ParseResult<string>.Error>());
        Assert.That(atFloor, Is.TypeOf<ParseResult<string>.Success>());
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~NCrontabConverterTests.ToNCrontab_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor"`
Expected: FAIL to compile — no `ToNCrontab(string, ScheduleParserOptions)` overload exists.

- [ ] **Step 3: Add the interface method**

In `src/HumanCron.NCrontab/Abstractions/INCrontabConverter.cs`, add `using HumanCron.Parsing;` to the usings, and add:

```csharp
    /// <summary>
    /// Convert natural language to NCrontab expression, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule</param>
    /// <param name="options">
    /// Parser options. Unlike the DateTimeZone? overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself.
    /// </param>
    /// <returns>ParseResult containing NCrontab expression, or an Error if MinInterval is set and violated</returns>
    ParseResult<string> ToNCrontab(string naturalLanguage, ScheduleParserOptions options);
```

- [ ] **Step 4: Implement it on `NCrontabConverter`, and have the `DateTimeZone?` overload delegate to it**

In `src/HumanCron.NCrontab/Converters/NCrontabConverter.cs`, replace the existing `ToNCrontab(string naturalLanguage, DateTimeZone? userTimezone)` method body with:

```csharp
    /// <inheritdoc/>
    public ParseResult<string> ToNCrontab(string naturalLanguage, DateTimeZone? userTimezone)
    {
        return ToNCrontab(naturalLanguage, new ScheduleParserOptions { TimeZone = userTimezone ?? _localTimeZone });
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNCrontab(string naturalLanguage, ScheduleParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<string>.Error("Natural language input cannot be empty");
        }

        if (naturalLanguage.Length > MaxInputLength)
        {
            return new ParseResult<string>.Error(
                $"Natural language input exceeds maximum length of {MaxInputLength} characters");
        }

        // Step 1: Parse natural language → ScheduleSpec
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is ParseResult<ScheduleSpec>.Error parseError)
        {
            return new ParseResult<string>.Error($"Failed to parse natural language: {parseError.Message}");
        }

        var spec = ((ParseResult<ScheduleSpec>.Success)parseResult).Value;

        // Step 2: Build NCrontab cron from ScheduleSpec
        var buildResult = _cronBuilder.Build(spec);
        if (buildResult is ParseResult<string>.Error buildError)
        {
            return new ParseResult<string>.Error($"Failed to convert to NCrontab: {buildError.Message}");
        }

        return buildResult;
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~NCrontabConverterTests"`
Expected: PASS (all NCrontabConverterTests, including the new one)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron.NCrontab/Abstractions/INCrontabConverter.cs src/HumanCron.NCrontab/Converters/NCrontabConverter.cs tests/HumanCron.Tests/Converters/NCrontabConverterTests.cs
git commit -m "feat: add ToNCrontab(string, ScheduleParserOptions) overload to INCrontabConverter"
```

---

### Task 7: Public API — Quartz `ToQuartzSchedule`/`CreateTriggerBuilder(string, ScheduleParserOptions, int)`

**Files:**
- Modify: `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs`
- Modify: `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs`
- Modify: `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs`

**Interfaces:**
- Produces: `IQuartzScheduleConverter.ToQuartzSchedule(string, ScheduleParserOptions, int misfireInstruction = 0)` and `IQuartzScheduleConverter.CreateTriggerBuilder(string, ScheduleParserOptions, int misfireInstruction = 0)`.

- [ ] **Step 1: Write the failing test**

Add to `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs` (inside the existing `QuartzScheduleConverterTests` class; add `using HumanCron.Parsing;` to the file's usings):

```csharp
    [Test]
    public void ToQuartzSchedule_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var tooFast = _converter.ToQuartzSchedule("every 5 minutes", options);
        var atFloor = _converter.ToQuartzSchedule("every 15 minutes", options);

        Assert.That(tooFast, Is.TypeOf<ParseResult<IScheduleBuilder>.Error>());
        Assert.That(atFloor, Is.TypeOf<ParseResult<IScheduleBuilder>.Success>());
    }

    [Test]
    public void CreateTriggerBuilder_ScheduleParserOptionsOverload_EnforcesMinIntervalFloor()
    {
        var options = new ScheduleParserOptions { MinInterval = TimeSpan.FromMinutes(15) };

        var result = _converter.CreateTriggerBuilder("every 5 minutes", options);

        Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~QuartzScheduleConverterTests"`
Expected: FAIL to compile — no `ScheduleParserOptions`-taking overloads exist on `IQuartzScheduleConverter`.

- [ ] **Step 3: Add the interface methods**

In `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs`, add `using HumanCron.Parsing;` to the usings, and add these two members (after the existing `ToQuartzSchedule` and before `ToNaturalLanguage`, and after `CreateTriggerBuilder` respectively):

```csharp
    /// <summary>
    /// Convert natural language to Quartz schedule builder, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "2w on sunday at 3am")</param>
    /// <param name="options">
    /// Parser options. Unlike the misfire-only overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself.
    /// </param>
    /// <param name="misfireInstruction">Quartz misfire instruction constant (default: 0 = SmartPolicy)</param>
    /// <returns>ParseResult with IScheduleBuilder, or an Error if MinInterval is set and violated</returns>
    ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        ScheduleParserOptions options,
        int misfireInstruction = 0);
```

and:

```csharp
    /// <summary>
    /// Create a pre-configured TriggerBuilder, with full parser options
    /// (timezone and/or a MinInterval floor)
    /// </summary>
    /// <param name="naturalLanguage">Natural language schedule (e.g., "3w on sunday at 2pm")</param>
    /// <param name="options">
    /// Parser options. Unlike the misfire-only overload, options.TimeZone is used exactly
    /// as given (default = system timezone) - there is no per-converter local-timezone
    /// fallback once you pass this object yourself.
    /// </param>
    /// <param name="misfireInstruction">Quartz misfire instruction constant (default: 0 = SmartPolicy)</param>
    /// <returns>ParseResult with TriggerBuilder, or an Error if MinInterval is set and violated</returns>
    ParseResult<TriggerBuilder> CreateTriggerBuilder(
        string naturalLanguage,
        ScheduleParserOptions options,
        int misfireInstruction = 0);
```

- [ ] **Step 4: Implement both on `QuartzScheduleConverter`, and have the existing internal/public overloads delegate**

In `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs`, replace the internal `ToQuartzSchedule(string, DateTimeZone?, int)` method with a delegating overload, and add the new core method:

```csharp
    internal ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        DateTimeZone? userTimezone,
        int misfireInstruction = 0)
    {
        return ToQuartzSchedule(
            naturalLanguage,
            new Parsing.ScheduleParserOptions { TimeZone = userTimezone ?? _localTimeZone },
            misfireInstruction);
    }

    /// <inheritdoc/>
    public ParseResult<IScheduleBuilder> ToQuartzSchedule(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        int misfireInstruction = 0)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return new ParseResult<IScheduleBuilder>.Error("Natural language input cannot be empty");
        }

        if (naturalLanguage.Length > MaxInputLength)
        {
            return new ParseResult<IScheduleBuilder>.Error(
                $"Natural language input exceeds maximum length of {MaxInputLength} characters");
        }

        // Step 1: Parse natural language to ScheduleSpec
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            return new ParseResult<IScheduleBuilder>.Error(error.Message);
        }

        var spec = success.Value;

        // Step 2: Build Quartz schedule from ScheduleSpec
        try
        {
            var scheduleBuilder = _quartzBuilder.Build(spec);

            // Step 3: Apply misfire instruction to the schedule builder
            scheduleBuilder = MisfireInstructionHelper.ApplyMisfireInstruction(scheduleBuilder, misfireInstruction);

            return new ParseResult<IScheduleBuilder>.Success(scheduleBuilder);
        }
        catch (Exception ex)
        {
            return new ParseResult<IScheduleBuilder>.Error($"Failed to build Quartz schedule: {ex.Message}");
        }
    }
```

Then replace `CalculateStartTime` with a delegating overload plus the new core method:

```csharp
    internal ParseResult<DateTimeOffset?> CalculateStartTime(
        string naturalLanguage,
        DateTimeOffset? referenceTime = null,
        DateTimeZone? userTimezone = null)
    {
        return CalculateStartTime(
            naturalLanguage,
            new Parsing.ScheduleParserOptions { TimeZone = userTimezone ?? _localTimeZone },
            referenceTime);
    }

    internal ParseResult<DateTimeOffset?> CalculateStartTime(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        DateTimeOffset? referenceTime = null)
    {
        var parseResult = _parser.Parse(naturalLanguage, options);
        if (parseResult is not ParseResult<ScheduleSpec>.Success success)
        {
            var error = (ParseResult<ScheduleSpec>.Error)parseResult;
            return new ParseResult<DateTimeOffset?>.Error(error.Message);
        }

        var spec = success.Value;
        var startTime = _quartzBuilder.CalculateStartTime(spec, referenceTime);
        return new ParseResult<DateTimeOffset?>.Success(startTime);
    }
```

Finally, add the new public `CreateTriggerBuilder(string, ScheduleParserOptions, int)` overload (after the existing public `CreateTriggerBuilder(string, int)`):

```csharp
    /// <inheritdoc/>
    public ParseResult<TriggerBuilder> CreateTriggerBuilder(
        string naturalLanguage,
        Parsing.ScheduleParserOptions options,
        int misfireInstruction = 0)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Get the schedule builder with misfire instruction applied
        // (ToQuartzSchedule validates input - null/empty/length checks)
        var scheduleResult = ToQuartzSchedule(naturalLanguage, options, misfireInstruction);
        if (scheduleResult is not ParseResult<IScheduleBuilder>.Success scheduleSuccess)
        {
            var error = (ParseResult<IScheduleBuilder>.Error)scheduleResult;
            return new ParseResult<TriggerBuilder>.Error(error.Message);
        }

        // Calculate start time (null if not needed)
        var startTimeResult = CalculateStartTime(naturalLanguage, options);
        if (startTimeResult is not ParseResult<DateTimeOffset?>.Success startSuccess)
        {
            var error = (ParseResult<DateTimeOffset?>.Error)startTimeResult;
            return new ParseResult<TriggerBuilder>.Error(error.Message);
        }

        // Create TriggerBuilder with schedule and optional start time
        var triggerBuilder = TriggerBuilder.Create()
            .WithSchedule(scheduleSuccess.Value);

        // Set start time if calculated (for CalendarInterval schedules with constraints)
        if (!startSuccess.Value.HasValue) return new ParseResult<TriggerBuilder>.Success(triggerBuilder);
        // Explicitly convert to UTC to ensure Quartz interprets it correctly
        var startTimeUtc = startSuccess.Value.Value.ToUniversalTime();
        triggerBuilder.StartAt(startTimeUtc);

        return new ParseResult<TriggerBuilder>.Success(triggerBuilder);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~QuartzScheduleConverterTests"`
Expected: PASS (all QuartzScheduleConverterTests, including the two new ones)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs
git commit -m "feat: add ScheduleParserOptions overloads to IQuartzScheduleConverter"
```

---

### Task 8: Full solution regression pass

**Files:** none (verification only)

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build HumanCron.slnx`
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 2: Run the whole test suite**

Run: `dotnet test HumanCron.slnx`
Expected: PASS, with a total test count of 1011 (original) + 2 (Task 1) + 23 (Task 3, superseding Task 2's 11) + 15 (Task 4) + 1 (Task 5) + 1 (Task 6) + 2 (Task 7) = 1055. If the actual count differs, that's fine as long as everything is green - this number is a sanity check, not a hard gate.

- [ ] **Step 3: Confirm no leftover references to the old `ScheduleParserOptions` class semantics**

Run: `grep -rn "class ScheduleParserOptions" src/`
Expected: no output (it's a `record` now, not a `class`).
