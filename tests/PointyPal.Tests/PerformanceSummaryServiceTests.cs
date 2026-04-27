using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class PerformanceSummaryServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly string _historyPath;
    private readonly string _summaryPath;

    public PerformanceSummaryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(Path.Combine(_tempDir, "config.json"));
        _configService.Config.SaveDebugArtifacts = true;
        _historyPath = Path.Combine(_tempDir, "debug", "interaction-timelines.jsonl");
        _summaryPath = Path.Combine(_tempDir, "debug", "performance-summary.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void RefreshSummary_CalculatesAverageAndPercentiles()
    {
        File.WriteAllLines(_historyPath, new[]
        {
            JsonSerializer.Serialize(CreateTimeline("a", 100, stt: 10, claude: 30, tts: 20), JsonOptions),
            JsonSerializer.Serialize(CreateTimeline("b", 200, stt: 20, claude: 40, tts: 30), JsonOptions),
            JsonSerializer.Serialize(CreateTimeline("c", 300, stt: 30, claude: 50, tts: 40), JsonOptions),
            JsonSerializer.Serialize(CreateTimeline("d", 400, stt: 40, claude: 60, tts: 50), JsonOptions)
        });

        var service = new PerformanceSummaryService(_configService, _historyPath, _summaryPath);

        var summary = service.RefreshSummary();

        summary.AverageTotalDurationMs.Should().Be(250);
        summary.P50TotalDurationMs.Should().Be(200);
        summary.P95TotalDurationMs.Should().Be(400);
        summary.AverageSttDurationMs.Should().Be(25);
        summary.AverageClaudeDurationMs.Should().Be(45);
        summary.AverageTtsDurationMs.Should().Be(35);
        summary.SlowestRecentInteractionId.Should().Be("d");
        summary.SlowestStepName.Should().Be(InteractionTimelineStepNames.ClaudeRequest);
        File.Exists(_summaryPath).Should().BeTrue();
    }

    private static InteractionTimeline CreateTimeline(
        string id,
        double total,
        double stt,
        double claude,
        double tts)
    {
        var now = DateTime.Now;
        return new InteractionTimeline
        {
            InteractionId = id,
            StartedAt = now.AddMilliseconds(-total),
            CompletedAt = now,
            TotalDurationMs = total,
            InteractionSource = InteractionSource.Hotkey,
            InteractionMode = InteractionMode.Assist,
            ProviderName = "Fake",
            Steps =
            [
                CreateStep(InteractionTimelineStepNames.TranscriptionRequest, stt),
                CreateStep(InteractionTimelineStepNames.ClaudeRequest, claude),
                CreateStep(InteractionTimelineStepNames.TtsRequest, tts),
                CreateStep(InteractionTimelineStepNames.ScreenshotCapture, 5),
                CreateStep(InteractionTimelineStepNames.UiAutomationCapture, 7)
            ]
        };
    }

    private static InteractionTimelineStep CreateStep(string name, double duration)
    {
        var now = DateTime.Now;
        return new InteractionTimelineStep
        {
            Name = name,
            StartedAt = now.AddMilliseconds(-duration),
            CompletedAt = now,
            DurationMs = duration,
            Success = true
        };
    }
}
