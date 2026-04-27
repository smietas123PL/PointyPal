using System.IO;
using System.Text.Json;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class AppConfigTests : IDisposable
{
    private readonly string _tempPath;

    public AppConfigTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public void LoadConfig_MissingFile_ReturnsDefaults()
    {
        var service = new ConfigService(_tempPath);
        service.Config.AiProvider.Should().Be("Claude");
        service.IsFirstRun.Should().BeTrue();
    }

    [Fact]
    public void LoadConfig_ValidFile_ReturnsSavedValues()
    {
        var config = new AppConfig { AiProvider = "Claude", VoiceEnabled = false };
        string json = JsonSerializer.Serialize(config);
        File.WriteAllText(_tempPath, json);

        var service = new ConfigService(_tempPath);
        service.Config.AiProvider.Should().Be("Claude");
        service.Config.VoiceEnabled.Should().BeFalse();
        service.IsFirstRun.Should().BeFalse();
    }

    [Fact]
    public void LoadConfig_PartialFile_MaintainsDefaultsForMissingFields()
    {
        // JSON with only one field
        string json = "{\"AiProvider\": \"Claude\"}";
        File.WriteAllText(_tempPath, json);

        var service = new ConfigService(_tempPath);
        service.Config.AiProvider.Should().Be("Claude");
        service.Config.VoiceEnabled.Should().BeTrue(); // Default value
    }

    [Fact]
    public void LoadConfig_CorruptFile_ReturnsDefaults()
    {
        File.WriteAllText(_tempPath, "corrupt { json");

        var service = new ConfigService(_tempPath);
        service.Config.AiProvider.Should().Be("Claude");
    }

    [Fact]
    public void DefaultConfig_ContainsBuild016PerformanceDefaults()
    {
        var config = new AppConfig();

        config.EnableTimelineLogging.Should().BeTrue();
        config.SaveTimelineHistory.Should().BeTrue();
        config.MaxTimelineHistoryItems.Should().Be(200);
        config.UiAutomationTimeoutMs.Should().Be(1000);
        config.ClaudeRequestTimeoutSeconds.Should().Be(60);
        config.TranscriptRequestTimeoutSeconds.Should().Be(60);
        config.TtsRequestTimeoutSeconds.Should().Be(60);
        config.ScreenshotMaxWidth.Should().Be(1280);
        config.ScreenshotJpegQuality.Should().Be(70);
        config.EnableParallelTtsAndPointerFlight.Should().BeTrue();
        config.SkipTtsForShortNoPointResponses.Should().BeFalse();
    }
    [Fact]
    public void DefaultConfig_ContainsBuild017LifecycleDefaults()
    {
        var config = new AppConfig();

        config.StartWithWindows.Should().BeFalse();
        config.StartMinimizedToTray.Should().BeTrue();
        config.CrashLoggingEnabled.Should().BeTrue();
        config.AppLoggingEnabled.Should().BeTrue();
        config.LogLevel.Should().Be("Info");
        config.LogRetentionDays.Should().Be(7);
    }

    [Fact]
    public void DefaultConfig_ContainsPt006UserModeDefaults()
    {
        var config = new AppConfig();

        config.DeveloperModeEnabled.Should().BeFalse();
        config.ShowDeveloperTrayItems.Should().BeFalse();
        config.EnableDeveloperHotkeys.Should().BeFalse();
        config.ShowAdvancedDiagnostics.Should().BeFalse();
        config.AllowFakeProvidersInDeveloperMode.Should().BeTrue();
        config.AllowFakeProviderFallbackInNormalMode.Should().BeFalse();
        config.EnableProviderFallback.Should().BeFalse();
        config.FallbackToFakeOnWorkerFailure.Should().BeFalse();
        config.AiProvider.Should().Be("Claude");
        config.TranscriptProvider.Should().Be("Worker");
        config.TtsProvider.Should().Be("Worker");
    }
}
