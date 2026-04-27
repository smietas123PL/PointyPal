using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.Core;

namespace PointyPal.Infrastructure;

public class InteractionTimelineService
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
    private readonly object _lock = new();
    private InteractionTimeline? _activeTimeline;
    private InteractionTimeline? _lastTimeline;
    private InteractionTimelineStep? _totalStep;

    public string LatestTimelinePath { get; }
    public string TimelineHistoryPath { get; }
    public InteractionTimeline? ActiveTimeline
    {
        get
        {
            lock (_lock) return _activeTimeline;
        }
    }

    public InteractionTimeline? LastTimeline
    {
        get
        {
            lock (_lock) return _lastTimeline ?? _activeTimeline;
        }
    }

    public string ActiveTimelineId
    {
        get
        {
            lock (_lock) return _activeTimeline?.InteractionId ?? "";
        }
    }

    public string CurrentActiveStep
    {
        get
        {
            lock (_lock)
            {
                return _activeTimeline?.Steps
                    .LastOrDefault(s => s.CompletedAt == null && s.Name != InteractionTimelineStepNames.TotalInteraction)
                    ?.Name ?? "";
            }
        }
    }

    public InteractionTimelineService(ConfigService configService)
        : this(
            configService,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "latest-interaction-timeline.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "interaction-timelines.jsonl"))
    {
    }

    public InteractionTimelineService(ConfigService configService, string latestTimelinePath, string timelineHistoryPath)
    {
        _configService = configService;
        LatestTimelinePath = latestTimelinePath;
        TimelineHistoryPath = timelineHistoryPath;
    }

    public InteractionTimeline StartTimeline(
        InteractionSource source,
        InteractionMode mode,
        string providerName = "")
    {
        lock (_lock)
        {
            var timeline = new InteractionTimeline
            {
                InteractionId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTime.Now,
                InteractionSource = source,
                InteractionMode = mode,
                ProviderName = providerName
            };

            _totalStep = new InteractionTimelineStep
            {
                Name = InteractionTimelineStepNames.TotalInteraction,
                StartedAt = timeline.StartedAt
            };
            timeline.Steps.Add(_totalStep);

            _activeTimeline = timeline;
            _lastTimeline = timeline;
            return timeline;
        }
    }

    public void SetProviderName(string providerName)
    {
        TryUpdate(timeline => timeline.ProviderName = providerName);
    }

    public void SetInteractionMode(InteractionMode mode)
    {
        TryUpdate(timeline => timeline.InteractionMode = mode);
    }

    public InteractionTimelineStep? StartStep(string name, Dictionary<string, string>? metadata = null)
    {
        try
        {
            lock (_lock)
            {
                if (_activeTimeline == null) return null;

                var step = new InteractionTimelineStep
                {
                    Name = name,
                    StartedAt = DateTime.Now,
                    Metadata = metadata == null ? null : new Dictionary<string, string>(metadata)
                };

                _activeTimeline.Steps.Add(step);
                return step;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline StartStep failed: {ex.Message}");
            return null;
        }
    }

    public void CompleteStep(InteractionTimelineStep? step, Dictionary<string, string>? metadata = null)
    {
        FinishStep(step, success: true, errorMessage: null, metadata);
    }

    public void FailStep(InteractionTimelineStep? step, string? errorMessage, Dictionary<string, string>? metadata = null)
    {
        FinishStep(step, success: false, errorMessage, metadata);
    }

    public void AddStepMetadata(InteractionTimelineStep? step, string key, string value)
    {
        if (step == null) return;

        try
        {
            lock (_lock)
            {
                step.Metadata ??= new Dictionary<string, string>();
                step.Metadata[key] = value;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline AddStepMetadata failed: {ex.Message}");
        }
    }

    public void CancelActiveStep(string reason)
    {
        try
        {
            lock (_lock)
            {
                var step = _activeTimeline?.Steps
                    .LastOrDefault(s => s.CompletedAt == null && s.Name != InteractionTimelineStepNames.TotalInteraction);
                if (step != null)
                {
                    FinishStepUnderLock(step, success: false, reason, metadata: null);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline CancelActiveStep failed: {ex.Message}");
        }
    }

    public async Task CompleteTimelineAsync(bool wasCancelled = false, string? errorMessage = null)
    {
        await CompleteTimelineAsync(null, wasCancelled, errorMessage);
    }

    public async Task CompleteTimelineAsync(string? expectedInteractionId, bool wasCancelled = false, string? errorMessage = null)
    {
        InteractionTimeline? completedTimeline = null;

        try
        {
            lock (_lock)
            {
                if (_activeTimeline == null) return;
                if (!string.IsNullOrWhiteSpace(expectedInteractionId) &&
                    _activeTimeline.InteractionId != expectedInteractionId)
                {
                    return;
                }

                var now = DateTime.Now;
                _activeTimeline.CompletedAt = now;
                _activeTimeline.TotalDurationMs = (now - _activeTimeline.StartedAt).TotalMilliseconds;
                _activeTimeline.WasCancelled = wasCancelled;
                _activeTimeline.ErrorMessage = errorMessage;

                foreach (var step in _activeTimeline.Steps.Where(s => s.CompletedAt == null).ToList())
                {
                    bool stepSuccess = !wasCancelled && string.IsNullOrWhiteSpace(errorMessage);
                    FinishStepUnderLock(step, stepSuccess, stepSuccess ? null : errorMessage ?? "Cancelled", metadata: null, now);
                }

                _lastTimeline = _activeTimeline;
                completedTimeline = _activeTimeline;
                _activeTimeline = null;
                _totalStep = null;
            }

            await PersistAsync(completedTimeline, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline CompleteTimeline failed: {ex.Message}");
        }
    }

    public void ClearTimelineHistory()
    {
        try
        {
            if (File.Exists(TimelineHistoryPath))
            {
                File.Delete(TimelineHistoryPath);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline history clear failed: {ex.Message}");
        }
    }

    private void FinishStep(
        InteractionTimelineStep? step,
        bool success,
        string? errorMessage,
        Dictionary<string, string>? metadata)
    {
        if (step == null) return;

        try
        {
            lock (_lock)
            {
                FinishStepUnderLock(step, success, errorMessage, metadata);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline FinishStep failed: {ex.Message}");
        }
    }

    private static void FinishStepUnderLock(
        InteractionTimelineStep step,
        bool success,
        string? errorMessage,
        Dictionary<string, string>? metadata,
        DateTime? completedAt = null)
    {
        if (step.CompletedAt != null) return;

        var now = completedAt ?? DateTime.Now;
        step.CompletedAt = now;
        step.DurationMs = (now - step.StartedAt).TotalMilliseconds;
        step.Success = success;
        step.ErrorMessage = errorMessage;

        if (metadata != null)
        {
            step.Metadata ??= new Dictionary<string, string>();
            foreach (var pair in metadata)
            {
                step.Metadata[pair.Key] = pair.Value;
            }
        }
    }

    private void TryUpdate(Action<InteractionTimeline> update)
    {
        try
        {
            lock (_lock)
            {
                if (_activeTimeline != null)
                {
                    update(_activeTimeline);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline update failed: {ex.Message}");
        }
    }

    private async Task PersistAsync(InteractionTimeline? timeline, CancellationToken cancellationToken)
    {
        if (timeline == null) return;

        var config = _configService.Config;
        if (!config.SaveDebugArtifacts || !config.EnableTimelineLogging)
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(LatestTimelinePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(timeline, JsonOptions);
            await File.WriteAllTextAsync(LatestTimelinePath, json, cancellationToken);

            if (config.SaveTimelineHistory && config.MaxTimelineHistoryItems > 0)
            {
                string line = JsonSerializer.Serialize(timeline, JsonLineOptions);
                await File.AppendAllLinesAsync(TimelineHistoryPath, new[] { line }, cancellationToken);
                TrimHistory(config.MaxTimelineHistoryItems);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline persist failed: {ex.Message}");
        }
    }

    private void TrimHistory(int maxItems)
    {
        try
        {
            if (maxItems <= 0 || !File.Exists(TimelineHistoryPath)) return;

            var lines = File.ReadAllLines(TimelineHistoryPath);
            if (lines.Length <= maxItems) return;

            File.WriteAllLines(TimelineHistoryPath, lines.Skip(lines.Length - maxItems));
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Timeline history trim failed: {ex.Message}");
        }
    }
}
