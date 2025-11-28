using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Hangfire;
using HumanCron.Builders;
using HumanCron.Models;
using HumanCron.Models.Internal;
using NodaTime;
using NaturalIntervalUnit = HumanCron.Models.Internal.IntervalUnit;

namespace HumanCron.Hangfire.Extensions;

/// <summary>
/// Extension methods for ScheduleBuilder to support Hangfire recurring job scheduling
/// </summary>
public static class ScheduleBuilderExtensions
{
    // Cache system default timezone to avoid repeated lookups
    private static readonly DateTimeZone SystemDefaultTimezone =
        DateTimeZoneProviders.Tzdb.GetSystemDefault();

    /// <param name="builder">The ScheduleBuilder instance</param>
    extension(ScheduleBuilder builder)
    {
        /// <summary>
        /// Add or update a Hangfire recurring job using the fluent schedule configuration
        /// </summary>
        /// <param name="jobId">The unique job identifier</param>
        /// <param name="methodCall">The method to execute</param>
        /// <param name="options">Optional recurring job options</param>
        /// <example>
        /// <code>
        /// new ScheduleBuilder()
        ///     .Every(30).Seconds()
        ///     .AddOrUpdateHangfireJob("my-job-id", () => DoWork());
        ///
        /// new ScheduleBuilder()
        ///     .Every(1).Day()
        ///     .At(14, 0)
        ///     .AddOrUpdateHangfireJob("daily-job", () => DailyTask());
        /// </code>
        /// </example>
        public void AddOrUpdateHangfireJob(
            string jobId,
            Expression<Action> methodCall,
            RecurringJobOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(jobId);
            ArgumentNullException.ThrowIfNull(methodCall);

            var cronExpression = builder.ToNCrontabExpression();
            options ??= new RecurringJobOptions();
            RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
        }

        /// <summary>
        /// Add or update a Hangfire recurring job using the fluent schedule configuration (generic method)
        /// </summary>
        /// <typeparam name="T">The type containing the method to execute</typeparam>
        /// <param name="jobId">The unique job identifier</param>
        /// <param name="methodCall">The method to execute</param>
        /// <param name="options">Optional recurring job options</param>
        public void AddOrUpdateHangfireJob<T>(
            string jobId,
            Expression<Action<T>> methodCall,
            RecurringJobOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(jobId);
            ArgumentNullException.ThrowIfNull(methodCall);

            var cronExpression = builder.ToNCrontabExpression();
            options ??= new RecurringJobOptions();
            RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
        }

        /// <summary>
        /// Add or update a Hangfire recurring job using the fluent schedule configuration (async method)
        /// </summary>
        /// <param name="jobId">The unique job identifier</param>
        /// <param name="methodCall">The async method to execute</param>
        /// <param name="options">Optional recurring job options</param>
        public void AddOrUpdateHangfireJob(
            string jobId,
            Expression<Func<Task>> methodCall,
            RecurringJobOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(jobId);
            ArgumentNullException.ThrowIfNull(methodCall);

            var cronExpression = builder.ToNCrontabExpression();
            options ??= new RecurringJobOptions();
            RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
        }

        /// <summary>
        /// Add or update a Hangfire recurring job using the fluent schedule configuration (generic async method)
        /// </summary>
        /// <typeparam name="T">The type containing the method to execute</typeparam>
        /// <param name="jobId">The unique job identifier</param>
        /// <param name="methodCall">The async method to execute</param>
        /// <param name="options">Optional recurring job options</param>
        public void AddOrUpdateHangfireJob<T>(
            string jobId,
            Expression<Func<T, Task>> methodCall,
            RecurringJobOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(jobId);
            ArgumentNullException.ThrowIfNull(methodCall);

            var cronExpression = builder.ToNCrontabExpression();
            options ??= new RecurringJobOptions();
            RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
        }

        /// <summary>
        /// Convert the fluent schedule to an NCrontab cron expression
        /// </summary>
        /// <returns>NCrontab 6-field cron expression</returns>
        /// <example>
        /// <code>
        /// var cronExpression = new ScheduleBuilder()
        ///     .Every(30).Seconds()
        ///     .ToNCrontabExpression();
        /// // Returns: "*/30 * * * * *"
        /// </code>
        /// </example>
        public string ToNCrontabExpression()
        {
            var spec = GetScheduleSpec(builder);
            var ncrontabBuilder = new NCrontab.Converters.NCrontabBuilder(
                NodaTime.SystemClock.Instance,
                SystemDefaultTimezone  // Use cached timezone
            );
            var result = ncrontabBuilder.Build(spec);

            return result switch
            {
                ParseResult<string>.Success success => success.Value,
                ParseResult<string>.Error error => throw new InvalidOperationException(
                    $"Failed to convert schedule to NCrontab: {error.Message}"),
                _ => throw new InvalidOperationException($"Unknown result type: {result.GetType().Name}")
            };
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
