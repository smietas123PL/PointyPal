using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class SelfTestReportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _reportPath;
    private readonly ConfigService _configService;

    public SelfTestReportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _reportPath = Path.Combine(_tempDir, "debug", "self-test-report.json");
        _configService = new ConfigService(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveLatestAsync_WritesReport_WhenDebugArtifactsEnabled()
    {
        _configService.Config.SaveDebugArtifacts = true;
        var service = new SelfTestReportService(_configService, _reportPath);
        var result = CreatePassingResult();

        await service.SaveLatestAsync(result);

        File.Exists(_reportPath).Should().BeTrue();
        service.LastResult.Should().BeSameAs(result);
        service.LastReportPath.Should().Be(_reportPath);
    }

    [Fact]
    public async Task SaveLatestAsync_DoesNotWriteReport_WhenDebugArtifactsDisabled()
    {
        _configService.Config.SaveDebugArtifacts = false;
        var service = new SelfTestReportService(_configService, _reportPath);
        var result = CreatePassingResult();

        await service.SaveLatestAsync(result);

        File.Exists(_reportPath).Should().BeFalse();
        service.LastResult.Should().BeSameAs(result);
        service.LastReportPath.Should().BeEmpty();
    }

    private static SelfTestResult CreatePassingResult()
    {
        var now = DateTime.Now;
        return new SelfTestResult
        {
            StartedAt = now,
            CompletedAt = now,
            DurationMs = 3,
            Mode = SelfTestMode.Quick,
            Passed = true,
            TotalScenarios = 1,
            PassedScenarios = 1,
            FailedScenarios = 0,
            ScenarioResults =
            [
                new SelfTestScenarioResult
                {
                    ScenarioName = "QuickAskNoPoint",
                    Passed = true,
                    DurationMs = 1,
                    AssertionsSummary = "ok"
                }
            ]
        };
    }
}
