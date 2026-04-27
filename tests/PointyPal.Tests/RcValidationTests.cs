using System;
using System.IO;
using System.Linq;
using Xunit;
using PointyPal.Core;
using PointyPal.Infrastructure;

namespace PointyPal.Tests;

public class RcValidationTests
{
    [Fact]
    public void NormalMode_AlwaysUsesRealProviders_EvenIfConfigSaysFake()
    {
        // Arrange
        var config = new AppConfig
        {
            AiProvider = "Fake",
            TranscriptProvider = "Fake",
            TtsProvider = "Fake",
            TtsEnabled = true,
            DeveloperModeEnabled = false
        };
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        var configService = new ConfigService(tempPath);
        configService.SaveConfig(config);
        var policy = new ProviderPolicyService(configService);

        // Act
        string ai = policy.GetEffectiveAiProvider();
        string stt = policy.GetEffectiveTranscriptProvider();
        string tts = policy.GetEffectiveTtsProvider();

        // Assert
        Assert.Equal("Claude", ai);
        Assert.Equal("Worker", stt);
        Assert.Equal("Worker", tts);
    }

    [Fact]
    public void NormalMode_WithFakeConfig_ShowsWarning()
    {
        // Arrange
        var config = new AppConfig
        {
            AiProvider = "Fake",
            DeveloperModeEnabled = false
        };
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        var configService = new ConfigService(tempPath);
        configService.SaveConfig(config);
        var policy = new ProviderPolicyService(configService);

        // Act
        var status = policy.GetProviderStatusForUi();

        // Assert
        Assert.Equal(ProviderPolicyService.FakeProvidersDeveloperOnlyMessage, status.SetupWarning);
    }

    [Fact]
    public void DeveloperMode_CanUseFakeProviders_IfAllowed()
    {
        // Arrange
        var config = new AppConfig
        {
            AiProvider = "Fake",
            DeveloperModeEnabled = true,
            AllowFakeProvidersInDeveloperMode = true
        };
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        var configService = new ConfigService(tempPath);
        configService.SaveConfig(config);
        var policy = new ProviderPolicyService(configService);

        // Act
        string ai = policy.GetEffectiveAiProvider();

        // Assert
        Assert.Equal("Fake", ai);
    }

    [Fact]
    public void SafeMode_ForcesFakeProviders()
    {
        // Arrange
        var config = new AppConfig();
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        var configService = new ConfigService(tempPath);
        configService.SetSafeMode(true, "Test");
        var policy = new ProviderPolicyService(configService);

        // Act
        string ai = policy.GetEffectiveAiProvider();
        string stt = policy.GetEffectiveTranscriptProvider();

        // Assert
        Assert.Equal("Fake", ai);
        Assert.Equal("Fake", stt);
    }

    [Fact]
    public void ReleaseDocuments_ExistInSource()
    {
        // We expect these in the repo root / docs
        string baseDir = AppContext.BaseDirectory;
        // Navigate up to find docs
        string? projectRoot = FindProjectRoot(baseDir);
        Assert.NotNull(projectRoot);

        string docsDir = Path.Combine(projectRoot, "docs");
        string[] requiredDocs = {
            "user-modes.md",
            "local-release.md",
            "recovery.md",
            "installer-smoke-test.md",
            "signing-runbook.md",
            "private-rc-dogfood-checklist.md",
            "private-rc-known-warnings.md"
        };

        foreach (var doc in requiredDocs)
        {
            Assert.True(File.Exists(Path.Combine(docsDir, doc)), $"Missing document: {doc}");
        }
    }

    private string? FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PointyPal.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
