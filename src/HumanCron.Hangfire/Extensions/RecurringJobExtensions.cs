using System;
using System.Linq.Expressions;
using Hangfire;
using HumanCron.Models;
using HumanCron.NCrontab.Abstractions;

namespace HumanCron.Hangfire.Extensions;

/// <summary>
/// Extension methods for Hangfire RecurringJob with natural language scheduling
/// </summary>
/// <remarks>
/// Provides natural language alternatives to RecurringJob.AddOrUpdate() methods
/// Example: RecurringJob.AddOrUpdate("job-id", "every 30 seconds", () => DoWork());
/// </remarks>
public static class RecurringJobExtensions
{
    /// <summary>
    /// Add or update a recurring job using natural language schedule
    /// </summary>
    /// <param name="jobId">The unique job identifier</param>
    /// <param name="naturalLanguageSchedule">Natural language schedule (e.g., "every 30 seconds", "every day at 2pm")</param>
    /// <param name="methodCall">The method to execute</param>
    /// <param name="converter">The NCrontab converter (optional, uses default if not provided)</param>
    /// <param name="options">Optional recurring job options</param>
    public static void AddOrUpdate(
        string jobId,
        string naturalLanguageSchedule,
        Expression<Action> methodCall,
        INCrontabConverter? converter = null,
        RecurringJobOptions? options = null)
    {
        var cronExpression = ConvertToCron(naturalLanguageSchedule, converter);
        options ??= new RecurringJobOptions();
        RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
    }

    /// <summary>
    /// Add or update a recurring job using natural language schedule with generic method call
    /// </summary>
    /// <typeparam name="T">The type containing the method to execute</typeparam>
    /// <param name="jobId">The unique job identifier</param>
    /// <param name="naturalLanguageSchedule">Natural language schedule (e.g., "every 30 seconds", "every day at 2pm")</param>
    /// <param name="methodCall">The method to execute</param>
    /// <param name="converter">The NCrontab converter (optional, uses default if not provided)</param>
    /// <param name="options">Optional recurring job options</param>
    public static void AddOrUpdate<T>(
        string jobId,
        string naturalLanguageSchedule,
        Expression<Action<T>> methodCall,
        INCrontabConverter? converter = null,
        RecurringJobOptions? options = null)
    {
        var cronExpression = ConvertToCron(naturalLanguageSchedule, converter);
        options ??= new RecurringJobOptions();
        RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
    }

    /// <summary>
    /// Add or update a recurring job using natural language schedule with Func method call
    /// </summary>
    /// <param name="jobId">The unique job identifier</param>
    /// <param name="naturalLanguageSchedule">Natural language schedule (e.g., "every 30 seconds", "every day at 2pm")</param>
    /// <param name="methodCall">The method to execute</param>
    /// <param name="converter">The NCrontab converter (optional, uses default if not provided)</param>
    /// <param name="options">Optional recurring job options</param>
    public static void AddOrUpdate(
        string jobId,
        string naturalLanguageSchedule,
        Expression<Func<System.Threading.Tasks.Task>> methodCall,
        INCrontabConverter? converter = null,
        RecurringJobOptions? options = null)
    {
        var cronExpression = ConvertToCron(naturalLanguageSchedule, converter);
        options ??= new RecurringJobOptions();
        RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
    }

    /// <summary>
    /// Add or update a recurring job using natural language schedule with generic Func method call
    /// </summary>
    /// <typeparam name="T">The type containing the method to execute</typeparam>
    /// <param name="jobId">The unique job identifier</param>
    /// <param name="naturalLanguageSchedule">Natural language schedule (e.g., "every 30 seconds", "every day at 2pm")</param>
    /// <param name="methodCall">The method to execute</param>
    /// <param name="converter">The NCrontab converter (optional, uses default if not provided)</param>
    /// <param name="options">Optional recurring job options</param>
    public static void AddOrUpdate<T>(
        string jobId,
        string naturalLanguageSchedule,
        Expression<Func<T, System.Threading.Tasks.Task>> methodCall,
        INCrontabConverter? converter = null,
        RecurringJobOptions? options = null)
    {
        var cronExpression = ConvertToCron(naturalLanguageSchedule, converter);
        options ??= new RecurringJobOptions();
        RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, options);
    }

    /// <summary>
    /// Convert natural language to NCrontab cron expression
    /// </summary>
    private static string ConvertToCron(string naturalLanguageSchedule, INCrontabConverter? converter)
    {
        converter ??= NCrontab.Converters.NCrontabConverter.Create();

        var result = converter.ToNCrontab(naturalLanguageSchedule);

        return result switch
        {
            ParseResult<string>.Success success => success.Value,
            ParseResult<string>.Error error => throw new ArgumentException(
                $"Failed to parse natural language schedule: {error.Message}",
                nameof(naturalLanguageSchedule)),
            _ => throw new InvalidOperationException($"Unknown result type: {result.GetType().Name}")
        };
    }
}
