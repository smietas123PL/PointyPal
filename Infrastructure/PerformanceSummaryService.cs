using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PointyPal.Core;

namespace PointyPal.Infrastructure;

public class PerformanceSummary
{
    public DateTime CalculatedAt { get; set; } = DateTime.Now;
    public int TimelineCount { get; set; }
    public double AverageTotalDurationMs { get; set; }
    public double P50TotalDurationMs { get; set; }
    public double P95TotalDurationMs { get; set; }
    public double AverageSttDurationMs { get; set; }
    public double AverageClaudeDurationMs { get; set; }
    public double AverageTtsDurationMs { get; set; }
    public double AverageScreenshotCaptureDurationMs { get; set; }
    public double AverageUiAutomationDurationMs { get; set; }
    public string SlowestRecentInteractionId { get; set; } = "";
    public double SlowestRecentInteractionDurationMs { get; set; }
    public string SlowestStepName { get; set; } = "";
    public double SlowestStepDurationMs { get; set; }
    public double LastTotalDurationMs { get; set; }
    public double LastSttDurationMs { get; set; }
    public double LastClaudeDurationMs { get; set; }
    public double LastTtsDurationMs { get; set; }
    public double LastScreenshotCaptureDurationMs { get; set; }
    public double LastUiAutomationDurationMs { get; set; }
    public string LastErrorOrCancellationReason { get; set; } = "";
    public string LatestTimelinePath { get; set; } = "";
    public string PerformanceSummaryPath { get; set; } = "";
}

public class PerformanceSummaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConfigService _configService;
    private readonly string _timelineHistoryPath;
    private readonly AppLogService? _appLog;

    public string SummaryPath { get; }
    public PerformanceSummary LastSummary { get; private set; } = new();

    public PerformanceSummaryService(ConfigService configService, AppLogService? appLog = null)
        : this(
            configService,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "interaction-timelines.jsonl"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "performance-summary.json"),
            appLog)
    {
    }

    internal PerformanceSummaryService(ConfigService configService, string timelineHistoryPath, string summaryPath, AppLogService? appLog = null)
    {
        _configService = configService;
        _timelineHistoryPath = timelineHistoryPath;
        SummaryPath = summaryPath;
        _appLog = appLog;
        LastSummary.PerformanceSummaryPath = SummaryPath;
    }

    public PerformanceSummary RefreshSummary(InteractionTimeline? latestInMemoryTimeline = null)
    {
        try
        {
            var timelines = LoadRecentTimelines();

            if (latestInMemoryTimeline != null &&
                !timelines.Any(t => t.InteractionId == latestInMemoryTimeline.InteractionId))
            {
                timelines.Add(latestInMemoryTimeline);
            }

            timelines = timelines
                .Where(t => t.CompletedAt != null || t.TotalDurationMs > 0)
                .OrderBy(t => t.StartedAt)
                .TakeLast(Math.Max(1, _configService.Config.MaxTimelineHistoryItems))
                .ToList();

            var summary = BuildSummary(timelines);
            summary.PerformanceSummaryPath = SummaryPath;
            LastSummary = summary;
            SaveSummary(summary);
            _appLog?.Info(
                "PerformanceSummary",
                $"TimelineCount={summary.TimelineCount}; P50Ms={Math.Round(summary.P50TotalDurationMs)}; P95Ms={Math.Round(summary.P95TotalDurationMs)}; SlowestStep={summary.SlowestStepName}");
            return summary;
        }
        catch (Exception ex)
        {
            _appLog?.Warning("PerformanceSummaryFailed", $"Error={ex.Message}");
            DebugLogger.LogStatic($"Performance summary refresh failed: {ex.Message}");
            return LastSummary;
        }
    }

    private List<InteractionTimeline> LoadRecentTimelines()
    {
        var timelines = new List<InteractionTimeline>();
        if (!File.Exists(_timelineHistoryPath)) return timelines;

        try
        {
            foreach (string line in File.ReadLines(_timelineHistoryPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var timeline = JsonSerializer.Deserialize<InteractionTimeline>(line, JsonLineOptions);
                    if (timeline != null)
                    {
                        timelines.Add(timeline);
                    }
                }
                catch
                {
                    // Ignore malformed history lines.
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Performance summary load failed: {ex.Message}");
        }

        return timelines;
    }

    private static PerformanceSummary BuildSummary(IReadOnlyList<InteractionTimeline> timelines)
    {
        var summary = new PerformanceSummary
        {
            CalculatedAt = DateTime.Now,
            TimelineCount = timelines.Count
        };

        if (timelines.Count == 0)
        {
            return summary;
        }

        var totals = timelines
            .Select(t => t.TotalDurationMs)
            .Where(d => d > 0)
            .OrderBy(d => d)
            .ToList();

        if (totals.Count > 0)
        {
            summary.AverageTotalDurationMs = totals.Average();
            summary.P50TotalDurationMs = PercentileNearestRank(totals, 0.50);
            summary.P95TotalDurationMs = PercentileNearestRank(totals, 0.95);
        }

        summary.AverageSttDurationMs = AverageStep(timelines, InteractionTimelineStepNames.TranscriptionRequest);
        summary.AverageClaudeDurationMs = AverageStep(timelines, InteractionTimelineStepNames.ClaudeRequest);
        summary.AverageTtsDurationMs = AverageStep(timelines, InteractionTimelineStepNames.TtsRequest);
        summary.AverageScreenshotCaptureDurationMs = AverageStep(timelines, InteractionTimelineStepNames.ScreenshotCapture);
        summary.AverageUiAutomationDurationMs = AverageStep(timelines, InteractionTimelineStepNames.UiAutomationCapture);

        var slowestInteraction = timelines.OrderByDescending(t => t.TotalDurationMs).FirstOrDefault();
        if (slowestInteraction != null)
        {
            summary.SlowestRecentInteractionId = slowestInteraction.InteractionId;
            summary.SlowestRecentInteractionDurationMs = slowestInteraction.TotalDurationMs;
        }

        var slowestStep = timelines
            .SelectMany(t => t.Steps)
            .Where(s => s.Name != InteractionTimelineStepNames.TotalInteraction)
            .OrderByDescending(s => s.DurationMs)
            .FirstOrDefault();

        if (slowestStep != null)
        {
            summary.SlowestStepName = slowestStep.Name;
            summary.SlowestStepDurationMs = slowestStep.DurationMs;
        }

        var last = timelines.OrderByDescending(t => t.StartedAt).First();
        summary.LastTotalDurationMs = last.TotalDurationMs;
        summary.LastSttDurationMs = StepDuration(last, InteractionTimelineStepNames.TranscriptionRequest);
        summary.LastClaudeDurationMs = StepDuration(last, InteractionTimelineStepNames.ClaudeRequest);
        summary.LastTtsDurationMs = StepDuration(last, InteractionTimelineStepNames.TtsRequest);
        summary.LastScreenshotCaptureDurationMs = StepDuration(last, InteractionTimelineStepNames.ScreenshotCapture);
        summary.LastUiAutomationDurationMs = StepDuration(last, InteractionTimelineStepNames.UiAutomationCapture);
        summary.LastErrorOrCancellationReason = last.WasCancelled
            ? last.ErrorMessage ?? "Cancelled"
            : last.ErrorMessage ?? "";

        return summary;
    }

    private static double AverageStep(IReadOnlyList<InteractionTimeline> timelines, string stepName)
    {
        var durations = timelines
            .Select(t => StepDuration(t, stepName))
            .Where(d => d > 0)
            .ToList();

        return durations.Count == 0 ? 0 : durations.Average();
    }

    private static double StepDuration(InteractionTimeline timeline, string stepName)
    {
        return timeline.Steps
            .Where(s => s.Name == stepName)
            .Select(s => s.DurationMs)
            .DefaultIfEmpty(0)
            .Last();
    }

    private static double PercentileNearestRank(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;

        int rank = (int)Math.Ceiling(percentile * sortedValues.Count);
        int index = Math.Clamp(rank - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private void SaveSummary(PerformanceSummary summary)
    {
        var config = _configService.Config;
        if (!config.SaveDebugArtifacts || !config.EnableTimelineLogging)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(SummaryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(summary, JsonOptions);
            File.WriteAllText(SummaryPath, json);
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Performance summary save failed: {ex.Message}");
        }
    }
}
