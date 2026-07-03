# Bidirectional Human Duration Parsing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add bidirectional natural-language duration parsing (`string ↔ TimeSpan`), anchored instant math (`string ↔ DateTimeOffset`), and a one-time Quartz trigger convenience method, per design spec `docs/superpowers/specs/2026-07-02-bidirectional-duration-parsing-design.md` (issue #11).

**Architecture:** Three layers. Layer 1 (`HumanCron` core) is a pure, fixed-length `string ↔ TimeSpan` grammar built on an internal `NodaTime.Period`-based parser/formatter pair. Layer 2 (`HumanCron` core) adds anchored `string ↔ DateTimeOffset` math with an optional `DateTimeZone` for DST-aware calendar (month/year) arithmetic. Layer 3 (`HumanCron.Quartz`) adds a one-time `TriggerBuilder` convenience method built on top of Layer 2.

**Tech Stack:** .NET 10 (net10.0), NodaTime (`Period`, `OffsetDateTime`, `ZonedDateTime`, `Instant`, `IClock`), NUnit, Quartz.NET.

## Global Constraints

- Unit tokens: full words (`millisecond(s)`, `second(s)`, `minute(s)`, `hour(s)`, `day(s)`, `week(s)`, `month(s)`, `year(s)`) plus abbreviations `ms`, `s`, `m` (minutes), `h`, `d`, `w`, `M` (months, case-sensitive vs. `m`), `y`.
- Compound durations are **spaced only**: `"1d 2h 30m"`. No squashed form (`"1d2h30m"`).
- Sign: `-`, `minus`, `negative` all parse as negative (synonyms); formatting always emits a single leading `-`. The sign applies once to the whole compound value.
- Zero/negligible durations (exactly zero, or entirely sub-millisecond) format as `""` (empty string), never a placeholder like `"0 seconds"`.
- `ParseDuration("")` (or whitespace-only) is a deliberate, sole exception to the rest of the library's "empty input is an error" convention: it returns `Success(TimeSpan.Zero)`.
- `ParseDuration` errors if the input contains month/year units (no fixed length) — only Layer 2 supports those, via an anchor.
- Layer 2's `timeZone: null` (default) is Mode 1 (naive, offset-preserving); a non-null `DateTimeZone` is Mode 2 (precise, DST-aware via `DateTimeZone.AtLeniently`).
- No direction restriction anywhere: negative durations and past targets are valid, not exceptional.
- `HumanDurationConverter`'s "now" source is NodaTime's `IClock` (`SystemClock.Instance` in production, `FakeClock` in tests) — **not** `TimeProvider`, for consistency with `QuartzScheduleConverter`'s existing clock-injection pattern and to avoid a new test-only package dependency.
- New public types follow the existing `Create()` factory + internal test constructor pattern (see `UnixCronConverter`).
- All new code lives in `HumanCron` core (Layers 1 & 2) and `HumanCron.Quartz` (Layer 3) — no new package, per the design's "Why not a new package" section.

---

### Task 1: Duration parser and formatter (internal, `Period`-based grammar)

**Files:**
- Create: `src/HumanCron/Parsing/DurationParser.cs`
- Create: `src/HumanCron/Formatting/DurationFormatter.cs`
- Test: `tests/HumanCron.Tests/Parsing/DurationParserTests.cs`
- Test: `tests/HumanCron.Tests/Formatting/DurationFormatterTests.cs`

**Interfaces:**
- Produces: `internal static partial class DurationParser { internal static ParseResult<Period> Parse(string duration); }` — parses a compound, optionally-signed duration string into a NodaTime `Period` (any combination of Years/Months/Weeks/Days/Hours/Minutes/Seconds/Milliseconds may be set). Empty/whitespace input succeeds with `Period.Zero`. Unparseable input returns `Error`.
- Produces: `internal static class DurationFormatter { internal static string Format(Period period); }` — formats any `Period` (fixed-unit-only, or including Years/Months) into the largest-whole-units-descending human string, with a single leading `-` for negative periods, and `""` for a period with no whole-unit components. Used by both Layer 1's `FormatDuration(TimeSpan)` (Task 2) and Layer 2's `ToNaturalDuration` (Task 3).

Both `NodaTime.Period` and `NodaTime.PeriodBuilder` are used here exactly as documented: `Period` has static factories (`FromYears`, `FromMonths`, ... `FromMilliseconds`), a unary negation operator (`-period`), `Normalize()` (folds raw ticks into natural-range Milliseconds/Seconds/Minutes/Hours/Days — **always zeroes the Weeks field**, per its own documentation), and `ToDuration()`/`Duration.ToTimeSpan()` for the fixed-unit case. `PeriodBuilder` exposes settable `int` properties for Years/Months/Weeks/Days and `long` properties for Hours/Minutes/Seconds/Milliseconds, built via `.Build()`.

- [ ] **Step 1: Write the failing tests for `DurationParser`**

```csharp
// tests/HumanCron.Tests/Parsing/DurationParserTests.cs
using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;

namespace HumanCron.Tests.Parsing;

[TestFixture]
public class DurationParserTests
{
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsZeroPeriod(string input)
    {
        var result = DurationParser.Parse(input);

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period, Is.EqualTo(Period.Zero));
    }

    [Test]
    public void Parse_SingleFullWordUnit_ReturnsExpectedPeriod()
    {
        var result = DurationParser.Parse("2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(2));
    }

    [Test]
    public void Parse_SingleAbbreviation_ReturnsExpectedPeriod()
    {
        var result = DurationParser.Parse("30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_MillisecondsAbbreviation_DoesNotCollideWithMinutes()
    {
        var result = DurationParser.Parse("500ms");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Milliseconds, Is.EqualTo(500));
        Assert.That(period.Minutes, Is.EqualTo(0));
    }

    [Test]
    public void Parse_MonthsAbbreviationUppercaseM_DiffersFromMinutes()
    {
        var result = DurationParser.Parse("3M");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Months, Is.EqualTo(3));
        Assert.That(period.Minutes, Is.EqualTo(0));
    }

    [Test]
    public void Parse_CompoundFullWords_ReturnsAllComponents()
    {
        var result = DurationParser.Parse("1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(1));
        Assert.That(period.Hours, Is.EqualTo(2));
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_CompoundAbbreviations_ReturnsAllComponents()
    {
        var result = DurationParser.Parse("1d 2h 30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(1));
        Assert.That(period.Hours, Is.EqualTo(2));
        Assert.That(period.Minutes, Is.EqualTo(30));
    }

    [Test]
    public void Parse_SquashedCompactForm_IsRejected()
    {
        var result = DurationParser.Parse("1d2h30m");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [TestCase("-2 hours")]
    [TestCase("minus 2 hours")]
    [TestCase("negative 2 hours")]
    public void Parse_SignSynonyms_AllProduceNegativePeriod(string input)
    {
        var result = DurationParser.Parse(input);

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(-2));
    }

    [Test]
    public void Parse_NegativeCompound_NegatesEveryComponent()
    {
        var result = DurationParser.Parse("-1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Days, Is.EqualTo(-1));
        Assert.That(period.Hours, Is.EqualTo(-2));
        Assert.That(period.Minutes, Is.EqualTo(-30));
    }

    [Test]
    public void Parse_MonthsAndYears_AreAcceptedAtThisLayer()
    {
        // DurationParser itself is unit-agnostic; the month/year restriction
        // is enforced by HumanDurationConverter.ParseDuration (Task 2), not here.
        var result = DurationParser.Parse("1 year 2 months");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Years, Is.EqualTo(1));
        Assert.That(period.Months, Is.EqualTo(2));
    }

    [Test]
    public void Parse_DuplicateUnitInCompound_SumsValues()
    {
        var result = DurationParser.Parse("1 hour 2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(3));
    }

    [Test]
    public void Parse_UnknownUnit_ReturnsError()
    {
        var result = DurationParser.Parse("2 fortnights");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_Garbage_ReturnsError()
    {
        var result = DurationParser.Parse("not a duration");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_SignWithNoMagnitude_ReturnsError()
    {
        var result = DurationParser.Parse("-");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_ValueExceedingIntRangeForDateUnit_ReturnsError()
    {
        var result = DurationParser.Parse("99999999999 days");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Error>());
    }

    [Test]
    public void Parse_LargeValueForTimeUnit_Succeeds()
    {
        // Hours/Minutes/Seconds/Milliseconds are long-typed in Period, so large
        // values (that would overflow the int-typed date units) are fine here.
        var result = DurationParser.Parse("99999999999 hours");

        Assert.That(result, Is.TypeOf<ParseResult<Period>.Success>());
        var period = ((ParseResult<Period>.Success)result).Value;
        Assert.That(period.Hours, Is.EqualTo(99999999999L));
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~DurationParserTests`
Expected: FAIL (compile error — `DurationParser` doesn't exist yet)

- [ ] **Step 3: Implement `DurationParser`**

```csharp
// src/HumanCron/Parsing/DurationParser.cs
using HumanCron.Models;
using NodaTime;
using System;
using System.Text.RegularExpressions;

namespace HumanCron.Parsing;

/// <summary>
/// Parses natural language duration strings (e.g. "1d 2h 30m") into a NodaTime Period.
/// INTERNAL: unit-agnostic — callers decide which unit combinations are valid for their layer.
/// </summary>
internal static partial class DurationParser
{
    // Longest-alternative-first so "ms" is tried before "m", and every full word is tried
    // before its single-letter abbreviation. Full words use an inline case-insensitive
    // scope (?i:...) so "Hours"/"HOURS" work without making the single-letter tokens
    // case-insensitive too — "m" (minutes) and "M" (months) must stay distinct.
    // Whitespace is OPTIONAL between a value and its own unit (so "30m" and "30 minutes"
    // both work) but MANDATORY ("\s+") between one complete token and the next - that's
    // what rejects a squashed compound like "1d2h30m" while accepting "1d 2h 30m". A named
    // group repeated across the pattern (once outside the trailing "*" group, once inside
    // it) populates one Capture per occurrence, in match order, in .NET's regex engine -
    // so a single anchored match yields every (value, unit) pair via Group.Captures.
    [GeneratedRegex(
        @"^\s*(?<value>\d+)\s*(?<unit>ms|(?i:milliseconds?)|(?i:seconds?)|s|(?i:minutes?)|m|(?i:hours?)|h|(?i:days?)|d|(?i:weeks?)|w|(?i:months?)|M|(?i:years?)|y)(?:\s+(?<value>\d+)\s*(?<unit>ms|(?i:milliseconds?)|(?i:seconds?)|s|(?i:minutes?)|m|(?i:hours?)|h|(?i:days?)|d|(?i:weeks?)|w|(?i:months?)|M|(?i:years?)|y))*\s*$")]
    private static partial Regex DurationBodyPattern();

    internal static ParseResult<Period> Parse(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return new ParseResult<Period>.Success(Period.Zero);
        }

        var trimmed = duration.Trim();
        var (isNegative, body) = ExtractSign(trimmed);

        if (body.Length == 0)
        {
            return new ParseResult<Period>.Error(
                $"Unable to parse duration from: '{duration}'. Expected a magnitude after the sign, e.g. '-2 hours'.");
        }

        var match = DurationBodyPattern().Match(body);
        if (!match.Success)
        {
            return new ParseResult<Period>.Error(
                $"Unable to parse duration from: '{duration}'. Expected a compound duration like " +
                "'1 day 2 hours 30 minutes' or '1d 2h 30m'.");
        }

        var builder = new PeriodBuilder();
        var values = match.Groups["value"].Captures;
        var units = match.Groups["unit"].Captures;
        for (var i = 0; i < values.Count; i++)
        {
            if (!long.TryParse(values[i].Value, out var value))
            {
                return new ParseResult<Period>.Error($"Invalid duration value: {values[i].Value}");
            }

            var error = AddToBuilder(builder, units[i].Value, value);
            if (error is not null)
            {
                return new ParseResult<Period>.Error(error);
            }
        }

        var period = builder.Build();
        return new ParseResult<Period>.Success(isNegative ? -period : period);
    }

    private static (bool isNegative, string body) ExtractSign(string trimmed)
    {
        if (trimmed.StartsWith('-'))
        {
            return (true, trimmed[1..].TrimStart());
        }

        if (trimmed.Length > 5 && trimmed.StartsWith("minus", StringComparison.OrdinalIgnoreCase)
            && char.IsWhiteSpace(trimmed[5]))
        {
            return (true, trimmed[5..].TrimStart());
        }

        if (trimmed.Length > 8 && trimmed.StartsWith("negative", StringComparison.OrdinalIgnoreCase)
            && char.IsWhiteSpace(trimmed[8]))
        {
            return (true, trimmed[8..].TrimStart());
        }

        return (false, trimmed);
    }

    private static string? AddToBuilder(PeriodBuilder builder, string rawUnit, long value)
    {
        var unit = CanonicalUnit(rawUnit);
        switch (unit)
        {
            case "ms":
                builder.Milliseconds += value;
                return null;
            case "s":
                builder.Seconds += value;
                return null;
            case "m":
                builder.Minutes += value;
                return null;
            case "h":
                builder.Hours += value;
                return null;
            case "d":
            case "w":
            case "M":
            case "y":
                if (value > int.MaxValue)
                {
                    return $"Duration value too large: {value}. Maximum is {int.MaxValue}.";
                }

                var intValue = (int)value;
                switch (unit)
                {
                    case "d": builder.Days += intValue; break;
                    case "w": builder.Weeks += intValue; break;
                    case "M": builder.Months += intValue; break;
                    case "y": builder.Years += intValue; break;
                }

                return null;
            default:
                throw new InvalidOperationException($"Unknown duration unit: {rawUnit}");
        }
    }

    private static string CanonicalUnit(string rawUnit)
    {
        if (rawUnit is "ms" or "s" or "m" or "h" or "d" or "w" or "M" or "y")
        {
            return rawUnit;
        }

        var lower = rawUnit.ToLowerInvariant();
        if (lower.StartsWith("millisecond", StringComparison.Ordinal)) return "ms";
        if (lower.StartsWith("second", StringComparison.Ordinal)) return "s";
        if (lower.StartsWith("minute", StringComparison.Ordinal)) return "m";
        if (lower.StartsWith("hour", StringComparison.Ordinal)) return "h";
        if (lower.StartsWith("week", StringComparison.Ordinal)) return "w";
        if (lower.StartsWith("month", StringComparison.Ordinal)) return "M";
        if (lower.StartsWith("year", StringComparison.Ordinal)) return "y";
        if (lower.StartsWith("day", StringComparison.Ordinal)) return "d";
        throw new InvalidOperationException($"Unknown duration unit: {rawUnit}");
    }
}
```

- [ ] **Step 4: Run the `DurationParser` tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~DurationParserTests`
Expected: PASS (17 tests)

- [ ] **Step 5: Write the failing tests for `DurationFormatter`**

```csharp
// tests/HumanCron.Tests/Formatting/DurationFormatterTests.cs
using HumanCron.Formatting;
using NodaTime;

namespace HumanCron.Tests.Formatting;

[TestFixture]
public class DurationFormatterTests
{
    [Test]
    public void Format_ZeroPeriod_ReturnsEmptyString()
    {
        var result = DurationFormatter.Format(Period.Zero);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void Format_SingleUnit_OmitsZeroComponents()
    {
        var period = Period.FromMinutes(1);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 minute"));
    }

    [Test]
    public void Format_PluralValue_UsesPluralWord()
    {
        var period = Period.FromMinutes(30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("30 minutes"));
    }

    [Test]
    public void Format_CompoundPeriod_DescendingLargestUnits()
    {
        var period = Period.FromHours(2) + Period.FromMinutes(30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("2 hours 30 minutes"));
    }

    [Test]
    public void Format_DaysFoldIntoWeeks()
    {
        var period = Period.FromDays(10);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 week 3 days"));
    }

    [Test]
    public void Format_ExactWeek_NoTrailingZeroDays()
    {
        var period = Period.FromDays(7);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 week"));
    }

    [Test]
    public void Format_NegativePeriod_SingleLeadingSign()
    {
        var period = Period.FromHours(-2) + Period.FromMinutes(-30);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("-2 hours 30 minutes"));
    }

    [Test]
    public void Format_MillisecondsIncludedWhenNonzero()
    {
        var period = Period.FromSeconds(1) + Period.FromMilliseconds(500);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 second 500 milliseconds"));
    }

    [Test]
    public void Format_YearsAndMonths_IncludedAsIs()
    {
        var period = Period.FromYears(1) + Period.FromMonths(2);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 year 2 months"));
    }

    [Test]
    public void Format_AllUnitsTogether_FullDescendingOrder()
    {
        var period = Period.FromYears(1) + Period.FromMonths(2) + Period.FromDays(10)
            + Period.FromHours(3) + Period.FromMinutes(4) + Period.FromSeconds(5) + Period.FromMilliseconds(6);

        var result = DurationFormatter.Format(period);

        Assert.That(result, Is.EqualTo("1 year 2 months 1 week 3 days 3 hours 4 minutes 5 seconds 6 milliseconds"));
    }
}
```

- [ ] **Step 6: Run the `DurationFormatter` tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~DurationFormatterTests`
Expected: FAIL (compile error — `DurationFormatter` doesn't exist yet)

- [ ] **Step 7: Implement `DurationFormatter`**

```csharp
// src/HumanCron/Formatting/DurationFormatter.cs
using NodaTime;
using System;
using System.Collections.Generic;

namespace HumanCron.Formatting;

/// <summary>
/// Formats a NodaTime Period as a natural language duration (e.g. "2 hours 30 minutes").
/// INTERNAL: shared by Layer 1 (HumanDurationConverter.FormatDuration) and Layer 2
/// (HumanDurationConverter.ToNaturalDuration) — the only difference is whether the
/// incoming Period ever has nonzero Years/Months.
/// </summary>
internal static class DurationFormatter
{
    internal static string Format(Period period)
    {
        ArgumentNullException.ThrowIfNull(period);

        var isNegative = period.Years < 0 || period.Months < 0 || period.Weeks < 0 || period.Days < 0
            || period.Hours < 0 || period.Minutes < 0 || period.Seconds < 0 || period.Milliseconds < 0;

        var magnitude = isNegative ? -period : period;

        // Period.Normalize() always reports Weeks as 0 (folded into Days), and
        // Period.Between(...) doesn't populate Weeks by default either — so this
        // fold-then-split is required regardless of where the Period came from.
        var totalDays = magnitude.Weeks * 7 + magnitude.Days;
        var weeks = totalDays / 7;
        var days = totalDays % 7;

        var parts = new List<string>();
        AppendPart(parts, magnitude.Years, "year", "years");
        AppendPart(parts, magnitude.Months, "month", "months");
        AppendPart(parts, weeks, "week", "weeks");
        AppendPart(parts, days, "day", "days");
        AppendPart(parts, magnitude.Hours, "hour", "hours");
        AppendPart(parts, magnitude.Minutes, "minute", "minutes");
        AppendPart(parts, magnitude.Seconds, "second", "seconds");
        AppendPart(parts, magnitude.Milliseconds, "millisecond", "milliseconds");

        if (parts.Count == 0)
        {
            return "";
        }

        var body = string.Join(" ", parts);
        return isNegative ? $"-{body}" : body;
    }

    private static void AppendPart(List<string> parts, long value, string singular, string plural)
    {
        if (value == 0)
        {
            return;
        }

        parts.Add(value == 1 ? $"{value} {singular}" : $"{value} {plural}");
    }
}
```

- [ ] **Step 8: Run the `DurationFormatter` tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~DurationFormatterTests`
Expected: PASS (10 tests)

- [ ] **Step 9: Commit**

```bash
git add src/HumanCron/Parsing/DurationParser.cs src/HumanCron/Formatting/DurationFormatter.cs \
  tests/HumanCron.Tests/Parsing/DurationParserTests.cs tests/HumanCron.Tests/Formatting/DurationFormatterTests.cs
git commit -m "feat(duration): add internal Period-based duration parser and formatter"
```

---

### Task 2: Layer 1 public API — `IHumanDurationConverter.ParseDuration`/`FormatDuration`

**Files:**
- Create: `src/HumanCron/Abstractions/IHumanDurationConverter.cs`
- Create: `src/HumanCron/Converters/Duration/HumanDurationConverter.cs`
- Test: `tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs`
- Test: `tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs`

**Interfaces:**
- Consumes: `DurationParser.Parse(string) -> ParseResult<Period>` and `DurationFormatter.Format(Period) -> string` from Task 1.
- Produces: `public interface IHumanDurationConverter { ParseResult<TimeSpan> ParseDuration(string duration); string FormatDuration(TimeSpan duration); }` and `public sealed class HumanDurationConverter : IHumanDurationConverter` with `internal HumanDurationConverter(IClock clock)` and `public static HumanDurationConverter Create()`. Task 3 extends both the interface and this class with `ToFutureTime`/`ToNaturalDuration` — those method bodies are not part of this task.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs
using HumanCron.Abstractions;
using HumanCron.Converters.Duration;
using HumanCron.Models;

namespace HumanCron.Tests.Converters;

[TestFixture]
public class HumanDurationConverterTests
{
    private IHumanDurationConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = HumanDurationConverter.Create();
    }

    [Test]
    public void ParseDuration_SimpleValue_ReturnsExpectedTimeSpan()
    {
        var result = _converter.ParseDuration("2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void ParseDuration_Compound_ReturnsSummedTimeSpan()
    {
        var result = _converter.ParseDuration("1 day 2 hours 30 minutes");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(new TimeSpan(1, 2, 30, 0)));
    }

    [Test]
    public void ParseDuration_Negative_ReturnsNegativeTimeSpan()
    {
        var result = _converter.ParseDuration("-2 hours");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.FromHours(-2)));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ParseDuration_EmptyOrWhitespace_ReturnsZero(string input)
    {
        var result = _converter.ParseDuration(input);

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var value = ((ParseResult<TimeSpan>.Success)result).Value;
        Assert.That(value, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void ParseDuration_ContainsMonths_ReturnsError()
    {
        var result = _converter.ParseDuration("1 month");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_ContainsYears_ReturnsError()
    {
        var result = _converter.ParseDuration("1 year");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_Garbage_ReturnsError()
    {
        var result = _converter.ParseDuration("not a duration");

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void ParseDuration_ExceedsMaxInputLength_ReturnsError()
    {
        var tooLong = new string('1', 1001) + " seconds";

        var result = _converter.ParseDuration(tooLong);

        Assert.That(result, Is.TypeOf<ParseResult<TimeSpan>.Error>());
    }

    [Test]
    public void FormatDuration_SimpleValue_ReturnsExpectedString()
    {
        var result = _converter.FormatDuration(TimeSpan.FromMinutes(150));

        Assert.That(result, Is.EqualTo("2 hours 30 minutes"));
    }

    [Test]
    public void FormatDuration_Zero_ReturnsEmptyString()
    {
        var result = _converter.FormatDuration(TimeSpan.Zero);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void FormatDuration_EntirelySubMillisecond_ReturnsEmptyString()
    {
        var result = _converter.FormatDuration(TimeSpan.FromTicks(50)); // 5 microseconds

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void FormatDuration_Negative_ReturnsLeadingSign()
    {
        var result = _converter.FormatDuration(TimeSpan.FromHours(-2));

        Assert.That(result, Is.EqualTo("-2 hours"));
    }

    [Test]
    public void FormatDuration_IgnoresSubMillisecondRemainder()
    {
        var result = _converter.FormatDuration(TimeSpan.FromMilliseconds(500) + TimeSpan.FromTicks(50));

        Assert.That(result, Is.EqualTo("500 milliseconds"));
    }
}
```

```csharp
// tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs
using HumanCron.Abstractions;
using HumanCron.Converters.Duration;
using HumanCron.Models;

namespace HumanCron.Tests.RoundTrip;

/// <summary>
/// Round-trip tests for Layer 1 (string ↔ TimeSpan), in the spirit of CompleteBidirectionalTests.
/// </summary>
[TestFixture]
public class DurationBidirectionalTests
{
    private IHumanDurationConverter _converter = null!;

    [SetUp]
    public void SetUp()
    {
        _converter = HumanDurationConverter.Create();
    }

    [TestCase(150 * 60_000L)] // 2h30m in ms
    [TestCase(90 * 60_000L)]  // 1h30m in ms
    [TestCase(1_000L)]        // 1 second
    [TestCase(500L)]          // 500 milliseconds
    [TestCase(0L)]
    [TestCase(-150 * 60_000L)]
    public void RoundTrip_FormatThenParse_ReturnsOriginalValue(long milliseconds)
    {
        var original = TimeSpan.FromMilliseconds(milliseconds);

        var formatted = _converter.FormatDuration(original);
        var parseResult = _converter.ParseDuration(formatted);

        Assert.That(parseResult, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var parsed = ((ParseResult<TimeSpan>.Success)parseResult).Value;
        Assert.That(parsed, Is.EqualTo(original));
    }

    [Test]
    public void RoundTrip_ParseThenFormat_NormalizesToCanonicalForm()
    {
        // "90 minutes" is not what the formatter would ever emit (it normalizes to
        // hours+minutes), but parsing it must still succeed and round-trip through
        // the canonical form.
        var parseResult = _converter.ParseDuration("90 minutes");
        Assert.That(parseResult, Is.TypeOf<ParseResult<TimeSpan>.Success>());
        var parsed = ((ParseResult<TimeSpan>.Success)parseResult).Value;

        var formatted = _converter.FormatDuration(parsed);

        Assert.That(formatted, Is.EqualTo("1 hour 30 minutes"));
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~HumanDurationConverterTests|FullyQualifiedName~DurationBidirectionalTests"`
Expected: FAIL (compile error — `IHumanDurationConverter`/`HumanDurationConverter` don't exist yet)

- [ ] **Step 3: Implement `IHumanDurationConverter`**

```csharp
// src/HumanCron/Abstractions/IHumanDurationConverter.cs
using HumanCron.Models;
using System;

namespace HumanCron.Abstractions;

/// <summary>
/// Bidirectional converter between natural language and durations/instants.
/// </summary>
/// <remarks>
/// Separate grammar from schedules (IHumanCronConverter) — durations have no "every"/"on"
/// prefix, so there is no collision.
///
/// Examples:
/// - "1 day 2 hours 30 minutes" ↔ TimeSpan
/// - "1d 2h 30m" → TimeSpan
/// - TimeSpan.FromMinutes(150) → "2 hours 30 minutes"
/// </remarks>
public interface IHumanDurationConverter
{
    /// <summary>
    /// Parse a natural language duration into a fixed-length TimeSpan
    /// </summary>
    /// <param name="duration">
    /// Natural language duration (e.g., "1 day 2 hours 30 minutes", "1d 2h 30m").
    /// Empty or whitespace-only input returns TimeSpan.Zero, not an error.
    /// </param>
    /// <returns>
    /// The parsed TimeSpan, or an Error if the duration contains month/year units (no fixed
    /// length — use ToFutureTime/ToNaturalDuration for calendar-aware math) or is unparseable.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = converter.ParseDuration("2 hours 30 minutes");
    /// if (result is ParseResult&lt;TimeSpan&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // 02:30:00
    /// }
    /// </code>
    /// </example>
    ParseResult<TimeSpan> ParseDuration(string duration);

    /// <summary>
    /// Format a TimeSpan as a natural language duration
    /// </summary>
    /// <param name="duration">The duration to format. May be negative.</param>
    /// <returns>
    /// Natural language duration (e.g., "2 hours 30 minutes"), with a single leading "-" for
    /// negative values, or an empty string for a zero or entirely sub-millisecond duration.
    /// </returns>
    /// <example>
    /// <code>
    /// converter.FormatDuration(TimeSpan.FromMinutes(150)); // "2 hours 30 minutes"
    /// converter.FormatDuration(TimeSpan.Zero); // ""
    /// </code>
    /// </example>
    string FormatDuration(TimeSpan duration);
}
```

- [ ] **Step 4: Implement `HumanDurationConverter`**

```csharp
// src/HumanCron/Converters/Duration/HumanDurationConverter.cs
using HumanCron.Abstractions;
using HumanCron.Formatting;
using HumanCron.Models;
using HumanCron.Parsing;
using NodaTime;
using System;

namespace HumanCron.Converters.Duration;

/// <summary>
/// Converts between natural language and durations/instants
/// Provides bidirectional conversion: natural language ↔ TimeSpan/DateTimeOffset
/// </summary>
public sealed class HumanDurationConverter : IHumanDurationConverter
{
    // Maximum input length to prevent DoS attacks via extremely long strings
    private const int MaxInputLength = 1000;

    private readonly IClock _clock;

    /// <summary>
    /// Internal constructor for dependency injection (tests only)
    /// </summary>
    /// <param name="clock">Clock for date/time operations (SystemClock.Instance in production, FakeClock in tests)</param>
    internal HumanDurationConverter(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Create a new duration converter (production use)
    /// </summary>
    public static HumanDurationConverter Create() => new(SystemClock.Instance);

    /// <inheritdoc/>
    public ParseResult<TimeSpan> ParseDuration(string duration)
    {
        if (duration is not null && duration.Length > MaxInputLength)
        {
            return new ParseResult<TimeSpan>.Error(
                $"Duration input exceeds maximum length of {MaxInputLength} characters");
        }

        var parseResult = DurationParser.Parse(duration);
        if (parseResult is not ParseResult<Period>.Success success)
        {
            var error = (ParseResult<Period>.Error)parseResult;
            return new ParseResult<TimeSpan>.Error(error.Message);
        }

        var period = success.Value;
        if (period.Years != 0 || period.Months != 0)
        {
            return new ParseResult<TimeSpan>.Error(
                $"'{duration}' contains a calendar unit (months/years) with no fixed length. " +
                "Use ToFutureTime/ToNaturalDuration for calendar-aware math against an anchor.");
        }

        return new ParseResult<TimeSpan>.Success(period.ToDuration().ToTimeSpan());
    }

    /// <inheritdoc/>
    public string FormatDuration(TimeSpan duration)
    {
        var period = Period.FromTicks(duration.Ticks).Normalize();
        return DurationFormatter.Format(period);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~HumanDurationConverterTests|FullyQualifiedName~DurationBidirectionalTests"`
Expected: PASS (13 + 7 = 20 tests)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: All tests pass (previous count + 47 new tests from Tasks 1–2)

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron/Abstractions/IHumanDurationConverter.cs src/HumanCron/Converters/Duration/HumanDurationConverter.cs \
  tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs
git commit -m "feat(duration): add Layer 1 public API - ParseDuration/FormatDuration"
```

---

### Task 3: Layer 2 — anchored instant math (`ToFutureTime`/`ToNaturalDuration`)

**Files:**
- Modify: `src/HumanCron/Abstractions/IHumanDurationConverter.cs` (add two methods)
- Modify: `src/HumanCron/Converters/Duration/HumanDurationConverter.cs` (implement them)
- Modify: `tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs` (add test methods)
- Modify: `tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs` (add anchored round-trip tests)

**Interfaces:**
- Consumes: `HumanDurationConverter._clock : IClock` (from Task 2's constructor), `DurationParser.Parse`, `DurationFormatter.Format`.
- Produces: `ParseResult<DateTimeOffset> ToFutureTime(string duration, DateTimeOffset? anchor = null, DateTimeZone? timeZone = null)` and `ParseResult<string> ToNaturalDuration(DateTimeOffset target, DateTimeOffset? anchor = null, DateTimeZone? timeZone = null)` — used by Task 4's `CreateOneTimeTriggerBuilder`.

The NodaTime API used here — `OffsetDateTime.FromDateTimeOffset`, `.LocalDateTime`, `.Offset`, the `OffsetDateTime(LocalDateTime, Offset)` constructor, `.ToDateTimeOffset()`, `Instant.FromDateTimeOffset`, `.InZone(DateTimeZone)`, `ZonedDateTime.LocalDateTime`, `LocalDateTime.Plus(Period)`, `DateTimeZone.AtLeniently(LocalDateTime)`, and `Period.Between(LocalDateTime, LocalDateTime)` — has been verified directly against the referenced NodaTime assembly (not assumed from memory).

- [ ] **Step 1: Write the failing tests**

Add to `tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs` (inside the existing `HumanDurationConverterTests` class, alongside a new `using` for `NodaTime` at the top of the file):

```csharp
// Add near the top of the file:
using NodaTime;

// Add inside the HumanDurationConverterTests class:

[Test]
public void ToFutureTime_WithExplicitAnchor_AddsFixedDuration()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.ToFutureTime("2 hours", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
    Assert.That(value, Is.EqualTo(anchor.AddHours(2)));
}

[Test]
public void ToFutureTime_NegativeDuration_SubtractsFromAnchor()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.ToFutureTime("-2 hours", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
    Assert.That(value, Is.EqualTo(anchor.AddHours(-2)));
}

[Test]
public void ToFutureTime_NaiveMode_MonthEndRollover_ClampsLikeAddMonths()
{
    var anchor = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.ToFutureTime("1 month", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
    Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)));
}

[Test]
public void ToFutureTime_NaiveMode_LeapYearMonthEndRollover_UsesFeb29()
{
    var anchor = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.ToFutureTime("1 month", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
    Assert.That(value, Is.EqualTo(new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero)));
}

[Test]
public void ToFutureTime_NaiveMode_KeepsOriginalOffsetAcrossDstTransition()
{
    // US Eastern springs forward on 2026-03-08 at 2am local (EST -05:00 -> EDT -04:00).
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

    var result = _converter.ToFutureTime("1 month", anchor); // no timeZone -> Mode 1, naive

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;

    // Naive mode keeps the -05:00 offset verbatim, which is WRONG for April 8th in
    // New York (should be -04:00) - this is the documented Mode 1 imprecision.
    Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-5))));
    Assert.That(value.Offset, Is.Not.EqualTo(newYork.GetUtcOffset(Instant.FromDateTimeOffset(value)).ToTimeSpan()));
}

[Test]
public void ToFutureTime_PreciseMode_ResolvesCorrectOffsetAcrossDstTransition()
{
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

    var result = _converter.ToFutureTime("1 month", anchor, newYork); // Mode 2, precise

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;

    // April 8th in New York is EDT (-04:00), not EST (-05:00).
    Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-4))));
}

[Test]
public void ToFutureTime_FixedUnitDuration_SameResultRegardlessOfMode()
{
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

    var naive = ((ParseResult<DateTimeOffset>.Success)_converter.ToFutureTime("90 minutes", anchor)).Value;
    var precise = ((ParseResult<DateTimeOffset>.Success)_converter.ToFutureTime("90 minutes", anchor, newYork)).Value;

    Assert.That(naive.ToUniversalTime(), Is.EqualTo(precise.ToUniversalTime()));
}

[Test]
public void ToFutureTime_NullAnchor_UsesInjectedClock()
{
    var fakeClock = new FakeClock(Instant.FromUtc(2026, 6, 1, 12, 0, 0));
    var converter = new HumanDurationConverter(fakeClock);

    var result = converter.ToFutureTime("2 hours");

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var value = ((ParseResult<DateTimeOffset>.Success)result).Value;
    Assert.That(value, Is.EqualTo(new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero)));
}

[Test]
public void ToFutureTime_InvalidDuration_ReturnsError()
{
    var result = _converter.ToFutureTime("not a duration");

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Error>());
}

[Test]
public void ToFutureTime_ExceedsMaxInputLength_ReturnsError()
{
    var tooLong = new string('1', 1001) + " seconds";

    var result = _converter.ToFutureTime(tooLong);

    Assert.That(result, Is.TypeOf<ParseResult<DateTimeOffset>.Error>());
}

[Test]
public void ToNaturalDuration_ExplicitAnchorInFuture_FormatsPositiveDuration()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var target = anchor.AddHours(2);

    var result = _converter.ToNaturalDuration(target, anchor);

    Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("2 hours"));
}

[Test]
public void ToNaturalDuration_TargetBeforeAnchor_FormatsNegativeDuration()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);
    var target = anchor.AddHours(-2);

    var result = _converter.ToNaturalDuration(target, anchor);

    Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("-2 hours"));
}

[Test]
public void ToNaturalDuration_SameInstantAsAnchor_ReturnsEmptyString()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.ToNaturalDuration(anchor, anchor);

    Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo(""));
}

[Test]
public void ToNaturalDuration_PreciseMode_CalendarUnitsAcrossDst()
{
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));
    var target = new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-4));

    var result = _converter.ToNaturalDuration(target, anchor, newYork);

    Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("1 month"));
}

[Test]
public void ToNaturalDuration_NullAnchor_UsesInjectedClock()
{
    var fakeClock = new FakeClock(Instant.FromUtc(2026, 6, 1, 12, 0, 0));
    var converter = new HumanDurationConverter(fakeClock);
    var target = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero);

    var result = converter.ToNaturalDuration(target);

    Assert.That(result, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)result).Value, Is.EqualTo("2 hours"));
}
```

Add to `tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs` (add `using NodaTime;` at the top):

```csharp
[Test]
public void RoundTrip_AnchoredFixedUnit_ToFutureTimeThenToNaturalDuration()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var futureResult = _converter.ToFutureTime("2 hours 30 minutes", anchor);
    Assert.That(futureResult, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var target = ((ParseResult<DateTimeOffset>.Success)futureResult).Value;

    var naturalResult = _converter.ToNaturalDuration(target, anchor);
    Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)naturalResult).Value, Is.EqualTo("2 hours 30 minutes"));
}

[Test]
public void RoundTrip_AnchoredCalendarUnit_PreciseMode_RoundTripsExactly()
{
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

    var futureResult = _converter.ToFutureTime("1 month", anchor, newYork);
    Assert.That(futureResult, Is.TypeOf<ParseResult<DateTimeOffset>.Success>());
    var target = ((ParseResult<DateTimeOffset>.Success)futureResult).Value;

    var naturalResult = _converter.ToNaturalDuration(target, anchor, newYork);
    Assert.That(naturalResult, Is.TypeOf<ParseResult<string>.Success>());
    Assert.That(((ParseResult<string>.Success)naturalResult).Value, Is.EqualTo("1 month"));
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~HumanDurationConverterTests|FullyQualifiedName~DurationBidirectionalTests"`
Expected: FAIL (compile error — `ToFutureTime`/`ToNaturalDuration` don't exist yet)

- [ ] **Step 3: Add the two methods to `IHumanDurationConverter`**

Add to `src/HumanCron/Abstractions/IHumanDurationConverter.cs`, inside the interface (add `using NodaTime;` to the file's using block):

```csharp
    /// <summary>
    /// Parse a natural language duration and add it to an anchor instant, producing a future
    /// (or past, if the duration is negative) DateTimeOffset
    /// </summary>
    /// <param name="duration">
    /// Natural language duration, including month/year units (e.g., "2 hours", "1 month")
    /// </param>
    /// <param name="anchor">Anchor instant (null = now, via the converter's injected clock)</param>
    /// <param name="timeZone">
    /// Governs precision for month/year components only. Null (default) uses Mode 1: naive,
    /// offset-preserving math - cheap, but can be off by up to an hour across a DST boundary
    /// when the duration includes month/year units. A non-null zone uses Mode 2: precise,
    /// DST-aware math via NodaTime's ZonedDateTime and DateTimeZone.AtLeniently. Fixed-unit-only
    /// durations (hours/minutes/seconds/days/weeks) produce the identical result either way.
    /// </param>
    /// <returns>The computed instant, or an Error if the duration is unparseable</returns>
    /// <example>
    /// <code>
    /// var result = converter.ToFutureTime("2 hours");
    /// if (result is ParseResult&lt;DateTimeOffset&gt;.Success success)
    /// {
    ///     Console.WriteLine(success.Value); // now + 2 hours
    /// }
    /// </code>
    /// </example>
    ParseResult<DateTimeOffset> ToFutureTime(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null);

    /// <summary>
    /// Format the difference between a target instant and an anchor as a natural language duration
    /// </summary>
    /// <param name="target">The instant to describe, relative to the anchor</param>
    /// <param name="anchor">Anchor instant (null = now, via the converter's injected clock)</param>
    /// <param name="timeZone">
    /// Governs precision for calendar-unit decomposition (see ToFutureTime). Null (default) uses
    /// Mode 1: naive math on each value's own embedded offset. A non-null zone uses Mode 2:
    /// resolves both instants through that zone before diffing.
    /// </param>
    /// <returns>
    /// Natural language duration (e.g., "1 hour 23 minutes"). A target before the anchor formats
    /// with a leading "-", same as a negative TimeSpan. Never errors - target/anchor are always
    /// valid DateTimeOffset values.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = converter.ToNaturalDuration(unfreezeAt, syncCompletedAt);
    /// // ParseResult&lt;string&gt;.Success("2 hours")
    /// </code>
    /// </example>
    ParseResult<string> ToNaturalDuration(
        DateTimeOffset target,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null);
```

- [ ] **Step 4: Implement the two methods on `HumanDurationConverter`**

Add to `src/HumanCron/Converters/Duration/HumanDurationConverter.cs` (add `using System;` already present; the class already has access to `_clock`):

```csharp
    /// <inheritdoc/>
    public ParseResult<DateTimeOffset> ToFutureTime(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null)
    {
        if (duration is not null && duration.Length > MaxInputLength)
        {
            return new ParseResult<DateTimeOffset>.Error(
                $"Duration input exceeds maximum length of {MaxInputLength} characters");
        }

        var parseResult = DurationParser.Parse(duration);
        if (parseResult is not ParseResult<Period>.Success success)
        {
            var error = (ParseResult<Period>.Error)parseResult;
            return new ParseResult<DateTimeOffset>.Error(error.Message);
        }

        var period = success.Value;
        var anchorValue = anchor ?? _clock.GetCurrentInstant().ToDateTimeOffset();

        var result = timeZone is null
            ? AddPeriodNaive(anchorValue, period)
            : AddPeriodPrecise(anchorValue, period, timeZone);

        return new ParseResult<DateTimeOffset>.Success(result);
    }

    /// <inheritdoc/>
    public ParseResult<string> ToNaturalDuration(
        DateTimeOffset target,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null)
    {
        var anchorValue = anchor ?? _clock.GetCurrentInstant().ToDateTimeOffset();

        var period = timeZone is null
            ? PeriodBetweenNaive(anchorValue, target)
            : PeriodBetweenPrecise(anchorValue, target, timeZone);

        return new ParseResult<string>.Success(DurationFormatter.Format(period));
    }

    // Splits a Period into its calendar component (Years/Months - navigated via LocalDateTime,
    // which is where DST/month-end ambiguity actually lives) and its fixed component (Weeks/
    // Days/Hours/Minutes/Seconds/Milliseconds - added as a physical Duration, which is DST-
    // agnostic by construction). Without this split, adding the WHOLE period through
    // LocalDateTime.Plus would make even a plain "90 minutes" sensitive to DST whenever the
    // addition straddles a transition - which contradicts "fixed-unit durations produce the
    // identical result under both modes" and was caught by testing AddPeriodNaive/
    // AddPeriodPrecise against a DST boundary before this split existed.
    private static Period CalendarOnly(Period period) =>
        new PeriodBuilder { Years = period.Years, Months = period.Months }.Build();

    private static Duration FixedDuration(Period period) =>
        new PeriodBuilder
        {
            Weeks = period.Weeks,
            Days = period.Days,
            Hours = period.Hours,
            Minutes = period.Minutes,
            Seconds = period.Seconds,
            Milliseconds = period.Milliseconds
        }.Build().ToDuration();

    private static DateTimeOffset AddPeriodNaive(DateTimeOffset anchor, Period period)
    {
        var offsetDateTime = OffsetDateTime.FromDateTimeOffset(anchor);
        var afterCalendar = new OffsetDateTime(
            offsetDateTime.LocalDateTime.Plus(CalendarOnly(period)), offsetDateTime.Offset);
        return afterCalendar.Plus(FixedDuration(period)).ToDateTimeOffset();
    }

    private static DateTimeOffset AddPeriodPrecise(DateTimeOffset anchor, Period period, DateTimeZone timeZone)
    {
        var zonedDateTime = Instant.FromDateTimeOffset(anchor).InZone(timeZone);
        var afterCalendar = timeZone.AtLeniently(zonedDateTime.LocalDateTime.Plus(CalendarOnly(period)));
        return afterCalendar.Plus(FixedDuration(period)).ToDateTimeOffset();
    }

    private static Period PeriodBetweenNaive(DateTimeOffset anchor, DateTimeOffset target)
    {
        var anchorLocal = OffsetDateTime.FromDateTimeOffset(anchor).LocalDateTime;
        var targetLocal = OffsetDateTime.FromDateTimeOffset(target).LocalDateTime;
        return Period.Between(anchorLocal, targetLocal);
    }

    private static Period PeriodBetweenPrecise(DateTimeOffset anchor, DateTimeOffset target, DateTimeZone timeZone)
    {
        var anchorLocal = Instant.FromDateTimeOffset(anchor).InZone(timeZone).LocalDateTime;
        var targetLocal = Instant.FromDateTimeOffset(target).InZone(timeZone).LocalDateTime;
        return Period.Between(anchorLocal, targetLocal);
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter "FullyQualifiedName~HumanDurationConverterTests|FullyQualifiedName~DurationBidirectionalTests"`
Expected: PASS (13 + 15 existing/new HumanDurationConverterTests methods, 7 + 2 DurationBidirectionalTests methods)

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: All tests pass

- [ ] **Step 7: Commit**

```bash
git add src/HumanCron/Abstractions/IHumanDurationConverter.cs src/HumanCron/Converters/Duration/HumanDurationConverter.cs \
  tests/HumanCron.Tests/Converters/HumanDurationConverterTests.cs tests/HumanCron.Tests/RoundTrip/DurationBidirectionalTests.cs
git commit -m "feat(duration): add Layer 2 anchored instant math - ToFutureTime/ToNaturalDuration"
```

---

### Task 4: Layer 3 — Quartz one-time trigger sugar (`CreateOneTimeTriggerBuilder`)

**Files:**
- Modify: `src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs` (add `SimpleScheduleBuilder` overload)
- Modify: `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs` (add method)
- Modify: `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs` (implement method, store a `HumanDurationConverter`)
- Test: `tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs` (add `SimpleScheduleBuilder` misfire tests)
- Test: `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs` (add `CreateOneTimeTriggerBuilder` tests)

**Interfaces:**
- Consumes: `HumanDurationConverter` (public class, `internal HumanDurationConverter(IClock)` constructor) from Task 2/3, already constructible from the `IClock clock` parameter `QuartzScheduleConverter`'s internal constructor already receives.
- Produces: `ParseResult<TriggerBuilder> CreateOneTimeTriggerBuilder(string duration, DateTimeOffset? anchor = null, DateTimeZone? timeZone = null, int misfireInstruction = 0)` on `IQuartzScheduleConverter`/`QuartzScheduleConverter`.

Quartz's `SimpleScheduleBuilder` has its own misfire-instruction method names (`WithMisfireHandlingInstructionFireNow()`, `WithMisfireHandlingInstructionNextWithRemainingCount()`, etc.) that are **not** the same as `CronScheduleBuilder`'s (`WithMisfireHandlingInstructionFireAndProceed()`, `WithMisfireHandlingInstructionDoNothing()`) — verified directly against the Quartz assembly. `MisfireInstructionHelper` needs its own `SimpleScheduleBuilder` overload rather than reusing the Cron/CalendarInterval mapping table, since misfire value `2` means something different for each trigger type.

- [ ] **Step 1: Write the failing tests for the `MisfireInstructionHelper` overload**

Add to `tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs` (inside the existing `QuartzMisfireInstructionTests` class; add `using HumanCron.Quartz.Helpers;` to the file's using block):

```csharp
#region SimpleScheduleBuilder (one-time trigger) Misfire Tests

[Test]
public void ApplyMisfireInstruction_SimpleSchedule_SmartPolicy_ReturnsUnmodifiedBuilder()
{
    var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

    var result = MisfireInstructionHelper.ApplyMisfireInstruction(builder);

    var trigger = (ISimpleTrigger)TriggerBuilder.Create().WithSchedule(result).Build();
    Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SmartPolicy));
}

[Test]
public void ApplyMisfireInstruction_SimpleSchedule_IgnoreMisfires_AppliesCorrectly()
{
    var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

    var result = MisfireInstructionHelper.ApplyMisfireInstruction(builder, MisfireInstruction.IgnoreMisfirePolicy);

    var trigger = (ISimpleTrigger)TriggerBuilder.Create().WithSchedule(result).Build();
    Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
}

[Test]
public void ApplyMisfireInstruction_SimpleSchedule_FireNow_AppliesCorrectly()
{
    var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

    var result = MisfireInstructionHelper.ApplyMisfireInstruction(builder, MisfireInstruction.SimpleTrigger.FireNow);

    var trigger = (ISimpleTrigger)TriggerBuilder.Create().WithSchedule(result).Build();
    Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.SimpleTrigger.FireNow));
}

[Test]
public void ApplyMisfireInstruction_SimpleSchedule_UnknownValue_ThrowsArgumentOutOfRangeException()
{
    var builder = SimpleScheduleBuilder.Create().WithRepeatCount(0);

    Assert.Throws<ArgumentOutOfRangeException>(() =>
        MisfireInstructionHelper.ApplyMisfireInstruction(builder, 999));
}

#endregion
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~QuartzMisfireInstructionTests`
Expected: FAIL (compile error — no `SimpleScheduleBuilder` overload of `ApplyMisfireInstruction` exists yet)

- [ ] **Step 3: Add the `SimpleScheduleBuilder` overload to `MisfireInstructionHelper`**

Add to `src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs`, after the existing `CalendarIntervalScheduleBuilder` overload and before the polymorphic `IScheduleBuilder` dispatch method:

```csharp
    /// <summary>
    /// Apply Quartz misfire instruction to a SimpleScheduleBuilder (one-time or fixed-interval triggers)
    /// </summary>
    /// <param name="builder">The simple schedule builder</param>
    /// <param name="misfireInstruction">
    /// Quartz misfire instruction constant (0 = default/SmartPolicy, no action taken).
    /// Use constants from Quartz.MisfireInstruction.SimpleTrigger. Note these differ in meaning
    /// from Quartz.MisfireInstruction.CronTrigger/CalendarIntervalTrigger - only 0 (SmartPolicy)
    /// and -1 (IgnoreMisfirePolicy) share the same meaning across all trigger types.
    /// </param>
    /// <returns>The builder with misfire instruction applied</returns>
    public static SimpleScheduleBuilder ApplyMisfireInstruction(
        SimpleScheduleBuilder builder,
        int misfireInstruction = 0)
    {
        return misfireInstruction switch
        {
            0 => builder, // SmartPolicy - don't call any method, use Quartz default
            -1 => builder.WithMisfireHandlingInstructionIgnoreMisfires(), // IgnoreMisfirePolicy
            1 => builder.WithMisfireHandlingInstructionFireNow(), // SimpleTrigger.FireNow
            2 => builder.WithMisfireHandlingInstructionNextWithRemainingCount(),
            3 => builder.WithMisfireHandlingInstructionNextWithExistingCount(),
            4 => builder.WithMisfireHandlingInstructionNowWithRemainingCount(),
            5 => builder.WithMisfireHandlingInstructionNowWithExistingCount(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(misfireInstruction),
                misfireInstruction,
                $"Unknown misfire instruction value: {misfireInstruction}. " +
                $"Use constants from Quartz.MisfireInstruction.SimpleTrigger")
        };
    }
```

Then update the polymorphic dispatch method's `switch` expression to add a case:

```csharp
    public static IScheduleBuilder ApplyMisfireInstruction(
        IScheduleBuilder builder,
        int misfireInstruction = 0)
    {
        return builder switch
        {
            CronScheduleBuilder cronBuilder => ApplyMisfireInstruction(cronBuilder, misfireInstruction),
            CalendarIntervalScheduleBuilder calendarBuilder => ApplyMisfireInstruction(calendarBuilder, misfireInstruction),
            SimpleScheduleBuilder simpleBuilder => ApplyMisfireInstruction(simpleBuilder, misfireInstruction),
            _ => throw new NotSupportedException(
                $"Misfire instruction application is not supported for builder type: {builder.GetType().Name}")
        };
    }
```

- [ ] **Step 4: Run the `MisfireInstructionHelper` tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~QuartzMisfireInstructionTests`
Expected: PASS (all existing tests + 4 new ones)

- [ ] **Step 5: Write the failing tests for `CreateOneTimeTriggerBuilder`**

Add to `tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs` (inside the existing `QuartzScheduleConverterTests` class; add `using NodaTime;` to the file's using block):

```csharp
#region CreateOneTimeTriggerBuilder() - One-Time Duration-Based Trigger

[Test]
public void CreateOneTimeTriggerBuilder_ExplicitAnchor_SetsCorrectStartTime()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.CreateOneTimeTriggerBuilder("2 hours", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
    var success = (ParseResult<TriggerBuilder>.Success)result;
    var trigger = success.Value.WithIdentity("test").Build();

    Assert.That(trigger, Is.InstanceOf<ISimpleTrigger>());
    Assert.That(trigger.StartTimeUtc, Is.EqualTo(anchor.AddHours(2).ToUniversalTime()));
}

[Test]
public void CreateOneTimeTriggerBuilder_FiresExactlyOnce()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.CreateOneTimeTriggerBuilder("2 hours", anchor);

    Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
    var success = (ParseResult<TriggerBuilder>.Success)result;
    var trigger = success.Value.WithIdentity("test").Build();

    var firstFire = trigger.GetFireTimeAfter(anchor.ToUniversalTime().AddSeconds(-1));
    Assert.That(firstFire, Is.EqualTo(anchor.AddHours(2).ToUniversalTime()));

    var secondFire = trigger.GetFireTimeAfter(firstFire);
    Assert.That(secondFire, Is.Null);
}

[Test]
public void CreateOneTimeTriggerBuilder_WithTimeZone_UsesPreciseMode()
{
    var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    var anchor = new DateTimeOffset(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

    var result = _converter.CreateOneTimeTriggerBuilder("1 month", anchor, newYork);

    Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
    var success = (ParseResult<TriggerBuilder>.Success)result;
    var trigger = success.Value.WithIdentity("test").Build();

    var expected = new DateTimeOffset(2026, 4, 8, 1, 30, 0, TimeSpan.FromHours(-4));
    Assert.That(trigger.StartTimeUtc, Is.EqualTo(expected.ToUniversalTime()));
}

[Test]
public void CreateOneTimeTriggerBuilder_MisfireInstruction_AppliesToTrigger()
{
    var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var result = _converter.CreateOneTimeTriggerBuilder(
        "2 hours", anchor, misfireInstruction: MisfireInstruction.IgnoreMisfirePolicy);

    Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Success>());
    var success = (ParseResult<TriggerBuilder>.Success)result;
    var trigger = (ISimpleTrigger)success.Value.WithIdentity("test").Build();

    Assert.That(trigger.MisfireInstruction, Is.EqualTo(MisfireInstruction.IgnoreMisfirePolicy));
}

[Test]
public void CreateOneTimeTriggerBuilder_InvalidDuration_ReturnsError()
{
    var result = _converter.CreateOneTimeTriggerBuilder("not a duration");

    Assert.That(result, Is.TypeOf<ParseResult<TriggerBuilder>.Error>());
}

#endregion
```

- [ ] **Step 6: Run the new tests to verify they fail**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~QuartzScheduleConverterTests`
Expected: FAIL (compile error — `CreateOneTimeTriggerBuilder` doesn't exist yet)

- [ ] **Step 7: Add `CreateOneTimeTriggerBuilder` to `IQuartzScheduleConverter`**

Add to `src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs`, inside the interface (add `using System;` and `using NodaTime;` to the file's using block if not already present):

```csharp
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
    /// Quartz misfire instruction constant (default: 0 = SmartPolicy).
    /// Use constants from Quartz.MisfireInstruction.SimpleTrigger
    /// </param>
    /// <returns>ParseResult with TriggerBuilder pre-configured with StartAt and no repeating schedule</returns>
    /// <example>
    /// <code>
    /// // Schedule a one-time job 2 hours from now
    /// var result = converter.CreateOneTimeTriggerBuilder("2 hours");
    /// if (result is ParseResult&lt;TriggerBuilder&gt;.Success success)
    /// {
    ///     var trigger = success.Value
    ///         .WithIdentity("unfreezeTrigger", "myGroup")
    ///         .ForJob("unfreezeJob", "myJobGroup")
    ///         .Build();
    /// }
    /// </code>
    /// </example>
    ParseResult<TriggerBuilder> CreateOneTimeTriggerBuilder(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null,
        int misfireInstruction = 0);
```

- [ ] **Step 8: Implement `CreateOneTimeTriggerBuilder` on `QuartzScheduleConverter`**

Modify `src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs`:

1. Add `using HumanCron.Converters.Duration;` to the using block.
2. Add a new private field alongside the existing ones:

```csharp
    private readonly IHumanDurationConverter _durationConverter;
```

3. In the internal constructor, add one line initializing it from the same `clock` parameter already received:

```csharp
    internal QuartzScheduleConverter(IScheduleParser parser, IScheduleFormatter formatter, IClock clock, DateTimeZone localTimeZone)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
        _quartzBuilder = new QuartzScheduleBuilder(clock ?? throw new ArgumentNullException(nameof(clock)));
        _quartzParser = new QuartzScheduleParser();
        _durationConverter = new HumanDurationConverter(clock);
    }
```

(Note: `clock` is already null-checked by the existing `_quartzBuilder` initialization line above it, which throws first if null — `_durationConverter`'s line runs after that check has already passed.)

4. Add the method implementation, near `CreateTriggerBuilder`:

```csharp
    /// <inheritdoc/>
    public ParseResult<TriggerBuilder> CreateOneTimeTriggerBuilder(
        string duration,
        DateTimeOffset? anchor = null,
        DateTimeZone? timeZone = null,
        int misfireInstruction = 0)
    {
        var futureTimeResult = _durationConverter.ToFutureTime(duration, anchor, timeZone);
        if (futureTimeResult is not ParseResult<DateTimeOffset>.Success success)
        {
            var error = (ParseResult<DateTimeOffset>.Error)futureTimeResult;
            return new ParseResult<TriggerBuilder>.Error(error.Message);
        }

        var scheduleBuilder = MisfireInstructionHelper.ApplyMisfireInstruction(
            SimpleScheduleBuilder.Create().WithRepeatCount(0),
            misfireInstruction);

        var triggerBuilder = TriggerBuilder.Create()
            .WithSchedule(scheduleBuilder)
            .StartAt(success.Value.ToUniversalTime());

        return new ParseResult<TriggerBuilder>.Success(triggerBuilder);
    }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/HumanCron.Tests/HumanCron.Tests.csproj --filter FullyQualifiedName~QuartzScheduleConverterTests`
Expected: PASS (all existing tests + 5 new ones)

- [ ] **Step 10: Run the full suite to confirm no regressions**

Run: `dotnet test HumanCron.slnx`
Expected: All tests pass

- [ ] **Step 11: Commit**

```bash
git add src/HumanCron.Quartz/Helpers/MisfireInstructionHelper.cs src/HumanCron.Quartz/Abstractions/IQuartzScheduleConverter.cs \
  src/HumanCron.Quartz/Converters/QuartzScheduleConverter.cs \
  tests/HumanCron.Tests/Quartz/QuartzMisfireInstructionTests.cs tests/HumanCron.Tests/Converters/QuartzScheduleConverterTests.cs
git commit -m "feat(duration): add Quartz one-time trigger sugar - CreateOneTimeTriggerBuilder"
```

---

### Task 5: Documentation and final verification

**Files:**
- Modify: `src/HumanCron/INTEGRATION.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: `IHumanDurationConverter`/`HumanDurationConverter` (Tasks 2–3), `IQuartzScheduleConverter.CreateOneTimeTriggerBuilder` (Task 4). No new interfaces produced — this task is documentation plus a final whole-suite check.

- [ ] **Step 1: Add a duration section to `src/HumanCron/INTEGRATION.md`**

Read the current file first (`src/HumanCron/INTEGRATION.md`) to find where the existing `ScheduleParserOptions`/options-overload section ends, then append a new section directly after it:

```markdown
## Duration Parsing (Bidirectional)

Separate from schedule parsing - durations have no `every`/`on` prefix, so there's no grammar
collision. `HumanDurationConverter` provides three tiers:

```csharp
using HumanCron.Converters.Duration;

var converter = HumanDurationConverter.Create();

// Layer 1: pure TimeSpan (no anchor, no calendar units)
var parsed = converter.ParseDuration("1 day 2 hours 30 minutes"); // ParseResult<TimeSpan>
var formatted = converter.FormatDuration(TimeSpan.FromMinutes(150)); // "2 hours 30 minutes"

// Layer 2: anchored instant math (calendar units - months/years - supported here)
var futureTime = converter.ToFutureTime("2 hours"); // ParseResult<DateTimeOffset>, anchored at now
var natural = converter.ToNaturalDuration(unfreezeAt, syncCompletedAt); // ParseResult<string>

// Layer 2 with an explicit timezone for DST-aware month/year math
var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];
var precise = converter.ToFutureTime("1 month", anchor, newYork);
```

`timeZone: null` (the default) uses naive, offset-preserving math for month/year components -
cheap, but can be off by up to an hour across a DST boundary. Passing an explicit `DateTimeZone`
resolves the correct offset via NodaTime. This distinction only matters for month/year
durations; fixed-unit durations (hours/minutes/seconds/days/weeks) are unaffected either way.

Accepted unit vocabulary: full words (`second(s)` ... `year(s)`, including `millisecond(s)`,
which has no schedule-side equivalent) and abbreviations `ms`, `s`, `m` (minutes), `h`, `d`,
`w`, `M` (months), `y`. Compound durations are space-separated: `"1d 2h 30m"`. A leading `-`,
`minus`, or `negative` parses as negative; formatting always emits a leading `-`.

### Quartz one-time triggers

```csharp
using HumanCron.Quartz;

var converter = QuartzScheduleConverterFactory.Create();
var result = converter.CreateOneTimeTriggerBuilder("2 hours");
if (result is ParseResult<TriggerBuilder>.Success success)
{
    var trigger = success.Value
        .WithIdentity("unfreezeTrigger", "myGroup")
        .ForJob("unfreezeJob", "myJobGroup")
        .Build();
}
```
```

- [ ] **Step 2: Add duration parsing to `README.md`'s feature list**

Read `README.md` first to find the top-level feature/capability list (near where Unix cron, NCrontab, and Quartz support are introduced), then add one bullet documenting duration parsing as a fourth capability, cross-referencing `src/HumanCron/INTEGRATION.md` for the full API. Match the existing bullet style and tone exactly (do not restructure surrounding content).

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test HumanCron.slnx`
Expected: All tests pass, 0 failures

- [ ] **Step 4: Run a full build to confirm zero warnings**

Run: `dotnet build HumanCron.slnx`
Expected: 0 Warnings, 0 Errors

- [ ] **Step 5: Commit**

```bash
git add src/HumanCron/INTEGRATION.md README.md
git commit -m "docs: document bidirectional duration parsing and Quartz one-time triggers"
```
