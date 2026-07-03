# Bidirectional Human Duration Parsing — Design

Issue: [#11](https://github.com/musicislife08/HumanCron/issues/11) — Bidirectional human duration
parsing: string ↔ TimeSpan/DateTimeOffset, with one-time Quartz trigger support.

## Problem

The library converts *recurring* schedules today (`"every day at 2pm"` ↔ cron). A common
companion need is **one-shot relative time**: parse `"2 hours"` and get a future instant
(`now + 2h`) — e.g. a periodic sync completes and freezes a dataset for a user-configured
period, scheduling a one-time Quartz job to unfreeze it. In keeping with the library's core
promise, it must be bidirectional: format a duration or instant back into the same human
vocabulary.

This is a separate grammar from schedules, not an extension of it — durations have no
`every`/`on` prefix, so there is zero grammar collision with `NaturalLanguageParser`.

## Architecture: three layers

### Layer 1 — core bidirectional parse (`HumanCron`): `string ↔ TimeSpan`

Pure, fixed-length durations. No anchor, no calendar, no timezone.

```csharp
ParseResult<TimeSpan> ParseDuration(string duration);
string FormatDuration(TimeSpan duration);
```

- Compound durations, both directions, required (not optional): `"1 day 2 hours 30 minutes"`
  / `"1d 2h 30m"`. The formatter decomposes into largest whole units descending
  (`TimeSpan.FromMinutes(150)` → `"2 hours 30 minutes"`), and the parser accepts everything
  the formatter emits, or the round-trip contract breaks. Each unit is included only when
  nonzero (`"1 minute"`, never `"1 minute 0 seconds"`); precision finer than a millisecond is
  ignored entirely (not rounded, just dropped). **Zero edge case:** if decomposition leaves no
  whole-unit components at all — the duration is exactly zero, or it's entirely
  sub-millisecond — `FormatDuration` returns `""` rather than inventing a placeholder unit
  (no `"0 seconds"`/`"0 milliseconds"`; a human describing a zero or negligible duration just
  says nothing). To keep `Parse(Format(x)) == x` true for this case, `ParseDuration("")` is a
  deliberate, sole exception to the rest of the library's "empty input is an error"
  convention: it returns `Success(TimeSpan.Zero)`, not `Error`.
- Unit tokens: full words (`millisecond(s)`, `second(s)`, `minute(s)`, `hour(s)`, `day(s)`,
  `week(s)`, `month(s)`, `year(s)`) and abbreviations, reusing the mapping already reserved
  (but unused) by the internal `IntervalUnit` enum: `s`, `m` (minutes), `h`, `d`, `w`, `M`
  (months, capitalized to disambiguate from minutes), `y` — plus `ms` for milliseconds, a
  unit the schedule grammar has no equivalent for (sub-second precision is meaningless for a
  *recurring* schedule but meaningful for a one-shot elapsed duration, so this is an
  intentional divergence, not an oversight). Because `ms` shares a prefix with `m` (minutes),
  the tokenizer matches `ms` before falling back to single-letter units (longest-match-first),
  so `"500ms"` isn't misread as `"500m"` plus a stray `s`.
- Compound spacing: **spaced only** — `"1d 2h 30m"`. No squashed form (`"1d2h30m"`) — the
  library favors readability over density, and there's no existing squashed-form precedent
  elsewhere in the grammar to match.
- Sign: parsing accepts `-`, `minus`, and `negative` as synonymous prefixes (`"-2 hours"`,
  `"minus 2 hours"`, `"negative 2 hours"` all parse identically). Formatting always emits the
  canonical leading `-` — one policy, applied twice: parse liberally, format canonically
  (same policy the schedule grammar already applies to day/month abbreviations). The sign
  applies once to the whole compound value, never per-component.
- Both directions (positive and negative) are valid — this is a general-purpose duration
  type, not scoped to "future only" (e.g. formatting the diff between two arbitrary
  timestamps, where the target could be before or after the reference point).
- `ParseDuration` **errors on month/year units** — those are calendar units with no fixed
  length; approximating them here would silently lie. They're fully supported in Layer 2,
  where an anchor makes the math well-defined.
- Results via `ParseResult<T>`, consistent with the rest of the library.

### Layer 2 — anchored instant math (`HumanCron`): `string ↔ DateTimeOffset`

```csharp
ParseResult<DateTimeOffset> ToFutureTime(
    string duration,
    DateTimeOffset? anchor = null,
    DateTimeZone? timeZone = null);

ParseResult<string> ToNaturalDuration(
    DateTimeOffset target,
    DateTimeOffset? anchor = null,
    DateTimeZone? timeZone = null);
```

- `anchor: null` means "now," sourced from an injected NodaTime `IClock`
  (`SystemClock.Instance` in production via `HumanDurationConverter.Create()`, `FakeClock` in
  tests via an internal constructor) — the same clock-injection pattern
  `QuartzScheduleConverter` already uses, not `TimeProvider`. This also avoids adding a new
  test-only package: `NodaTime.Testing`'s `FakeClock` is already a dependency; `TimeProvider`'s
  `FakeTimeProvider` would require a separate package. `IClock.GetCurrentInstant()` also
  returns the NodaTime `Instant` this layer's math needs directly, with no intermediate
  `DateTimeOffset` conversion.
- `timeZone` governs precision for calendar (month/year) components, and is the reason this
  layer takes a zone at all rather than working purely off the anchor's fixed UTC offset:

  **Why a plain offset isn't enough.** A `DateTimeOffset` carries a fixed numeric offset with
  no knowledge of *which* IANA zone it came from, or that zone's DST rules. Calendar
  navigation ("next month, same wall-clock time") can land on the other side of a DST
  transition, where the correct offset differs from the one the anchor started with. Example:
  anchor `2026-03-08T01:30:00-05:00` (New York, EST) plus one calendar month should be
  `2026-04-08 01:30 AM` New York time — but by April, New York is on EDT (`-04:00`), not EST.
  Naively keeping the original offset (what `DateTimeOffset.AddMonths` does) produces an
  instant that's off by exactly one hour, silently.

  - `timeZone: null` (default) — **Mode 1, naive.** Convert the anchor to NodaTime's
    `OffsetDateTime` (local time + its existing fixed offset, no zone attached), add the
    `Period` to the local-time component only, and keep the original offset as-is. Cheap, no
    zone lookup required, but can be off by up to an hour across a DST boundary when the
    duration includes month/year components. Fine when the caller doesn't have or need a real
    zone, or the imprecision is acceptable for their use case.
  - `timeZone: <zone>` — **Mode 2, precise.** Convert the anchor to an `Instant`, resolve it
    into a `ZonedDateTime` in the given zone, add the `Period` to its `LocalDateTime`, and
    re-resolve the result through `zone.AtLeniently(...)` — NodaTime's built-in handling for a
    result that lands in a DST gap (skipped hour) or overlap (repeated hour), so this layer
    never needs to invent DST logic or throw on the edge cases.
  - **This distinction only matters when the duration includes month/year components.** For
    fixed-unit-only durations (hours/minutes/seconds/days/weeks), Mode 1 and Mode 2 always
    produce the identical instant — there's no calendar ambiguity to resolve.
- Months/years are fully supported here (unlike Layer 1) because the anchor makes calendar
  math well-defined. Month-end rollover follows NodaTime's own `Period`/`LocalDateTime`
  semantics (`Jan 31` + 1 month → `Feb 28`/`Feb 29`, matching `DateTime.AddMonths`-style
  clamping).
- `ToNaturalDuration` computes `target − anchor` (calendar-aware via `Period` when a
  `timeZone` is given) and formats the result with the same decomposition and sign policy as
  Layer 1 — no direction restriction; a `target` before `anchor` formats with a leading `-`
  just like a negative `TimeSpan` does.
- `ToFutureTime` keeps its name (forward scheduling is the primary intended use, matching the
  issue's motivating example) but doesn't special-case direction either — passing a negative
  duration string simply produces an instant before the anchor.

### Layer 3 — Quartz sugar (`HumanCron.Quartz`)

```csharp
ParseResult<TriggerBuilder> CreateOneTimeTriggerBuilder(
    string duration,
    DateTimeOffset? anchor = null,
    DateTimeZone? timeZone = null,
    int misfireInstruction = 0);
```

Added to `IQuartzScheduleConverter`/`QuartzScheduleConverter`, mirroring the existing
`CreateTriggerBuilder` for recurring schedules: built on top of `HumanDurationConverter.ToFutureTime`,
with `.StartAt(...)` pre-set to the computed instant and no repeating schedule — a one-time
fire. Parameter order matches the core method (`anchor`, then `timeZone`) with
`misfireInstruction` appended last, following the same "extend by appending, never insert"
convention as the rest of the library's overloads.

Hangfire equivalent (`BackgroundJob.Schedule` delay) is a possible follow-up, not v1.

## Why not a new package

The existing satellite packages (`HumanCron.NCrontab`, `HumanCron.Quartz`,
`HumanCron.Hangfire`) each exist to isolate a **third-party dependency** (the NCrontab
library, Quartz.NET, Hangfire.Core) that the core package doesn't want to force on every
consumer. Duration parsing introduces no new external dependency — it only needs NodaTime
(already a core dependency). There's nothing to isolate, and splitting it out would force the
issue's own motivating use case (schedule a sync, then schedule its one-time unfreeze) across
two package references for no technical benefit. Layers 1 and 2 live in `HumanCron` core;
Layer 3 is an addition to the existing `HumanCron.Quartz`.

## Internal implementation notes

- No new internal model type is needed for "parsed duration" — NodaTime's own `Period` type
  already has the Years/Months/Weeks/Days/Hours/Minutes/Seconds/Milliseconds fields required,
  so the internal parser produces a `Period` directly.
- A new internal `DurationParser`/`DurationFormatter` pair mirrors the existing
  `NaturalLanguageParser`/`NaturalLanguageFormatter` split. `FormatDuration(TimeSpan)` (Layer
  1) is a thin wrapper: convert the `TimeSpan` to a fixed-units-only `Period`, then call the
  same decomposition logic Layer 2's calendar-aware formatting uses.
- Public surface:
  - `src/HumanCron/Abstractions/IHumanDurationConverter.cs`
  - `src/HumanCron/Converters/Duration/HumanDurationConverter.cs` (public class, `Create()`
    factory + internal `IClock`-accepting constructor for tests)
- Existing `MaxInputLength` DoS-guard convention (rejecting inputs beyond a fixed length)
  carries over to the new parse entry points for consistency with the existing converters.

## Error handling

- Empty or whitespace-only input to `ParseDuration` → `Success(TimeSpan.Zero)` (trimmed
  first, so `""` and `"   "` behave identically). This is the one deliberate exception to the
  rest of the library's "empty input is an error" convention, kept solely to preserve
  `Parse(Format(x)) == x` for the zero-duration case (see Layer 1 above).
- Unparseable tokens / unknown units → `Error` with a message naming the offending input.
- `ParseDuration` given month/year units → `Error`, pointing the caller at `ToFutureTime`/
  `ToNaturalDuration` for calendar-aware math.
- No error for direction — negative durations and past targets are valid, not exceptional.

## Testing

- Round-trip tests (`Parse(Format(x)) == x`) for `TimeSpan`, in the spirit of the existing
  `CompleteBidirectionalTests` — including negative values and millisecond-precision values.
- The zero-duration exception explicitly: `FormatDuration(TimeSpan.Zero) == ""` and
  `ParseDuration("") == Success(TimeSpan.Zero)`, plus the sub-millisecond-truncates-to-zero
  case (e.g. a duration built from ticks alone).
- `FakeClock`-pinned tests for the now-based sugar (`anchor: null`).
- A month-end rollover test (`Jan 31` + 1 month across both leap and non-leap Februaries).
- An explicit DST-transition test pair proving the Mode 1 vs. Mode 2 distinction: the same
  anchor and duration, once with `timeZone: null` (asserting the naive, off-by-an-hour
  result) and once with an explicit zone spanning the transition (asserting the corrected
  result).
- Anchored round-trip tests including calendar units (`ToNaturalDuration(ToFutureTime(...))`
  round-trips for month/year durations under Mode 2).

## Follow-ups (out of scope)

- Hangfire one-time scheduling sugar.
- Localized unit names (English-only for v1, consistent with the rest of the library).
