using HumanCron.Builders;
using HumanCron.Models.Internal;
using Quartz;
using System;
using System.Runtime.CompilerServices;
using NodaTime;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Quartz.Extensions;

/// <summary>
/// Extension methods for ScheduleBuilder to support Quartz.NET schedule generation
/// </summary>
public static class ScheduleBuilderExtensions
{
    /// <param name="builder">The ScheduleBuilder instance</param>
    extension(ScheduleBuilder builder)
    {
        /// <summary>
        /// Build a Quartz.NET IScheduleBuilder from the fluent schedule configuration
        /// </summary>
        /// <returns>Quartz IScheduleBuilder (CronScheduleBuilder or CalendarIntervalScheduleBuilder)</returns>
        /// <example>
        /// <code>
        /// var quartzSchedule = new ScheduleBuilder()
        ///     .Every(1).Day()
        ///     .At(14, 0)
        ///     .ToQuartzSchedule();
        /// 
        /// var trigger = TriggerBuilder.Create()
        ///     .WithSchedule(quartzSchedule)
        ///     .Build();
        /// </code>
        /// </example>
        /// <exception cref="NotSupportedException">Thrown when the schedule pattern cannot be expressed in Quartz.NET</exception>
        public IScheduleBuilder ToQuartzSchedule()
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Get the ScheduleSpec from the builder via reflection (internal type access)
            var spec = GetScheduleSpec(builder);

            // Use QuartzScheduleBuilder to convert to Quartz schedule
            var quartzBuilder = QuartzScheduleBuilder.Create();
            return quartzBuilder.Build(spec);
        }

        /// <summary>
        /// Calculate the appropriate start time for schedules with day-of-week or day-of-month constraints
        /// </summary>
        /// <param name="referenceTime">Optional reference time (defaults to UTC now)</param>
        /// <returns>The calculated start time, or null if no special start time is needed</returns>
        /// <example>
        /// <code>
        /// var schedule = new ScheduleBuilder()
        ///     .Every(2).Weeks()
        ///     .On(DayOfWeek.Sunday)
        ///     .At(14, 0);
        /// 
        /// var quartzSchedule = schedule.ToQuartzSchedule();
        /// var startTime = schedule.CalculateQuartzStartTime();
        /// 
        /// var trigger = TriggerBuilder.Create()
        ///     .WithSchedule(quartzSchedule)
        ///     .StartAt(startTime ?? DateTimeOffset.UtcNow)
        ///     .Build();
        /// </code>
        /// </example>
        public DateTimeOffset? CalculateQuartzStartTime(DateTimeOffset? referenceTime = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var spec = GetScheduleSpec(builder);
            var quartzBuilder = QuartzScheduleBuilder.Create();
            return quartzBuilder.CalculateStartTime(spec, referenceTime);
        }
    }

    // ========================================
    // UnsafeAccessor methods for accessing ScheduleBuilder private fields
    // These provide compile-time checked, zero-overhead access to private members
    // ========================================

    /// <summary>Accesses the private _interval field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_interval")]
    private static extern ref int GetInterval(ScheduleBuilder builder);

    /// <summary>Accesses the private _unit field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_unit")]
    private static extern ref NaturalIntervalUnit GetUnit(ScheduleBuilder builder);

    /// <summary>Accesses the private _dayOfWeek field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dayOfWeek")]
    private static extern ref DayOfWeek? GetDayOfWeek(ScheduleBuilder builder);

    /// <summary>Accesses the private _dayPattern field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dayPattern")]
    private static extern ref DayPattern? GetDayPattern(ScheduleBuilder builder);

    /// <summary>Accesses the private _dayOfMonth field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dayOfMonth")]
    private static extern ref int? GetDayOfMonth(ScheduleBuilder builder);

    /// <summary>Accesses the private _timeOfDay field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_timeOfDay")]
    private static extern ref TimeOnly? GetTimeOfDay(ScheduleBuilder builder);

    /// <summary>Accesses the private _timeZone field from ScheduleBuilder</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_timeZone")]
    private static extern ref DateTimeZone GetTimeZone(ScheduleBuilder builder);

    /// <summary>
    /// Extract ScheduleSpec from ScheduleBuilder using UnsafeAccessor
    /// This is necessary because ScheduleBuilder.Build() returns a string, but we need the internal ScheduleSpec
    /// UnsafeAccessor provides compile-time checked, zero-overhead access without reflection
    /// </summary>
    private static ScheduleSpec GetScheduleSpec(ScheduleBuilder builder)
    {
        return new ScheduleSpec
        {
            Interval = GetInterval(builder),
            Unit = GetUnit(builder),
            DayOfWeek = GetDayOfWeek(builder),
            DayPattern = GetDayPattern(builder),
            DayOfMonth = GetDayOfMonth(builder),
            TimeOfDay = GetTimeOfDay(builder),
            TimeZone = GetTimeZone(builder)
        };
    }
}
