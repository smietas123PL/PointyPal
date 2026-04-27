using System.Text.Json;
using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class InteractionTimelineServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;
    private readonly string _latestPath;
    private readonly string _historyPath;

    public InteractionTimelineServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(Path.Combine(_tempDir, "config.json"));
        _configService.Config.SaveDebugArtifacts = true;
        _configService.Config.SaveTimelineHistory = true;
        _latestPath = Path.Combine(_tempDir, "debug", "latest-interaction-timeline.json");
        _historyPath = Path.Combine(_tempDir, "debug", "interaction-timelines.jsonl");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CompleteTimelineAsync_WritesTimedStepAndFiles()
    {
        var service = new InteractionTimelineService(_configService, _latestPath, _historyPath);

        service.StartTimeline(InteractionSource.Hotkey, InteractionMode.Assist, "Fake");
        var step = service.StartStep(InteractionTimelineStepNames.ClaudeRequest);
        await Task.Delay(5);
        service.CompleteStep(step);
        await service.CompleteTimelineAsync();

        service.LastTimeline.Should().NotBeNull();
        service.LastTimeline!.Steps.Should().Contain(s =>
            s.Name == InteractionTimelineStepNames.ClaudeRequest &&
            s.Success &&
            s.DurationMs > 0);
        File.Exists(_latestPath).Should().BeTrue();
        File.Exists(_historyPath).Should().BeTrue();
    }

    [Fact]
    public async Task CompleteTimelineAsync_MarksCancelledStepAndTimeline()
    {
        var service = new InteractionTimelineService(_configService, _latestPath, _historyPath);

        service.StartTimeline(InteractionSource.Hotkey, InteractionMode.Assist, "Fake");
        service.StartStep(InteractionTimelineStepNames.TtsRequest);
        await service.CompleteTimelineAsync(wasCancelled: true, errorMessage: "Escape pressed");

        var timeline = service.LastTimeline!;
        timeline.WasCancelled.Should().BeTrue();
        timeline.ErrorMessage.Should().Be("Escape pressed");
        timeline.Steps.Should().Contain(s =>
            s.Name == InteractionTimelineStepNames.TtsRequest &&
            !s.Success &&
            s.ErrorMessage == "Escape pressed");
    }

    [Fact]
    public async Task FailStep_RecordsErrorWithoutBreakingTimeline()
    {
        var service = new InteractionTimelineService(_configService, _latestPath, _historyPath);

        service.StartTimeline(InteractionSource.Hotkey, InteractionMode.Assist, "Fake");
        var step = service.StartStep(InteractionTimelineStepNames.UiAutomationCapture);
        service.FailStep(step, "UIA timeout");
        await service.CompleteTimelineAsync();

        service.LastTimeline!.Steps.Should().Contain(s =>
            s.Name == InteractionTimelineStepNames.UiAutomationCapture &&
            !s.Success &&
            s.ErrorMessage == "UIA timeout");
    }

    [Fact]
    public async Task CompleteTimelineAsync_DoesNotWriteFiles_WhenDebugArtifactsDisabled()
    {
        _configService.Config.SaveDebugArtifacts = false;
        var service = new InteractionTimelineService(_configService, _latestPath, _historyPath);

        service.StartTimeline(InteractionSource.Hotkey, InteractionMode.Assist, "Fake");
        var step = service.StartStep(InteractionTimelineStepNames.ClaudeRequest);
        service.CompleteStep(step);
        await service.CompleteTimelineAsync();

        service.LastTimeline.Should().NotBeNull();
        File.Exists(_latestPath).Should().BeFalse();
        File.Exists(_historyPath).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteTimelineAsync_TrimsHistoryToConfiguredMaximum()
    {
        _configService.Config.MaxTimelineHistoryItems = 2;
        var service = new InteractionTimelineService(_configService, _latestPath, _historyPath);

        for (int i = 0; i < 3; i++)
        {
            service.StartTimeline(InteractionSource.Hotkey, InteractionMode.Assist, $"Fake{i}");
            var step = service.StartStep(InteractionTimelineStepNames.ClaudeRequest);
            service.CompleteStep(step);
            await service.CompleteTimelineAsync();
        }

        File.ReadAllLines(_historyPath).Should().HaveCount(2);
        File.ReadAllText(_historyPath).Should().NotContain("Fake0");
    }
}
