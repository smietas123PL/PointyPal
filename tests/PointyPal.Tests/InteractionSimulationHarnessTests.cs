using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class InteractionSimulationHarnessTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;

    public InteractionSimulationHarnessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(Path.Combine(_tempDir, "config.json"));
        _configService.Config.SaveDebugArtifacts = true;
        _configService.Config.SaveTimelineHistory = true;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunQuickAsync_RunsExpectedOfflineSubset()
    {
        _configService.Config.WorkerBaseUrl = "";
        _configService.Config.AiProvider = "Claude";
        _configService.Config.TranscriptProvider = "Worker";
        _configService.Config.TtsProvider = "Worker";

        var harness = new InteractionSimulationHarness(_configService);

        var result = await harness.RunQuickAsync();

        result.Mode.Should().Be(SelfTestMode.Quick);
        result.Passed.Should().BeTrue();
        result.TotalScenarios.Should().Be(4);
        result.ScenarioResults.Select(s => s.ScenarioName)
            .Should()
            .Equal(InteractionSimulationHarness.QuickScenarioNames);
        result.ScenarioResults.Should().OnlyContain(s => s.Passed);
    }

    [Fact]
    public async Task RunFullAsync_RunsAllScenariosWithoutWorkerUrl()
    {
        _configService.Config.WorkerBaseUrl = "";
        _configService.Config.AiProvider = "Claude";
        _configService.Config.TranscriptProvider = "Worker";
        _configService.Config.TtsProvider = "Worker";

        var harness = new InteractionSimulationHarness(_configService);

        var result = await harness.RunFullAsync();

        result.Mode.Should().Be(SelfTestMode.Full);
        result.Passed.Should().BeTrue();
        result.TotalScenarios.Should().Be(10);
        result.ScenarioResults.Select(s => s.ScenarioName)
            .Should()
            .Equal(InteractionSimulationHarness.AllScenarioNames);
        result.ScenarioResults.Should().OnlyContain(s => s.Passed);
    }

    [Fact]
    public async Task RunLatencySelfTestAsync_ProducesOfflineTimingData()
    {
        _configService.Config.WorkerBaseUrl = "";
        _configService.Config.AiProvider = "Claude";
        _configService.Config.TranscriptProvider = "Worker";
        _configService.Config.TtsProvider = "Worker";
        string latestPath = Path.Combine(_tempDir, "debug", "latest-interaction-timeline.json");
        string historyPath = Path.Combine(_tempDir, "debug", "interaction-timelines.jsonl");
        string summaryPath = Path.Combine(_tempDir, "debug", "performance-summary.json");
        var timelineService = new InteractionTimelineService(_configService, latestPath, historyPath);
        var performanceService = new PerformanceSummaryService(_configService, historyPath, summaryPath);
        var harness = new InteractionSimulationHarness(_configService);

        var result = await harness.RunLatencySelfTestAsync(timelineService, performanceService);

        result.Mode.Should().Be(SelfTestMode.LatencySelfTest);
        result.Passed.Should().BeTrue();
        result.TotalScenarios.Should().Be(InteractionSimulationHarness.LatencyScenarioNames.Length);
        File.Exists(historyPath).Should().BeTrue();
        performanceService.LastSummary.TimelineCount.Should().BeGreaterThan(0);
        performanceService.LastSummary.P50TotalDurationMs.Should().BeGreaterThan(0);
    }
}
