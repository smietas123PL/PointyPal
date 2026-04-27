using System;
using System.Collections.Generic;

namespace PointyPal.Core;

public enum InteractionSource
{
    Voice,
    QuickAsk,
    Hotkey,
    SelfTest
}

public static class InteractionTimelineStepNames
{
    public const string PushToTalkRecording = "PushToTalkRecording";
    public const string AudioFileWrite = "AudioFileWrite";
    public const string TranscriptionRequest = "TranscriptionRequest";
    public const string ScreenshotCapture = "ScreenshotCapture";
    public const string UiAutomationCapture = "UiAutomationCapture";
    public const string PromptPayloadBuild = "PromptPayloadBuild";
    public const string ClaudeRequest = "ClaudeRequest";
    public const string AiResponseParse = "AiResponseParse";
    public const string PointValidation = "PointValidation";
    public const string TtsRequest = "TtsRequest";
    public const string AudioPlaybackStart = "AudioPlaybackStart";
    public const string PointerFlight = "PointerFlight";
    public const string BubbleDisplay = "BubbleDisplay";
    public const string TotalInteraction = "TotalInteraction";
}

public class InteractionTimelineStep
{
    public string Name { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class InteractionTimeline
{
    public string InteractionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double TotalDurationMs { get; set; }
    public InteractionSource InteractionSource { get; set; }
    public InteractionMode InteractionMode { get; set; }
    public string ProviderName { get; set; } = "";
    public List<InteractionTimelineStep> Steps { get; set; } = new();
    public bool WasCancelled { get; set; }
    public string? ErrorMessage { get; set; }
}
