using HumanCron.Models.Internal;

namespace HumanCron.Abstractions;

/// <summary>
/// Formats a ScheduleSpec back to natural language representation
/// Provides bidirectional conversion: natural language → ScheduleSpec → natural language
/// INTERNAL: Used internally by converters
/// </summary>
internal interface IScheduleFormatter
{
    /// <summary>
    /// Formats a ScheduleSpec as natural language
    /// </summary>
    /// <param name="spec">The schedule specification to format</param>
    /// <returns>Natural language representation (e.g., "1d at 2pm", "2w on sunday")</returns>
    /// <example>
    /// <code>
    /// var spec = new ScheduleSpec { Interval = 1, Unit = IntervalUnit.Days, TimeOfDay = new TimeOnly(14, 0) };
    /// var formatted = formatter.Format(spec); // Returns "1d at 2pm"
    /// </code>
    /// </example>
    string Format(ScheduleSpec spec);
}
