# MinInterval Floor Validation — Design

Issue: [#10](https://github.com/musicislife08/HumanCron/issues/10) — Add optional MinInterval (floor) validation for parsed schedules.

## Problem

Today the only bound on parsed intervals is a hardcoded, unit-blind check (1–1000) in
`NaturalLanguageParser.Interval.cs`. Apps that expose natural-language scheduling to end
users often need to enforce a fastest-allowed cadence (e.g. "may not run more often than
every 15 minutes"), and today every consumer has to re-validate downstream after the fact.

## Semantics: tightest firing gap, inclusive floor

`MinInterval` constrains the **tightest gap between consecutive firings**, not the raw
parsed interval number — this generalizes uniformly across all patterns.

**Boundary rule: inclusive.** A schedule is valid when `tightestGap >= MinInterval`; it is
rejected only when `tightestGap < MinInterval`. Example: with a 15-minute floor, `every 15
minutes` is allowed (gap == floor) and `every 5 minutes` is rejected (gap < floor). "May not
run more often than every 15 minutes" means 15 minutes *is* the fastest allowed cadence, not
the fastest forbidden one.

| Pattern | Tightest gap | 15-min floor |
|---|---|---|
| `every 5 minutes` | 5 min | rejected |
| `every 15 minutes` | 15 min | allowed (boundary) |
| `at minutes 0,15,30,45` | 15 min | allowed (boundary) |
| `at minutes 0,10,20,30,40,50` | 10 min | rejected |
| `every 5 minutes between 0 and 30 of each hour` | 5 min | rejected (bursts every 5 min) |
| `every weekday at 9am` | 24 h | allowed |

## Out of scope: MaxInterval ceiling

A ceiling ("must fire at least every Y") was considered and explicitly dropped from this
work. The floor is analytically tractable because pessimism (assuming firings are closer
together than reality) is the *safe* error direction — it only over-rejects contrived
patterns, never lets a too-fast one through. A ceiling needs the opposite safe direction
(never underestimate the widest gap), and the true widest gap is a global property —
cross-boundary gaps (`every friday and monday` → 3-day gap), variable month length, DST,
and Quartz's `L`/`W`/`#` all resolve differently depending on the actual calendar, which
means correctly bounding it requires occurrence simulation, not per-field analytic math.
That's a separate, materially larger feature — file separately if a motivating use case
shows up.

## API

### `ScheduleParserOptions` becomes a record, gains `MinInterval`

```csharp
public sealed record ScheduleParserOptions
{
    public DateTimeZone TimeZone { get; init; } = DateTimeZoneProviders.Tzdb.GetSystemDefault();
    public TimeSpan? MinInterval { get; init; }
}
```

Converting the existing `sealed class` to a `sealed record` is additive in practice (value
equality, `ToString`, `Deconstruct`) and gives callers `with`-expression overrides for free —
the same pattern `System.Text.Json`'s `JsonSerializerOptions` uses:

```csharp
private static readonly ScheduleParserOptions Defaults = new() { MinInterval = TimeSpan.FromMinutes(15) };

// per-call override, cheap non-destructive copy
converter.ToCron(text, Defaults with { MinInterval = TimeSpan.FromMinutes(5) });
```

There is no framework-level "global" config (no `AddHumanCron(configure)`, no
`IOptions<T>`, no new package). An app owns a shared options instance and reuses it — that
*is* the global story, and it needs zero plumbing on the library side.

### New overloads, additive only

Each converter gets one new overload taking the options object directly, alongside (not
replacing) the existing scalar-parameter overloads:

- `IHumanCronConverter.ToCron(string naturalLanguage, ScheduleParserOptions options)`
- `INCrontabConverter.ToNCrontab(string naturalLanguage, ScheduleParserOptions options)`
- `IQuartzScheduleConverter.ToQuartzSchedule(string naturalLanguage, ScheduleParserOptions options)`
- `IQuartzScheduleConverter.CreateTriggerBuilder(string naturalLanguage, ScheduleParserOptions options)`

### Validation lives once

All three converters already funnel through the same internal
`NaturalLanguageParser.Parse(string, ScheduleParserOptions)`. The floor check is added there,
right before returning `Success`, so every converter inherits it automatically — no
per-converter code.

## `TightestGapCalculator`

A new internal pure function, `ScheduleSpec -> TimeSpan`, in the core `HumanCron` project.
Called from `NaturalLanguageParser.Parse` only when `options.MinInterval` is set.

Per-shape formulas:

- **Plain interval** → `interval x unitLength`. Seconds/minutes/hours/days/weeks are exact;
  months approximate to 28 days and years to 365 days — both the *shortest* possible
  calendar length, since underestimating the gap is the safe direction for a floor. Only
  matters for floors above ~4 weeks.
- **Minute/hour step or range+step** → the step value.
- **Minute/hour list, 2+ entries** → minimum adjacent difference, including wraparound
  (mod 60 for minutes, mod 24 for hours).
- **Minute/hour list, exactly 1 entry** → the full cycle length (60 min / 24 h). This needs
  its own branch: "minimum adjacent difference" is undefined on a single-element list, and
  naively falling through would compute 0, which would reject every schedule against any
  positive floor.
- **At-most-daily shapes** (day-of-week, day-of-month, weekday patterns, "on"-patterns) →
  flat 24-hour floor.

**Known accepted pessimism (v1, not silently changed later):** `at minutes 0,59 at hours 9
and 15` computes a 1-minute wraparound gap (0 and 59 are adjacent mod 60) even though the
true tightest gap is 59 minutes, because the calculator doesn't check whether the two
selected hours are themselves adjacent. This over-rejects a contrived pattern but never
under-rejects a genuinely fast one, so it's safe — just conservative. An exact version
(apply wraparound only when two selected hours are adjacent) is a possible future
refinement, not part of this design.

## Error handling

Violation returns through the existing `ParseResult<T>.Error` channel, worded so it's safe
to show directly in a UI:

```
'every 5 minutes' runs more often than the minimum allowed interval of 15 minutes
```

`MinInterval = null` (default) never triggers the check — no behavior change for existing
callers. `MinInterval = TimeSpan.Zero` is mathematically inert (a zero floor forbids
nothing) and is allowed, not special-cased. A negative `MinInterval` is likewise inert
(`gap < negative` is never true) and is not validated against — it's nonsensical input, not
a dangerous one, so there's nothing to defend against.

## Testing plan

Correctness here means boundary-exact, not just "roughly works" — every shape gets an
above/at/below triplet at its floor boundary, plus:

- Boundary triplets per pattern shape (`gap == floor` allowed, `gap == floor - 1 unit`
  rejected, `gap == floor + 1 unit` allowed) for plain intervals, minute lists, hour lists,
  and range+step patterns
- Single-element minute/hour list (the 60/24h full-cycle edge case)
- Wraparound minute/hour lists, including the documented pessimistic case above
- All 7 interval units at their exact floor boundary (seconds through years, including the
  month ~28d / year ~365d approximation)
- `MinInterval = null` always passes regardless of interval (regression safety)
- `MinInterval = TimeSpan.Zero` always passes
- One test per converter (Unix, Quartz, NCrontab) proving the floor is inherited without
  per-converter code — validates the shared-`Parse` design actually holds
- Exact error-message string match against the documented wording
