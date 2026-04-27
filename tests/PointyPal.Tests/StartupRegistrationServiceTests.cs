using System.Collections.Generic;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class FakeStartupRegistry : IStartupRegistry
{
    private readonly Dictionary<string, string> _values = new();

    public string? GetValue(string subKey, string valueName)
    {
        string key = $"{subKey}\\{valueName}";
        return _values.TryGetValue(key, out var val) ? val : null;
    }

    public void SetValue(string subKey, string valueName, string value)
    {
        string key = $"{subKey}\\{valueName}";
        _values[key] = value;
    }

    public void DeleteValue(string subKey, string valueName)
    {
        string key = $"{subKey}\\{valueName}";
        _values.Remove(key);
    }
}

public class StartupRegistrationServiceTests
{
    [Fact]
    public void IsRegistered_WhenRegistryHasValue_ReturnsTrue()
    {
        var registry = new FakeStartupRegistry();
        registry.SetValue(StartupRegistrationService.RunKeyPath, "PointyPal", "some_path");
        var service = new StartupRegistrationService(registry, "PointyPal");

        service.IsRegistered().Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_WhenRegistryIsEmpty_ReturnsFalse()
    {
        var registry = new FakeStartupRegistry();
        var service = new StartupRegistrationService(registry, "PointyPal");

        service.IsRegistered().Should().BeFalse();
    }

    [Fact]
    public void ApplyConfig_WithStartWithWindows_SetsRegistryValue()
    {
        var registry = new FakeStartupRegistry();
        var service = new StartupRegistrationService(registry, "PointyPal");
        var config = new AppConfig { StartWithWindows = true, StartMinimizedToTray = true };

        var result = service.ApplyConfig(config);

        result.Success.Should().BeTrue();
        result.Enabled.Should().BeTrue();
        
        string val = registry.GetValue(StartupRegistrationService.RunKeyPath, "PointyPal");
        val.Should().NotBeNullOrWhiteSpace();
        val.Should().EndWith(" --minimized");
    }

    [Fact]
    public void ApplyConfig_WithoutStartWithWindows_DeletesRegistryValue()
    {
        var registry = new FakeStartupRegistry();
        registry.SetValue(StartupRegistrationService.RunKeyPath, "PointyPal", "some_path");
        
        var service = new StartupRegistrationService(registry, "PointyPal");
        var config = new AppConfig { StartWithWindows = false };

        var result = service.ApplyConfig(config);

        result.Success.Should().BeTrue();
        result.Enabled.Should().BeFalse();
        
        string val = registry.GetValue(StartupRegistrationService.RunKeyPath, "PointyPal");
        val.Should().BeNull();
    }

    [Fact]
    public void BuildStartupCommand_FormatsCorrectly()
    {
        string cmd = StartupRegistrationService.BuildStartupCommand("C:\\App.exe", true);
        cmd.Should().Be("\"C:\\App.exe\" --minimized");

        cmd = StartupRegistrationService.BuildStartupCommand("C:\\App.exe", false);
        cmd.Should().Be("\"C:\\App.exe\"");
    }
}
