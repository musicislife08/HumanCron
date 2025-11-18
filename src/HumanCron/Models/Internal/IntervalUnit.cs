namespace HumanCron.Models.Internal;

/// <summary>
/// Represents the unit of time for a schedule interval
/// INTERNAL: Not exposed in public API
/// </summary>
internal enum IntervalUnit
{
    Seconds,   // s
    Minutes,   // m (lowercase)
    Hours,     // h
    Days,      // d
    Weeks,     // w
    Months,    // M (UPPERCASE to distinguish from minutes)
    Years      // y
}
