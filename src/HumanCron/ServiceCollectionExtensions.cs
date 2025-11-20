using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HumanCron.Abstractions;
using HumanCron.Converters.Unix;
using HumanCron.Formatting;
using HumanCron.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace HumanCron;

/// <summary>
/// Extension methods for registering HumanCron services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds HumanCron services to the dependency injection container.
    /// Automatically discovers and registers extension packages (Quartz.NET, Hangfire, etc.)
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method automatically detects installed HumanCron extension packages:
    /// - Base: Registers Unix 5-part cron converter (industry standard)
    /// - HumanCron.Quartz: Auto-registers Quartz.NET converter (if package installed)
    ///
    /// No additional configuration needed - just install the package and call AddHumanCron()
    /// </remarks>
    /// <example>
    /// <code>
    /// // Works with base package only OR with Quartz extension installed
    /// using HumanCron;
    ///
    /// services.AddHumanCron();
    ///
    /// // Then inject and use:
    /// public class MyService
    /// {
    ///     private readonly IHumanCronConverter _converter;
    ///
    ///     public MyService(IHumanCronConverter converter)
    ///     {
    ///         _converter = converter;
    ///     }
    ///
    ///     public void ConvertSchedule()
    ///     {
    ///         // Natural language → Unix cron
    ///         var result = _converter.ToCron("1d at 2pm");
    ///         if (result is ParseResult&lt;string&gt;.Success success)
    ///         {
    ///             Console.WriteLine(success.Value); // "0 14 * * *"
    ///         }
    ///
    ///         // Unix cron → Natural language
    ///         var reverse = _converter.ToNaturalLanguage("0 14 * * *");
    ///         if (reverse is ParseResult&lt;string&gt;.Success reverseSuccess)
    ///         {
    ///             Console.WriteLine(reverseSuccess.Value); // "1d at 2pm"
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddHumanCron(this IServiceCollection services)
    {
        // Register NodaTime dependencies if not already registered
        // TryAddSingleton allows users to override with custom implementations
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.TryAddSingleton<DateTimeZone>(_ =>
            DateTimeZoneProviders.Tzdb.GetSystemDefault());

        // Register core services (internal dependencies)
        services.AddTransient<IScheduleParser, NaturalLanguageParser>();
        services.AddTransient<IScheduleFormatter, NaturalLanguageFormatter>();

        // Register Unix cron converter using factory method (handles IClock and DateTimeZone dependencies)
        services.AddTransient<IHumanCronConverter>(_ => UnixCronConverter.Create());

        // Auto-discover and register extension services (Quartz, Hangfire, etc.)
        RegisterExtensionServices(services);

        return services;
    }

    /// <summary>
    /// Scans loaded assemblies for HumanCron extension packages and invokes their AddServices methods
    /// Each extension package can control its own service lifetimes and registration logic
    /// </summary>
    private static void RegisterExtensionServices(IServiceCollection services)
    {
        // Force-load HumanCron.* extension assemblies from bin directory
        // (Assemblies are loaded lazily by .NET, so they may not be in AppDomain yet)
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var extensionDlls = Directory.GetFiles(baseDirectory, "HumanCron.*.dll", SearchOption.TopDirectoryOnly);

        // Cache loaded assemblies to avoid O(n×m) complexity
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.FullName)
            .ToHashSet();

        foreach (var dllPath in extensionDlls)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(dllPath);

                // Skip if already loaded
                if (loadedAssemblies.Contains(assemblyName.FullName))
                    continue;

                // Load assembly by name (let .NET resolve dependencies)
                Assembly.Load(assemblyName);
            }
            catch (Exception ex)
            {
                // Ignore load failures (might be incompatible assemblies)
                // Log for debugging purposes in development
                Debug.WriteLine($"HumanCron: Failed to load assembly '{dllPath}': {ex.Message}");
            }
        }

        // Now scan loaded assemblies for extension services
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var filteredAssemblies = assemblies
            .Where(assembly => assembly.GetName().Name?.StartsWith("HumanCron.") is true
                            && assembly.GetName().Name != "HumanCron");

        foreach (var assembly in filteredAssemblies)
        {
            // Look for static classes (IsClass + IsAbstract + IsSealed)
            var staticTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true });

            foreach (var type in staticTypes)
            {
                // Look for method signature: internal static IServiceCollection AddServices(IServiceCollection)
                var method = type.GetMethod("AddServices",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    [typeof(IServiceCollection)],
                    null);

                if (method != null && method.ReturnType == typeof(IServiceCollection))
                {
                    // Invoke the extension's registration method
                    method.Invoke(null, [services]);
                }
            }
        }
    }
}
