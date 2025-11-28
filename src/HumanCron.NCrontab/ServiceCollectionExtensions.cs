using Microsoft.Extensions.DependencyInjection;
using HumanCron.NCrontab.Abstractions;
using HumanCron.NCrontab.Converters;

namespace HumanCron.NCrontab;

/// <summary>
/// Dependency injection registration for HumanCron.NCrontab extension
/// </summary>
/// <remarks>
/// DO NOT call AddServices() directly - it is automatically invoked by HumanCron.AddHumanCron()
/// assembly scanning when this package is installed.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Internal registration method invoked by HumanCron.AddHumanCron() assembly scanning
        /// Registers NCrontab 6-field converter services
        /// </summary>
        /// <returns>The service collection for chaining</returns>
        internal IServiceCollection AddServices()
        {
            // Register NCrontab converter using factory method
            // Using Transient lifetime - creates new instance per injection
            services.AddTransient<INCrontabConverter>(_ => NCrontabConverter.Create());

            return services;
        }
    }
}
