namespace HumanCron.Models.Internal;

/// <summary>
/// Represents a pattern of days (weekdays, weekends, etc.)
/// INTERNAL: Not exposed in public API
/// </summary>
internal enum DayPattern
{
    /// <summary>
    /// Monday through Friday
    /// </summary>
    Weekdays,

    /// <summary>
    /// Saturday and Sunday
    /// </summary>
    Weekends
}
