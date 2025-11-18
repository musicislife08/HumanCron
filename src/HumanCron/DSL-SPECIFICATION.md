# HumanCron DSL Specification

**Version:** 1.0
**Date:** 2025-01-17
**Target:** Unix cron (5-field) + Quartz.NET (6-field cron + CalendarIntervalSchedule)

---

## Design Principles

1. **Human-readable first**: `"every day at 2pm"` not `"1d at 2pm"`
2. **Verbose over concise**: Full words for maximum clarity
3. **"every" always required**: All patterns must start with "every"
4. **Accept abbreviations on input**: `mon`/`monday`, `jan`/`january`
5. **Output uses full names**: Always format with full words for readability
6. **Order-independent**: `"every day at 2pm in january"` = `"every day in january at 2pm"`
7. **Lowercase keywords**: `monday`, `weekdays`, `at`, `on`, `in`, `between` all lowercase
8. **Full Unix cron parity**: Support all standard cron patterns (months, days, ranges, lists)

---

## Grammar Components

### 1. Interval (Required - Always starts with "every")

**Format:** `every <number> <unit>` or `every <unit>` (for singular)

**Units (full words only):**
- `second` / `seconds` - Every N seconds
- `minute` / `minutes` - Every N minutes
- `hour` / `hours` - Every N hours
- `day` / `days` - Every N days
- `week` / `weeks` - Every N weeks
- `month` / `months` - Every N months
- `year` / `years` - Every N years

**Examples:**
```
every 30 seconds       → Every 30 seconds
every second           → Every second (singular form)
every 15 minutes       → Every 15 minutes
every minute           → Every minute
every 6 hours          → Every 6 hours
every hour             → Every hour
every day              → Every day
every 2 days           → Every 2 days
every week             → Every week
every 2 weeks          → Every 2 weeks (CalendarIntervalSchedule)
every month            → Every month
every 3 months         → Every 3 months (quarterly)
every year             → Every year
```

**Constraints:**
- Must start with `"every"`
- Number must be positive integer (1+) or omitted for singular
- No compound intervals: `"every 1 hour 30 minutes"` is invalid
- Multi-week (2+), month, and year intervals use CalendarIntervalSchedule

### 2. Time Specifier (Optional)

**Format:** `at <time>`

**Time Formats:**
- 12-hour: `2pm`, `3:30am`, `12:00pm`
- 24-hour: `14:00`, `03:30`, `00:00`
- Mixed case OK: `2PM`, `2pm`, `2pM` all valid

**Examples:**
```
at 2pm
at 14:00
at 3:30am
at 00:00
at midnight
at noon
```

**Semantics:**
- For day/week intervals: Specific time to run
- For second/minute/hour intervals: Starting time for interval pattern

### 3. Day Specifier (Optional)

**Format:** `on <day>`, `every <day>`, `between <day> and <day>`, or `on <day-of-month>`

**Context-Aware Behavior:**
- With **daily/weekly** intervals: Day of week
- With **monthly** intervals: Day of month (1-31)

#### Days of Week

**Full names (accepted):**
- `monday`, `tuesday`, `wednesday`, `thursday`, `friday`, `saturday`, `sunday`

**Abbreviations (accepted on input, never output):**
- `mon`, `tue`, `wed`, `thu`, `fri`, `sat`, `sun`

**Patterns:**
- `weekday` / `weekdays` - Monday through Friday
- `weekend` / `weekends` - Saturday and Sunday

**Day Ranges:**
- `between monday and friday` - Mon-Fri
- `between mon and fri` - Same (abbreviations accepted)
- `between tuesday and thursday` - Tue-Thu

**Examples:**
```
every monday                    → Every Monday (full name)
every mon                       → Every Monday (abbreviation accepted)
every weekday                   → Mon-Fri
between monday and friday       → Mon-Fri (range syntax)
between mon and fri             → Mon-Fri (abbreviations)
```

#### Day of Month (for monthly intervals only)

**Format:** `on <number>` where number is 1-31

**Examples:**
```
on 15              → 15th of month (for monthly only)
on 1               → 1st of month (for monthly only)
on 31              → 31st (skips months with fewer days)
```

### 4. Month Specifier (Optional - NEW in v2.0)

**Format:** `in <month>`, `between <month> and <month>`, or `in <month-list>`

#### Specific Month

**Full names (accepted):**
- `january`, `february`, `march`, `april`, `may`, `june`
- `july`, `august`, `september`, `october`, `november`, `december`

**Abbreviations (accepted on input, never output):**
- `jan`, `feb`, `mar`, `apr`, `may`, `jun`
- `jul`, `aug`, `sep`, `oct`, `nov`, `dec`

**Examples:**
```
in january                      → January only (full name)
in jan                          → January only (abbreviation)
in december                     → December only
```

#### Month Ranges

**Format:** `between <month> and <month>`

**Examples:**
```
between january and march       → Jan-Mar (Q1)
between jan and mar             → Jan-Mar (abbreviations)
between june and august         → Jun-Aug (summer)
```

#### Month Lists

**Format:** `in <month>,<month>,<month>,...`

**Examples:**
```
in january,april,july,october   → Fiscal quarters (full names)
in jan,apr,jul,oct              → Fiscal quarters (abbreviations)
in june,july,august             → Summer months
```

---

## Default Behavior

### No Time Specified
- Uses **midnight (00:00) in specified timezone**
- If no timezone in `ScheduleParserOptions`: **UTC midnight (default)**

**Examples:**
```
"every day"                      → Daily at 00:00 (user TZ or UTC)
"every week"                     → Weekly on current day at 00:00
"every monday"                   → Every Monday at 00:00
```

### No Day Specified
- **Daily patterns**: Runs every day
- **Weekly patterns (`every week`)**: Uses **current day of week** (when schedule created)
- **Multi-week patterns**: Uses **current day of week**
- **Monthly patterns**: Uses **day 1** (first of month)
- **Yearly patterns**: Uses **January 1st**
- **Sub-day intervals**: Runs continuously (no day restriction)

**Examples:**
```
"every week"                     → Every <current-day> at midnight
"every week at 2pm"              → Every <current-day> at 2pm
"every 2 weeks"                  → Every 2 weeks on <current-day>
"every month"                    → Every month on the 1st
"every year"                     → Every year on January 1st
```

**With explicit day:**
```
"every tuesday"                  → Every Tuesday (overrides current day)
"every 2 weeks on sunday at 1pm" → Every 2 weeks on Sunday at 1pm
"on 15 every month"              → Every month on the 15th
"on 15 every month at 2pm"       → Every month on the 15th at 2pm
```

### No Month Specified
- Runs **every month** (wildcard in cron month field)

### No Timezone in Options
- Default to **UTC**
- All times interpreted as UTC
- Cron/schedule generated in UTC

---

## Supported Pattern Combinations

### Interval Only
```
"every 30 seconds"               → Every 30 seconds
"every 15 minutes"               → Every 15 minutes
"every 6 hours"                  → Every 6 hours
"every day"                      → Daily at midnight (user TZ)
"every week"                     → Weekly on current day at midnight
"every 2 weeks"                  → Every 2 weeks (CalendarInterval)
"every month"                    → Monthly on the 1st
"every 3 months"                 → Quarterly on the 1st
"every year"                     → Annually on January 1st
```

### Interval + Time
```
"every day at 2pm"               → Daily at 2pm
"every week at 3:30am"           → Weekly on current day at 3:30am
"every 2 weeks at 9am"           → Every 2 weeks at 9am
"every month at 2pm"             → Monthly on the 1st at 2pm
"every 6 hours at 2pm"           → Every 6 hours starting at 2pm
"every 30 minutes at 9am"        → Every 30 min starting at 9am
```

### Interval + Day
```
"every hour on monday"           → Every hour on Mondays only
"every day on weekdays"          → Mon-Fri at midnight
"every monday"                   → Every Monday at midnight
"every tuesday"                  → Every Tuesday at midnight
"every 2 weeks on sunday"        → Every 2 weeks on Sunday
"on 15 every month"              → Every month on the 15th
"between monday and friday"      → Mon-Fri at midnight
```

### Interval + Month (NEW in v2.0)
```
"every day in january"           → Daily in January only
"every day in jan"               → Daily in January (abbreviation)
"every day between june and august" → Daily in summer (Jun-Aug)
"every day in jan,apr,jul,oct"   → Daily in fiscal quarters
"every monday in december"       → Mondays in December only
"every weekday in january"       → Weekdays in January
```

### Interval + Day + Time (Order Independent)
```
"every monday at 2pm"            → Every Monday at 2pm
"every day at 2pm on weekdays"   → Mon-Fri at 2pm (any order)
"at 9am every weekday"           → Mon-Fri at 9am
"every 2 weeks on sunday at 1pm" → Every 2 weeks on Sunday at 1pm
"on 15 every month at 2pm"       → 15th of every month at 2pm
"between monday and friday at 9am" → Mon-Fri at 9am
```

### Interval + Day + Month + Time (FULL COMBINATION - NEW)
```
"every day in january at 9am"    → Daily in Jan at 9am
"every monday in december at 2pm" → Mondays in Dec at 2pm
"every weekday in january at 9am" → Weekdays in Jan at 9am
"every day between jan and mar at 2pm" → Daily in Q1 at 2pm
"between monday and friday in summer at 9am" → Weekdays in Jun-Aug at 9am
"on 15 in january,april,july,october at 9am" → 15th of fiscal quarters at 9am
```

---

## Cron Expression Mapping

### Unix Cron Format (5 fields)
```
minute  hour  day-of-month  month  day-of-week
```

### Quartz Cron Format (6 fields)
```
second  minute  hour  day-of-month  month  day-of-week  [year]
```

### Examples with Month Support

| Natural Language | Unix Cron | Quartz Cron | Notes |
|-----------------|-----------|-------------|-------|
| `"every day"` | `0 0 * * *` | `0 0 0 * * ?` | Every day at midnight |
| `"every day at 2pm"` | `0 14 * * *` | `0 0 14 * * ?` | Every day at 2pm |
| `"every monday"` | `0 0 * * 1` | `0 0 0 ? * MON` | Every Monday |
| `"every day in january"` | `0 0 * 1 *` | `0 0 0 * 1 ?` | **NEW** - Daily in Jan |
| `"every day in jan"` | `0 0 * 1 *` | `0 0 0 * 1 ?` | Same (abbrev accepted) |
| `"every day between jan and mar"` | `0 0 * 1-3 *` | `0 0 0 * 1-3 ?` | **NEW** - Daily Q1 |
| `"in jan,apr,jul,oct every day"` | `0 0 * 1,4,7,10 *` | `0 0 0 * 1,4,7,10 ?` | **NEW** - Fiscal quarters |
| `"every monday in december"` | `0 0 * 12 1` | `0 0 0 ? 12 MON` | **NEW** - Mondays in Dec |
| `"every monday in december at 9am"` | `0 9 * 12 1` | `0 0 9 ? 12 MON` | **NEW** - Combined |
| `"between monday and friday"` | `0 0 * * 1-5` | `0 0 0 ? * MON-FRI` | **NEW** - Day range |
| `"on 15 in march"` | `0 0 15 3 *` | `0 0 0 15 3 ?` | **NEW** - March 15th |
| `"on 15 in jan,apr,jul,oct at 9am"` | `0 9 15 1,4,7,10 *` | `0 0 9 15 1,4,7,10 ?` | **NEW** - Quarters |

---

## Quartz Scheduling Strategy

### CronSchedule (6-part cron)

**Used for:**
- All second/minute/hour/day intervals
- Single-week intervals (`every week`)
- Patterns with specific months or days

**Examples:**
```
"every 30 seconds"                → 0 */30 * * * ?
"every 15 minutes"                → 0 */15 * * * ?
"every 6 hours"                   → 0 0 */6 * * ?
"every day at 2pm"                → 0 0 14 * * ?
"every tuesday at 2pm"            → 0 0 14 ? * TUE
"every day in january"            → 0 0 0 * 1 ?
"every monday in december"        → 0 0 0 ? 12 MON
```

### CalendarIntervalSchedule

**Used for:**
- Multi-week intervals (`every 2 weeks`, `every 3 weeks`, etc.)
- Month intervals (`every month`, `every 3 months`, etc.)
- Year intervals (`every year`, `every 2 years`, etc.)

**Why:** Cron limitations:
- Cannot express "every N weeks" where N > 1
- Cannot express "every N months" reliably
- Cannot express "every N years"

**Examples:**
```
"every 2 weeks"                  → CalendarInterval: 2 weeks
"every 3 weeks on sunday at 1pm" → CalendarInterval: 3 weeks, Sunday, 1pm
"every month"                    → CalendarInterval: 1 month, day 1
"on 15 every month at 2pm"       → CalendarInterval: 1 month, day 15, 2pm
"every 3 months on 1 at 9am"     → CalendarInterval: 3 months, day 1, 9am
"every year"                     → CalendarInterval: 1 year, Jan 1
```

---

## Output Formatting (Cron → Natural Language)

### Always Use Full Names

When converting cron back to natural language, **always use full names**, never abbreviations:

```
*/30 * * * *        → "every 30 minutes"  (not "every 30 min")
0 * * * *           → "every hour"
0 0 * * *           → "every day"
0 14 * * *          → "every day at 2pm"
0 9 * * 1           → "every monday at 9am"  (not "every mon")
0 9 * 1 *           → "every day in january at 9am"  (not "in jan")
0 9 * 1-3 *         → "every day between january and march at 9am"
0 9 15 1,4,7,10 *   → "on 15 in january,april,july,october at 9am"
0 9 * 12 1          → "every monday in december at 9am"
0 9 * * 1-5         → "between monday and friday at 9am"
```

### Month Number to Name Mapping

```
1  → january
2  → february
3  → march
4  → april
5  → may
6  → june
7  → july
8  → august
9  → september
10 → october
11 → november
12 → december
```

### Day Number to Name Mapping

```
0 → sunday
1 → monday
2 → tuesday
3 → wednesday
4 → thursday
5 → friday
6 → saturday
```

---

## Validation Rules

### Valid Patterns

✅ All combinations are allowed as long as they map to valid cron/Quartz schedules

### Required Rules

✅ **Must start with "every":**
```
"every day"          ✅
"day"                ❌ Missing "every"
```

✅ **Interval is always required:**
```
"every day"          ✅
"on monday"          ❌ Missing interval
"at 2pm"             ❌ Missing interval
```

### Error Cases

❌ **Empty or invalid input:**
```
""                   → Error: Input cannot be empty
"foobar"             → Error: Unable to parse
```

❌ **Missing "every":**
```
"day at 2pm"         → Error: Must start with "every"
"monday"             → Error: Must start with "every"
"30 minutes"         → Error: Must start with "every"
```

❌ **Invalid units:**
```
"every 1x"           → Error: Invalid unit 'x'
"every 30"           → Error: Missing unit
```

❌ **Invalid times:**
```
"every day at 25pm"  → Error: Invalid hour for 12-hour format
"every day at 99:00" → Error: Invalid hour for 24-hour format
"every day at 2:60am" → Error: Invalid minutes
```

❌ **Invalid days:**
```
"every funday"       → Error: Invalid day 'funday'
```

❌ **Invalid months:**
```
"every day in january" ✅ Valid (full name)
"every day in jan"     ✅ Valid (abbreviation)
"every day in janu"    ❌ Invalid (not recognized)
```

❌ **Context conflicts:**
```
"on 15 every day"    → Error: Day-of-month only valid with monthly intervals
"on 15 every week"   → Error: Day-of-month only valid with monthly intervals
```

❌ **Month conflicts:**
```
"every month in january" → Error: Month interval conflicts with month selection
"every 3 months in jan"  → Error: Month interval conflicts with month selection
```

---

## Parsing Strategy

### Order-Independent Parsing

Since components can appear in any order:

1. **Extract all components** using separate regex patterns
2. **Validate** required parts are present
3. **Validate** no conflicts (e.g., month interval + month selection)
4. **Assemble** `ScheduleSpec` from extracted parts

### Regex Patterns (Source Generated - .NET 10 Optimized)

```csharp
// Interval: every 30 seconds, every 15 minutes, every day
[GeneratedRegex(@"every\s+(\d+)?\s*(second|seconds|minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)", RegexOptions.IgnoreCase)]
private static partial Regex IntervalPattern();

// Time: at 2pm, at 14:00, at 3:30am
[GeneratedRegex(@"at\s+(\d{1,2}):?(\d{2})?\s*(am|pm)?", RegexOptions.IgnoreCase)]
private static partial Regex TimePattern();

// Specific day: every monday, every mon, every weekday
[GeneratedRegex(@"every\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun|weekday|weekdays|weekend|weekends)", RegexOptions.IgnoreCase)]
private static partial Regex SpecificDayPattern();

// Day range: between monday and friday, between mon and fri
[GeneratedRegex(@"between\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)\s+and\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)", RegexOptions.IgnoreCase)]
private static partial Regex DayRangePattern();

// Day of month: on 15, on 1, on 31 (for monthly intervals)
[GeneratedRegex(@"on\s+(\d{1,2})", RegexOptions.IgnoreCase)]
private static partial Regex DayOfMonthPattern();

// Specific month: in january, in jan
[GeneratedRegex(@"in\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)", RegexOptions.IgnoreCase)]
private static partial Regex SpecificMonthPattern();

// Month range: between january and march, between jan and mar
[GeneratedRegex(@"between\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)\s+and\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)", RegexOptions.IgnoreCase)]
private static partial Regex MonthRangePattern();

// Month list: in jan,apr,jul,oct or in january,april,july,october
[GeneratedRegex(@"in\s+((?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(?:,\s*(?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec))+)", RegexOptions.IgnoreCase)]
private static partial Regex MonthListPattern();
```

---

## Examples with Full Breakdown

### Example 1: `"every day at 2pm"`
```
Input:    "every day at 2pm"
Interval: every day → Interval=1, Unit=Days
Time:     at 2pm → TimeOfDay=14:00
Day:      (none) → DayOfWeek=null
Month:    (none) → Month=null
Unix:     0 14 * * *
Quartz:   0 0 14 * * ?
Meaning:  Daily at 2pm
```

### Example 2: `"every day in january at 9am"`
```
Input:    "every day in january at 9am"
Interval: every day → Interval=1, Unit=Days
Time:     at 9am → TimeOfDay=09:00
Day:      (none) → DayOfWeek=null
Month:    in january → Month=1
Unix:     0 9 * 1 *
Quartz:   0 0 9 * 1 ?
Meaning:  Daily in January at 9am
```

### Example 3: `"every monday in december at 2pm"`
```
Input:    "every monday in december at 2pm"
Interval: every monday → Interval=1, Unit=Days (daily with day filter)
Time:     at 2pm → TimeOfDay=14:00
Day:      monday → DayOfWeek=Monday
Month:    in december → Month=12
Unix:     0 14 * 12 1
Quartz:   0 0 14 ? 12 MON
Meaning:  Every Monday in December at 2pm
```

### Example 4: `"between monday and friday in summer at 9am"`
```
Input:    "between monday and friday in summer at 9am"
Interval: every day (implied)
Time:     at 9am → TimeOfDay=09:00
Day:      between monday and friday → DayRange=(1,5)
Month:    in summer → MonthRange=(6,8)  [if "summer" alias defined]
Unix:     0 9 * 6-8 1-5
Quartz:   0 0 9 ? 6-8 MON-FRI
Meaning:  Weekdays in Jun-Aug at 9am
```

### Example 5: `"on 15 in jan,apr,jul,oct at 9am"`
```
Input:    "on 15 in jan,apr,jul,oct at 9am"
Interval: every month (implied from day-of-month)
Time:     at 9am → TimeOfDay=09:00
Day:      on 15 → DayOfMonth=15
Month:    in jan,apr,jul,oct → MonthList=[1,4,7,10]
Unix:     0 9 15 1,4,7,10 *
Quartz:   0 0 9 15 1,4,7,10 ?
Meaning:  15th of fiscal quarters at 9am
```

---

## Future Considerations

### Not Currently Supported

**Last day of month:**
- `"last day of month"` - Quartz supports `L` but complex to parse
- Reason: Edge case, add if needed

**Nth weekday of month:**
- `"1st monday of month"` - Quartz supports `MON#1`
- Reason: Complex parsing, limited use case

**Business day logic:**
- `"every business day"` (excluding holidays)
- Reason: Requires holiday calendar, out of scope

**Time ranges:**
- `"9am-5pm"` (hourly during business hours)
- Reason: Better handled by multiple schedules

---

**End of Specification**
