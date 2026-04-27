using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class ProviderPolicyServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly ConfigService _configService;

    public ProviderPolicyServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PointyPalTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        _configService = new ConfigService(_configPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void NormalMode_DisallowsFakeProviders()
    {
        _configService.Config.AiProvider = "Fake";
        _configService.Config.TranscriptProvider = "Fake";
        _configService.Config.TtsEnabled = true;
        _configService.Config.TtsProvider = "Fake";

        var policy = new ProviderPolicyService(_configService);

        policy.CanUseFakeProviders().Should().BeFalse();
        policy.GetEffectiveAiProvider().Should().Be("Claude");
        policy.GetEffectiveTranscriptProvider().Should().Be("Worker");
        policy.GetEffectiveTtsProvider().Should().Be("Worker");
        policy.GetProviderSetupWarning().Should().Be(ProviderPolicyService.FakeProvidersDeveloperOnlyMessage);
    }

    [Fact]
    public void DeveloperMode_AllowsFakeProvidersWhenEnabled()
    {
        _configService.Config.DeveloperModeEnabled = true;
        _configService.Config.AllowFakeProvidersInDeveloperMode = true;
        _configService.Config.AiProvider = "Fake";
        _configService.Config.TranscriptProvider = "Fake";
        _configService.Config.TtsEnabled = true;
        _configService.Config.TtsProvider = "Fake";

        var policy = new ProviderPolicyService(_configService);

        policy.CanUseFakeProviders().Should().BeTrue();
        policy.GetEffectiveAiProvider().Should().Be("Fake");
        policy.GetEffectiveTranscriptProvider().Should().Be("Fake");
        policy.GetEffectiveTtsProvider().Should().Be("Fake");
    }

    [Fact]
    public void SafeMode_AllowsFakeProvidersAndDisablesRealProviders()
    {
        _configService.SetSafeMode(true, "test");

        var policy = new ProviderPolicyService(_configService);

        policy.CanUseFakeProviders().Should().BeTrue();
        policy.CanUseRealProviders().Should().BeFalse();
        policy.GetEffectiveAiProvider().Should().Be("Fake");
        policy.GetEffectiveTranscriptProvider().Should().Be("Fake");
    }

    [Fact]
    public void SelfTestMode_AllowsFakeProvidersOfflineOnly()
    {
        var policy = new ProviderPolicyService(_configService, ProviderRuntimeMode.SelfTest);

        policy.CanUseFakeProviders().Should().BeTrue();
        policy.CanUseRealProviders().Should().BeFalse();
        policy.GetEffectiveAiProvider().Should().Be("Fake");
    }

    [Fact]
    public void ValidateRealProviderConfiguration_ReportsMissingWorkerClientKey()
    {
        _configService.Config.WorkerBaseUrl = "https://example.workers.dev";
        _configService.Config.WorkerClientKey = "";

        var policy = new ProviderPolicyService(_configService);

        var result = policy.ValidateRealProviderConfiguration();

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Contain(UserMessages.ErrorWorkerKeyMissing);
    }

    [Fact]
    public void ExistingFakeConfig_DoesNotSilentlyUseFakeInNormalMode()
    {
        var config = new AppConfig
        {
            AiProvider = "Fake",
            TranscriptProvider = "Fake",
            TtsEnabled = true,
            TtsProvider = "Fake"
        };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(config));
        _configService.ReloadConfig();

        var policy = new ProviderPolicyService(_configService);

        policy.GetEffectiveAiProvider().Should().Be("Claude");
        policy.GetEffectiveTranscriptProvider().Should().Be("Worker");
        policy.GetEffectiveTtsProvider().Should().Be("Worker");
        policy.GetProviderSetupWarning().Should().Be(ProviderPolicyService.FakeProvidersDeveloperOnlyMessage);
    }

    [Fact]
    public void PartialOldConfig_LoadsWithPt006Defaults()
    {
        File.WriteAllText(_configPath, "{\"AiProvider\":\"Fake\"}");
        _configService.ReloadConfig();

        _configService.Config.DeveloperModeEnabled.Should().BeFalse();
        _configService.Config.EnableDeveloperHotkeys.Should().BeFalse();
        _configService.Config.AllowFakeProvidersInDeveloperMode.Should().BeTrue();
        _configService.Config.AllowFakeProviderFallbackInNormalMode.Should().BeFalse();
    }
}
