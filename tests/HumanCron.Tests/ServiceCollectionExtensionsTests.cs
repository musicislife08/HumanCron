using HumanCron;
using HumanCron.Abstractions;
using HumanCron.NCrontab.Abstractions;
using HumanCron.Quartz.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HumanCron.Tests;

/// <summary>
/// Tests for dependency injection registration and auto-discovery of extension packages
/// </summary>
/// <remarks>
/// These tests verify the assembly scanning and extension discovery mechanism,
/// not the converters themselves (those are tested elsewhere).
/// </remarks>
[TestFixture]
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies that AddHumanCron() registers the base Unix cron converter
    /// </summary>
    [Test]
    public void AddHumanCron_RegistersUnixConverter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHumanCron();
        var provider = services.BuildServiceProvider();

        // Assert - Base library should always register IHumanCronConverter
        var converter = provider.GetService<IHumanCronConverter>();
        Assert.That(converter, Is.Not.Null, "IHumanCronConverter should be registered by base library");
    }

    /// <summary>
    /// Verifies that AddHumanCron() auto-discovers and registers Quartz.NET extension
    /// </summary>
    [Test]
    public void AddHumanCron_AutoDiscoversQuartzExtension()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHumanCron();
        var provider = services.BuildServiceProvider();

        // Assert - Quartz extension should be auto-discovered via assembly scanning
        var converter = provider.GetService<IQuartzScheduleConverter>();
        Assert.That(converter, Is.Not.Null,
            "IQuartzScheduleConverter should be registered via auto-discovery of HumanCron.Quartz");
    }

    /// <summary>
    /// Verifies that AddHumanCron() auto-discovers and registers NCrontab extension
    /// </summary>
    [Test]
    public void AddHumanCron_AutoDiscoversNCrontabExtension()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHumanCron();
        var provider = services.BuildServiceProvider();

        // Assert - NCrontab extension should be auto-discovered via assembly scanning
        var converter = provider.GetService<INCrontabConverter>();
        Assert.That(converter, Is.Not.Null,
            "INCrontabConverter should be registered via auto-discovery of HumanCron.NCrontab");
    }

    /// <summary>
    /// Verifies that multiple calls to AddHumanCron() don't cause errors
    /// (Testing idempotency of registration)
    /// </summary>
    [Test]
    public void AddHumanCron_CanBeCalledMultipleTimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Call multiple times
        services.AddHumanCron();
        services.AddHumanCron();
        services.AddHumanCron();

        // Assert - Should not throw when building provider
        Assert.DoesNotThrow(() =>
        {
            var provider = services.BuildServiceProvider();
            var converter = provider.GetService<IHumanCronConverter>();
            Assert.That(converter, Is.Not.Null);
        });
    }

    /// <summary>
    /// Verifies that registered converters are actually usable (not just registered)
    /// This is a basic smoke test to ensure the DI wiring works end-to-end
    /// </summary>
    [Test]
    public void AddHumanCron_RegisteredConvertersAreUsable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHumanCron();
        var provider = services.BuildServiceProvider();

        // Act - Get converters and attempt basic operations
        var unixConverter = provider.GetRequiredService<IHumanCronConverter>();
        var quartzConverter = provider.GetRequiredService<IQuartzScheduleConverter>();
        var ncrontabConverter = provider.GetRequiredService<INCrontabConverter>();

        // Assert - Basic smoke test (detailed behavior tested elsewhere)
        Assert.DoesNotThrow(() =>
        {
            var unixResult = unixConverter.ToCron("every day at 2pm");
            Assert.That(unixResult, Is.Not.Null);

            var quartzResult = quartzConverter.ToQuartzSchedule("every day at 2pm");
            Assert.That(quartzResult, Is.Not.Null);

            var ncrontabResult = ncrontabConverter.ToNCrontab("every day at 2pm");
            Assert.That(ncrontabResult, Is.Not.Null);
        }, "Registered converters should be usable");
    }
}
