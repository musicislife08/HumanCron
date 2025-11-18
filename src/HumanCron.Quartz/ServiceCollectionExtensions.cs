using Microsoft.Extensions.DependencyInjection;
using HumanCron.Quartz.Abstractions;

namespace HumanCron.Quartz;

/// <summary>
/// Dependency injection registration for HumanCron.Quartz extension
/// </summary>
/// <remarks>
/// DO NOT call AddServices() directly - it is automatically invoked by HumanCron.AddHumanCron()
/// assembly scanning when this package is installed.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Internal registration method invoked by HumanCron.AddHumanCron() assembly scanning
    /// Registers Quartz.NET converter services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    internal static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Register Quartz schedule converter using factory method
        // Using Transient lifetime - creates new instance per injection
        services.AddTransient<IQuartzScheduleConverter>(_ => QuartzScheduleConverterFactory.Create());

        return services;
    }
}
