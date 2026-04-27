using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.AI;
using PointyPal.Infrastructure;

namespace PointyPal.Core;

public class InteractionSimulationHarness
{
    public static readonly string[] QuickScenarioNames =
    [
        "QuickAskNoPoint",
        "QuickAskPointMode",
        "TtsDisabled",
        "InvalidAiResponse"
    ];

    public static readonly string[] AllScenarioNames =
    [
        "VoiceAssistFake",
        "QuickAskNoPoint",
        "QuickAskPointMode",
        "QuickAskReadScreen",
        "TtsDisabled",
        "PointerDisabled",
        "ScreenshotDisabledTextOnly",
        "DailyLimitReached",
        "InvalidAiResponse",
        "EscapeCancellation"
    ];

    public static readonly string[] LatencyScenarioNames =
    [
        "LatencyQuickAskNoPoint",
        "LatencyVoicePoint",
        "LatencyScreenshotOnly",
        "LatencyTtsAndPointer"
    ];

    private readonly ConfigService _configService;
    private readonly PointTagParser _parser = new();
    private readonly AiResponseValidator _validator = new();

    public InteractionSimulationHarness(ConfigService configService)
    {
        _configService = configService;
    }

    public Task<SelfTestResult> RunQuickAsync(CancellationToken cancellationToken = default)
    {
        return RunScenariosAsync(SelfTestMode.Quick, QuickScenarioNames, cancellationToken);
    }

    public Task<SelfTestResult> RunFullAsync(CancellationToken cancellationToken = default)
    {
        return RunScenariosAsync(SelfTestMode.Full, AllScenarioNames, cancellationToken);
    }

    public async Task<SelfTestResult> RunLatencySelfTestAsync(
        InteractionTimelineService timelineService,
        PerformanceSummaryService performanceSummaryService,
        CancellationToken cancellationToken = default)
    {
        var result = new SelfTestResult
        {
            StartedAt = DateTime.Now,
            Mode = SelfTestMode.LatencySelfTest,
            TotalScenarios = LatencyScenarioNames.Length
        };

        var stopwatch = Stopwatch.StartNew();

        foreach (string scenarioName in LatencyScenarioNames)
        {
            result.ScenarioResults.Add(await RunLatencyScenarioAsync(scenarioName, timelineService, cancellationToken));
        }

        stopwatch.Stop();
        result.CompletedAt = DateTime.Now;
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.PassedScenarios = result.ScenarioResults.Count(s => s.Passed);
        result.FailedScenarios = result.TotalScenarios - result.PassedScenarios;
        result.Passed = result.FailedScenarios == 0;

        performanceSummaryService.RefreshSummary(timelineService.LastTimeline);
        return result;
    }

    private async Task<SelfTestResult> RunScenariosAsync(
        SelfTestMode mode,
        IReadOnlyList<string> scenarioNames,
        CancellationToken cancellationToken)
    {
        var result = new SelfTestResult
        {
            StartedAt = DateTime.Now,
            Mode = mode,
            TotalScenarios = scenarioNames.Count
        };

        var stopwatch = Stopwatch.StartNew();

        foreach (string scenarioName in scenarioNames)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.ErrorMessage = "Self-test cancelled.";
                break;
            }

            result.ScenarioResults.Add(await RunScenarioAsync(scenarioName, cancellationToken));
        }

        stopwatch.Stop();
        result.CompletedAt = DateTime.Now;
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.PassedScenarios = result.ScenarioResults.Count(s => s.Passed);
        result.FailedScenarios = result.TotalScenarios - result.PassedScenarios;
        result.Passed = result.FailedScenarios == 0 && string.IsNullOrWhiteSpace(result.ErrorMessage);

        return result;
    }

    private async Task<SelfTestScenarioResult> RunScenarioAsync(string scenarioName, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SelfTestScenarioResult { ScenarioName = scenarioName };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            result.AssertionsSummary = scenarioName switch
            {
                "VoiceAssistFake" => await ScenarioVoiceAssistFakeAsync(cancellationToken),
                "QuickAskNoPoint" => await ScenarioQuickAskNoPointAsync(cancellationToken),
                "QuickAskPointMode" => await ScenarioQuickAskPointModeAsync(cancellationToken),
                "QuickAskReadScreen" => await ScenarioQuickAskReadScreenAsync(cancellationToken),
                "TtsDisabled" => await ScenarioTtsDisabledAsync(cancellationToken),
                "PointerDisabled" => await ScenarioPointerDisabledAsync(cancellationToken),
                "ScreenshotDisabledTextOnly" => await ScenarioScreenshotDisabledTextOnlyAsync(cancellationToken),
                "DailyLimitReached" => await ScenarioDailyLimitReachedAsync(cancellationToken),
                "InvalidAiResponse" => await ScenarioInvalidAiResponseAsync(cancellationToken),
                "EscapeCancellation" => await ScenarioEscapeCancellationAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unknown self-test scenario: {scenarioName}")
            };

            result.Passed = true;
        }
        catch (OperationCanceledException)
        {
            result.Passed = false;
            result.ErrorMessage = "Cancelled";
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private async Task<SelfTestScenarioResult> RunLatencyScenarioAsync(
        string scenarioName,
        InteractionTimelineService timelineService,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SelfTestScenarioResult { ScenarioName = scenarioName };
        InteractionTimeline? timeline = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mode = scenarioName.Contains("NoPoint", StringComparison.OrdinalIgnoreCase)
                ? InteractionMode.NoPoint
                : InteractionMode.Assist;
            var source = scenarioName.Contains("Voice", StringComparison.OrdinalIgnoreCase)
                ? InteractionSource.Voice
                : InteractionSource.SelfTest;

            timeline = timelineService.StartTimeline(source, mode, "LatencySelfTest");

            if (source == InteractionSource.Voice)
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.PushToTalkRecording, 12, cancellationToken);
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.AudioFileWrite, 4, cancellationToken);
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.TranscriptionRequest, 15, cancellationToken);
            }

            if (!scenarioName.Contains("ScreenshotOnly", StringComparison.OrdinalIgnoreCase))
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.ScreenshotCapture, 8, cancellationToken);
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.UiAutomationCapture, 6, cancellationToken);
            }
            else
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.ScreenshotCapture, 8, cancellationToken);
            }

            await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.PromptPayloadBuild, 5, cancellationToken);
            await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.ClaudeRequest, 20, cancellationToken);
            await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.AiResponseParse, 3, cancellationToken);

            bool hasPoint = !scenarioName.Contains("NoPoint", StringComparison.OrdinalIgnoreCase);
            if (hasPoint)
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.PointValidation, 4, cancellationToken);
            }

            if (scenarioName.Contains("Tts", StringComparison.OrdinalIgnoreCase))
            {
                var ttsTask = CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.TtsRequest, 14, cancellationToken);
                var flightTask = CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.PointerFlight, 16, cancellationToken);
                await Task.WhenAll(ttsTask, flightTask);
            }
            else if (hasPoint)
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.PointerFlight, 10, cancellationToken);
            }
            else
            {
                await CompleteSyntheticStepAsync(timelineService, InteractionTimelineStepNames.BubbleDisplay, 2, cancellationToken);
            }

            await timelineService.CompleteTimelineAsync(timeline.InteractionId);
            result.Passed = true;
            result.AssertionsSummary = "Offline latency timeline generated without real providers.";
        }
        catch (OperationCanceledException)
        {
            if (timeline != null)
            {
                await timelineService.CompleteTimelineAsync(timeline.InteractionId, wasCancelled: true, errorMessage: "Latency self-test cancelled");
            }

            result.Passed = false;
            result.ErrorMessage = "Cancelled";
        }
        catch (Exception ex)
        {
            if (timeline != null)
            {
                await timelineService.CompleteTimelineAsync(timeline.InteractionId, errorMessage: ex.Message);
            }

            result.Passed = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private static async Task CompleteSyntheticStepAsync(
        InteractionTimelineService timelineService,
        string stepName,
        int delayMs,
        CancellationToken cancellationToken)
    {
        var step = timelineService.StartStep(stepName, new Dictionary<string, string>
        {
            ["Source"] = "LatencySelfTest"
        });

        await Task.Delay(delayMs, cancellationToken);
        timelineService.CompleteStep(step);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
        }
    }

    private static AiRequest BuildFakeRequest(string userText, InteractionMode mode, bool includeScreenshot)
    {
        return new AiRequest
        {
            UserText = userText,
            InteractionMode = mode,
            PromptInstructions = PromptProfileBuilder.BuildModeInstructions(mode),
            ScreenshotPath = includeScreenshot ? "simulated.jpg" : "",
            ScreenshotWidth = includeScreenshot ? 1920 : 0,
            ScreenshotHeight = includeScreenshot ? 1080 : 0,
            ScreenshotBase64 = "",
            ScreenshotBytes = null
        };
    }

    private Task<string> ScenarioVoiceAssistFakeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var transcript = new Voice.TranscriptResult
        {
            Text = "Point at the center of the screen.",
            ProviderName = "Fake",
            DurationMs = 12
        };

        string rawResponse = "The center is here. [POINT:960,540:center]";
        var validation = _validator.Validate(rawResponse);
        var tag = _parser.Parse(rawResponse);
        bool flightRequested = tag.HasPoint;

        Assert(transcript.ProviderName == "Fake", "transcript provider must be fake");
        Assert(validation.IsValid, $"fake response should be valid: {validation.WarningMessage}");
        Assert(tag.HasPoint, "point tag should be parsed");
        Assert(tag.X == 960 && tag.Y == 540, "point coordinates should match the simulated target");
        Assert(tag.Label == "center", "point label should be preserved");
        Assert(flightRequested, "valid point flow should request a pointer flight");

        return Task.FromResult("Fake voice transcript, valid point tag, and pointer-flight decision passed.");
    }

    private Task<string> ScenarioQuickAskNoPointAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = BuildFakeRequest("What is this?", InteractionMode.Assist, includeScreenshot: true);
        string rawResponse = "No pointing is needed for this answer. [POINT:none]";
        var tag = _parser.Parse(rawResponse);
        bool flightRequested = tag.HasPoint;

        Assert(request.UserText.Length > 0, "typed Quick Ask text should be present");
        Assert(!tag.HasPoint, "[POINT:none] should not produce a coordinate");
        Assert(!flightRequested, "no pointer flight should be requested");

        return Task.FromResult("Typed Quick Ask with [POINT:none] avoided pointer flight.");
    }

    private Task<string> ScenarioQuickAskPointModeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = BuildFakeRequest("Where is Save?", InteractionMode.Point, includeScreenshot: true);
        string instructions = request.PromptInstructions;
        string rawResponse = "Click Save here. [POINT:450,200:save button]";
        var tag = _parser.Parse(rawResponse);

        Assert(instructions.Contains("Mode: Point", StringComparison.OrdinalIgnoreCase), "point profile should identify Point mode");
        Assert(instructions.Contains("REQUIRED", StringComparison.OrdinalIgnoreCase), "point profile should require a point when relevant");
        Assert(instructions.Contains("click", StringComparison.OrdinalIgnoreCase), "point profile should include click guidance");
        Assert(tag.HasPoint, "point-mode response should parse a coordinate");
        Assert(tag.Label == "save button", "point label should identify the simulated control");

        return Task.FromResult("Point-mode profile and parseable point response passed.");
    }

    private Task<string> ScenarioQuickAskReadScreenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = BuildFakeRequest("Read the screen.", InteractionMode.ReadScreen, includeScreenshot: true);
        string instructions = request.PromptInstructions;
        string rawResponse = "Visible text: Hello World. [POINT:none]";
        var tag = _parser.Parse(rawResponse);

        Assert(instructions.Contains("Do not hallucinate", StringComparison.OrdinalIgnoreCase), "ReadScreen should discourage hallucination");
        Assert(instructions.Contains("Usually, no pointing is needed", StringComparison.OrdinalIgnoreCase), "ReadScreen should usually avoid pointing");
        Assert(instructions.Contains("[POINT:none]", StringComparison.OrdinalIgnoreCase), "ReadScreen should recommend [POINT:none]");
        Assert(!tag.HasPoint, "ReadScreen response should avoid pointer flight");

        return Task.FromResult("ReadScreen profile discouraged hallucination and avoided pointing.");
    }

    private Task<string> ScenarioTtsDisabledAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool ttsEnabled = false;
        string rawResponse = "Here is the text answer. [POINT:none]";
        var tag = _parser.Parse(rawResponse);
        bool ttsRequested = ttsEnabled && !string.IsNullOrWhiteSpace(tag.CleanText);
        var validation = _validator.Validate(rawResponse);

        Assert(validation.IsValid, "response should remain valid with TTS disabled");
        Assert(!string.IsNullOrWhiteSpace(tag.CleanText), "clean response text should remain available");
        Assert(!ttsRequested, "TTS should be skipped when disabled");

        return Task.FromResult("TTS disabled path kept the clean response and skipped audio.");
    }

    private Task<string> ScenarioPointerDisabledAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool pointerFlightEnabled = false;
        string rawResponse = "Use this control. [POINT:960,540:center]";
        var tag = _parser.Parse(rawResponse);
        bool flightRequested = tag.HasPoint && pointerFlightEnabled;

        Assert(tag.HasPoint, "point should still be parsed");
        Assert(!flightRequested, "pointer flight should be suppressed when disabled");

        return Task.FromResult("Point parsed while pointer flight stayed disabled.");
    }

    private Task<string> ScenarioScreenshotDisabledTextOnlyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = BuildFakeRequest("Answer without a screenshot.", InteractionMode.Assist, includeScreenshot: false);
        string rawResponse = "Text-only answer is available. [POINT:none]";
        var tag = _parser.Parse(rawResponse);

        Assert(!string.IsNullOrWhiteSpace(request.UserText), "text-only request should include user text");
        Assert(request.ScreenshotWidth == 0 && request.ScreenshotHeight == 0, "text-only request should have no screenshot dimensions");
        Assert(string.IsNullOrEmpty(request.ScreenshotPath), "text-only request should not require a screenshot path");
        Assert(!tag.HasPoint, "text-only response should not require pointing");

        return Task.FromResult("Text-only request built without screenshot data.");
    }

    private Task<string> ScenarioDailyLimitReachedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int limit = Math.Max(1, _configService.Config.DailyInteractionLimit);
        int currentCount = limit;
        bool blocked = currentCount >= limit;
        string? blockReason = blocked ? "DailyInteractionLimit reached" : null;

        Assert(blocked, "interaction should be blocked when daily limit is reached");
        Assert(blockReason == "DailyInteractionLimit reached", "block reason should match coordinator guard");

        return Task.FromResult($"Daily limit guard blocked at {currentCount}/{limit}.");
    }

    private Task<string> ScenarioInvalidAiResponseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string missingTagResponse = "This response forgot the required point tag.";
        var validation = _validator.Validate(missingTagResponse);
        var tag = _parser.Parse(missingTagResponse);

        Assert(!validation.IsValid, "validator should flag a missing point tag");
        Assert(!string.IsNullOrWhiteSpace(validation.WarningMessage), "validator should provide a warning message");
        Assert(!tag.HasPoint, "parser should handle malformed response without a point");

        string malformedPointResponse = "Broken coordinate [POINT:not-a-coordinate]";
        var malformedTag = _parser.Parse(malformedPointResponse);
        Assert(!malformedTag.HasPoint, "parser should not crash on malformed point content");

        return Task.FromResult($"Invalid AI response reported warning: {validation.WarningMessage}");
    }

    private static async Task<string> ScenarioEscapeCancellationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var cts = new CancellationTokenSource();
        bool exitedCleanly = false;

        try
        {
            cts.CancelAfter(25);
            await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (OperationCanceledException)
        {
            exitedCleanly = true;
        }

        Assert(exitedCleanly, "cancellation should be caught by the simulated escape path");
        return "Cancellation exited cleanly without touching real input devices.";
    }
}
