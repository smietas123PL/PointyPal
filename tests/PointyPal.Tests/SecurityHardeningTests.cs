using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class SecurityHardeningTests
{
    [Fact]
    public void DefaultConfig_HasPrivacyFirstDefaults()
    {
        var config = new AppConfig();

        config.SaveDebugArtifacts.Should().BeFalse("Privacy First: SaveDebugArtifacts must default to false.");
        config.SaveScreenshots.Should().BeFalse("Privacy First: SaveScreenshots must default to false.");
        config.SaveRecordings.Should().BeFalse("Privacy First: SaveRecordings must default to false.");
        config.SaveTtsAudio.Should().BeFalse("Privacy First: SaveTtsAudio must default to false.");
        config.SaveInteractionHistory.Should().BeFalse("Privacy First: SaveInteractionHistory must default to false.");
        config.SaveUiAutomationDebug.Should().BeFalse("Privacy First: SaveUiAutomationDebug must default to false.");
        config.RedactDebugPayloads.Should().BeTrue("Privacy First: RedactDebugPayloads must default to true.");
    }

    [Fact]
    public void AppLogService_RedactsWorkerClientKey()
    {
        string key = "PP_SECRET_12345";
        string input = $"Sending request with X-PointyPal-Client-Key: {key}";
        string expected = "Sending request with X-PointyPal-Client-Key: [REDACTED_SECRET]";

        AppLogService.Redact(input).Should().Be(expected);
    }

    [Fact]
    public async Task PreflightCheck_FailsIfWorkerClientKeyIsMissingForRealProvider()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            var config = new AppConfig 
            { 
                AiProvider = "Claude", 
                WorkerBaseUrl = "https://my-worker.workers.dev",
                WorkerClientKey = "" // Missing
            };
            var configService = new ConfigService(tempFile);
            configService.SaveConfig(config);
            var preflight = new PreflightCheckService(configService, null, null);

            var report = await preflight.RunAllChecksAsync();
            
            var authCheck = report.Items.FirstOrDefault(i => i.Name == "Worker Auth" || i.Name == UserMessages.WorkerConnection);
            authCheck.Should().NotBeNull();
            authCheck!.Status.Should().Be(PreflightStatus.Fail);
            authCheck.Message.Should().Contain(UserMessages.ErrorWorkerClientKeyMissing);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PreflightCheck_PassesIfWorkerClientKeyIsPresentForRealProvider()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            var config = new AppConfig 
            { 
                AiProvider = "Claude", 
                WorkerBaseUrl = "https://my-worker.workers.dev",
                WorkerClientKey = "VALID_KEY" 
            };
            var configService = new ConfigService(tempFile);
            configService.SaveConfig(config);
            var preflight = new PreflightCheckService(configService, null, null);

            var report = await preflight.RunAllChecksAsync();
            
            var authCheck = report.Items.FirstOrDefault(i => i.Name == UserMessages.WorkerConnection);
            authCheck.Should().NotBeNull();
            authCheck!.Status.Should().Be(PreflightStatus.Pass);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
