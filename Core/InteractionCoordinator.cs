using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PointyPal.AI;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using PointyPal.Voice;
using System.Linq;
using Point = System.Windows.Point;

namespace PointyPal.Core;

public enum ProviderOverride { None, ForceFake, ForceClaude }

public class InteractionOptions
{
    public InteractionMode InteractionMode { get; set; } = InteractionMode.Assist;
    public InteractionSource InteractionSource { get; set; } = InteractionSource.Hotkey;
    public bool ScreenshotEnabled { get; set; } = true;
    public bool UiAutomationEnabled { get; set; } = true;
    public bool TtsEnabled { get; set; } = true;
    public bool PointerFlightEnabled { get; set; } = true;
    public bool UseDefaultConfig { get; set; } = false;
}

public class LastResponseData
{
    public string LastResponseText { get; set; } = "";
    public string LastRawResponse { get; set; } = "";
    public string LastCleanResponse { get; set; } = "";
    public InteractionMode LastMode { get; set; }
    public string LastProvider { get; set; } = "";
    public string LastPointLabel { get; set; } = "";
    public string? RequestId { get; set; }
    public DateTime LastTimestamp { get; set; }
}

public class InteractionDiagnostics
{
    public string LastUserInput { get; set; } = "";
    public string RawResponse { get; set; } = "";
    public string CleanResponse { get; set; } = "";
    public string PayloadPath { get; set; } = "";
    public PointTag? ParsedTag { get; set; }
    public long DurationMs { get; set; }
    public string? LastError { get; set; }
    
    public string ActiveProvider { get; set; } = "";
    public string WorkerUrl { get; set; } = "";
    public bool PointClamped { get; set; }
    public string LatestRequestPath { get; set; } = "";
    public string LatestResponsePath { get; set; } = "";
    
    public string TranscriptProvider { get; set; } = "";
    public string TranscriptLanguage { get; set; } = "";
    public string LatestTranscriptRequestPath { get; set; } = "";
    public string LatestTranscriptResponsePath { get; set; } = "";
    public double TranscriptDurationMs { get; set; }
    public string? TranscriptError { get; set; }
    public string RecordingPath { get; set; } = "";
    public long AudioFileSize { get; set; }
    public bool IsFakeTranscript { get; set; }

    // TTS fields
    public bool TtsEnabled { get; set; }
    public string TtsProvider { get; set; } = "";
    public string TtsVoiceId { get; set; } = "";
    public string TtsModelId { get; set; } = "";
    public string LatestTtsRequestPath { get; set; } = "";
    public string LatestTtsResponsePath { get; set; } = "";
    public string LatestTtsAudioPath { get; set; } = "";
    public int TtsTextLength { get; set; }
    public double TtsDurationMs { get; set; }
    public string? TtsError { get; set; }
    public bool PlaybackActive { get; set; }

    // Build010: Guard fields
    public bool GuardBlocked { get; set; }
    public string? BlockReason { get; set; }
    public string CancellationReason { get; set; } = "";
    public bool TokenCancelled { get; set; }

    // UI Automation fields
    public bool UiAutomationEnabled { get; set; }
    public bool IncludeUiAutomationInPrompt { get; set; }
    public string ActiveWindowTitle { get; set; } = "";
    public string ElementUnderCursor { get; set; } = "";
    public string FocusedElement { get; set; } = "";
    public int NearbyElementCount { get; set; }
    public string LatestUiContextPath { get; set; } = "";
    public string UiAutomationError { get; set; } = "";
    public double UiCollectionDurationMs { get; set; }

    // Build013: Point Accuracy fields
    public double? ParsedPointImageX { get; set; }
    public double? ParsedPointImageY { get; set; }
    public double? MappedScreenX { get; set; }
    public double? MappedScreenY { get; set; }
    public double? FinalPointScreenX { get; set; }
    public double? FinalPointScreenY { get; set; }
    public bool PointSnapped { get; set; }
    public string? AdjustmentReason { get; set; }
    public string? NearestUiElement { get; set; }
    public double? DistanceToNearestUiElement { get; set; }
    public bool ResponseValid { get; set; } = true;
    public string? ResponseValidationWarning { get; set; }
    public int? LastManualRating { get; set; }
    public string LatestPointingAttemptPath { get; set; } = "";

    // Build014: Interactions & History
    public string InteractionMode { get; set; } = "";
    public string LastQuickAskText { get; set; } = "";
    public bool HistoryEnabled { get; set; }
    public string HistoryFilePath { get; set; } = "";
    public int HistoryItemCount { get; set; }
    public bool QuickAskOpen { get; set; }

    // Build016: Interaction timeline and performance
    public double LastInteractionTotalDurationMs { get; set; }
    public string ActiveTimelineId { get; set; } = "";
    public string CurrentActiveStep { get; set; } = "";
    public string LastSlowestStep { get; set; } = "";
    public double LastSttDurationMs { get; set; }
    public double LastClaudeDurationMs { get; set; }
    public double LastTimelineTtsDurationMs { get; set; }
    public double LastTimelineUiAutomationDurationMs { get; set; }
    public double LastScreenshotCaptureDurationMs { get; set; }
    public bool TimelineLoggingEnabled { get; set; }
    public string LatestTimelinePath { get; set; } = "";
    public string PerformanceSummaryPath { get; set; } = "";
    public double P50TotalDurationMs { get; set; }
    public double P95TotalDurationMs { get; set; }
    public string LastTimelineErrorOrCancellationReason { get; set; } = "";
    public long ScreenshotByteSize { get; set; }
    public string? RequestId { get; set; }
}

public class InteractionCoordinator : IDisposable
{
    private readonly AppStateManager _stateManager;
    private readonly ScreenCaptureService _captureService;
    private readonly CoordinateMapper _mapper;
    private readonly PointTagParser _parser;
    private readonly PromptPayloadBuilder _payloadBuilder;
    private readonly UiAutomationContextService _uiAutomationService;
    private readonly IAiResponseProvider _fakeProvider;
    private readonly IAiResponseProvider _claudeProvider;
    private readonly ConfigService _configService;
    private readonly PointValidationService _validationService;
    private readonly AiResponseValidator _responseValidator;
    private readonly ITtsProvider _fakeTtsProvider;
    private readonly ITtsProvider _workerTtsProvider;
    private readonly Voice.AudioPlaybackService _audioService;
    private readonly UsageTracker _usageTracker;
    private readonly DebugLogger _debugLogger;
    private readonly InteractionHistoryService _historyService;
    private readonly InteractionTimelineService _timelineService;
    private readonly PerformanceSummaryService _performanceSummaryService;
    private readonly ResilienceMonitorService? _resilienceMonitor;
    private readonly AppLogService? _appLog;
    private readonly ProviderPolicyService _providerPolicy;

    private PointingAttempt? _lastAttempt;
    private int? _lastRating;
    private CancellationTokenSource? _cts;
    private LastResponseData _lastResponse = new();
    private string _lastCancellationReason = "";

    public LastResponseData LastResponse => _lastResponse;

    public event Action<string>? ResponseBubbleRequested;
    public event Action<Point, TimeSpan>? FlightRequested;
    public event Action<InteractionDiagnostics>? DiagnosticsUpdated;

    public InteractionCoordinator(
        AppStateManager stateManager,
        ScreenCaptureService captureService,
        CoordinateMapper mapper,
        PointTagParser parser,
        PromptPayloadBuilder payloadBuilder,
        UiAutomationContextService uiAutomationService,
        IAiResponseProvider fakeProvider,
        IAiResponseProvider claudeProvider,
        ConfigService configService,
        PointValidationService validationService,
        AiResponseValidator responseValidator,
        ITtsProvider fakeTtsProvider,
        ITtsProvider workerTtsProvider,
        Voice.AudioPlaybackService audioService,
        UsageTracker usageTracker,
        DebugLogger debugLogger,
        InteractionHistoryService historyService,
        InteractionTimelineService timelineService,
        PerformanceSummaryService performanceSummaryService,
        ResilienceMonitorService? resilienceMonitor = null,
        AppLogService? appLog = null,
        ProviderPolicyService? providerPolicy = null)
    {
        _stateManager = stateManager;
        _captureService = captureService;
        _mapper = mapper;
        _parser = parser;
        _payloadBuilder = payloadBuilder;
        _uiAutomationService = uiAutomationService;
        _fakeProvider = fakeProvider;
        _claudeProvider = claudeProvider;
        _configService = configService;
        _validationService = validationService;
        _responseValidator = responseValidator;
        _fakeTtsProvider = fakeTtsProvider;
        _workerTtsProvider = workerTtsProvider;
        _audioService = audioService;
        _usageTracker = usageTracker;
        _debugLogger = debugLogger;
        _historyService = historyService;
        _timelineService = timelineService;
        _performanceSummaryService = performanceSummaryService;
        _resilienceMonitor = resilienceMonitor;
        _appLog = appLog;
        _providerPolicy = providerPolicy ?? new ProviderPolicyService(configService);

        _stateManager.StateChanged += (s, e) =>
        {
            if (e.State == CompanionState.FollowingCursor && _cts != null && !_cts.IsCancellationRequested)
            {
                _lastCancellationReason = "State changed to following cursor";
                _cts.Cancel();
                _audioService.Stop();
            }
        };
    }

    public async Task RunVoiceInteractionAsync(Voice.TranscriptResult transcript, CancellationToken token)
    {
        var config = _configService.Config;
        if (_timelineService.ActiveTimeline == null)
        {
            _timelineService.StartTimeline(InteractionSource.Voice, config.VoiceInteractionMode, transcript.ProviderName);
            var transcriptionStep = _timelineService.StartStep(
                InteractionTimelineStepNames.TranscriptionRequest,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["Provider"] = transcript.ProviderName,
                    ["ProviderDurationMs"] = Math.Round(transcript.DurationMs).ToString(),
                    ["TranscriptLength"] = transcript.Text.Length.ToString()
                });
            if (string.IsNullOrEmpty(transcript.ErrorMessage))
            {
                _timelineService.CompleteStep(transcriptionStep);
            }
            else
            {
                _timelineService.FailStep(transcriptionStep, transcript.ErrorMessage);
            }
        }

        var diag = new InteractionDiagnostics { 
            LastUserInput = transcript.Text,
            TranscriptProvider = transcript.ProviderName,
            TranscriptLanguage = config.TranscriptionLanguage,
            TranscriptDurationMs = transcript.DurationMs,
            TranscriptError = transcript.ErrorMessage,
            RecordingPath = transcript.AudioFilePath,
            IsFakeTranscript = transcript.ProviderName == "Fake"
        };
        
        if (File.Exists(transcript.AudioFilePath))
        {
            diag.AudioFileSize = new FileInfo(transcript.AudioFilePath).Length;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string debugDir = Path.Combine(appData, "PointyPal", "debug");
        diag.LatestTranscriptRequestPath = Path.Combine(debugDir, "latest-transcript-request.json");
        diag.LatestTranscriptResponsePath = Path.Combine(debugDir, "latest-transcript-response.json");

        var options = new InteractionOptions
        {
            InteractionMode = config.VoiceInteractionMode,
            InteractionSource = InteractionSource.Voice
        };
        await StartInteractionAsync(transcript.Text, ProviderOverride.None, diag, options);
    }

    public async Task StartInteractionAsync(string userText, ProviderOverride providerOverride = ProviderOverride.None, InteractionDiagnostics? existingDiag = null, InteractionOptions? options = null)
    {
        _lastRating = null;
        var config = _configService.Config;
        
        // Resolve options
        var interactionMode = options?.InteractionMode ?? config.DefaultInteractionMode;
        var interactionSource = options?.InteractionSource ?? InteractionSource.Hotkey;
        bool screenshotEnabled = options?.ScreenshotEnabled ?? config.ScreenshotEnabled;
        bool uiAutomationEnabled = options?.UiAutomationEnabled ?? config.UiAutomationEnabled;
        bool ttsEnabled = options?.TtsEnabled ?? config.TtsEnabled;
        bool pointerFlightEnabled = options?.PointerFlightEnabled ?? config.PointerFlightEnabled;
        int screenshotMaxWidth = config.ScreenshotMaxWidth > 0 ? config.ScreenshotMaxWidth : config.MaxImageWidth;
        int screenshotJpegQuality = config.ScreenshotJpegQuality > 0 ? config.ScreenshotJpegQuality : config.JpegQuality;
        
        if (options == null && providerOverride == ProviderOverride.None)
        {
            // Default F12 path
            interactionMode = config.DefaultInteractionMode;
        }

        // Cancel any existing interaction if configured
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            if (config.NewInteractionBehavior == "IgnoreNew")
            {
                return;
            }
            _lastCancellationReason = "Cancelled by new interaction";
            _cts.Cancel();
            _audioService.Stop();
            await _timelineService.CompleteTimelineAsync(wasCancelled: true, errorMessage: _lastCancellationReason);
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _lastCancellationReason = "";

        bool reusingVoiceTimeline =
            interactionSource == InteractionSource.Voice &&
            _timelineService.ActiveTimeline?.InteractionSource == InteractionSource.Voice;

        if (reusingVoiceTimeline)
        {
            _timelineService.SetInteractionMode(interactionMode);
        }
        else
        {
            _timelineService.StartTimeline(interactionSource, interactionMode);
        }
        string timelineId = _timelineService.ActiveTimelineId;

        var diag = existingDiag ?? new InteractionDiagnostics { LastUserInput = userText };
        diag.LastManualRating = _lastRating;
        diag.CancellationReason = "";
        diag.InteractionMode = interactionMode.ToString();
        diag.HistoryEnabled = config.SaveInteractionHistory;
        diag.HistoryFilePath = _historyService.GetHistoryPath();
        diag.HistoryItemCount = _historyService.GetItemCount();
        diag.QuickAskOpen = interactionSource == InteractionSource.QuickAsk;
        if (options != null) diag.LastQuickAskText = userText;
        ApplyTimelineDiagnostics(diag);
        _appLog?.Info(
            "InteractionStart",
            $"InteractionId={timelineId}; Source={interactionSource}; Mode={interactionMode}; TextLength={userText?.Length ?? 0}; Screenshot={screenshotEnabled}; UIA={uiAutomationEnabled}; TTS={ttsEnabled}; PointerFlight={pointerFlightEnabled}");

        var sw = Stopwatch.StartNew();
        bool timelineCancelled = false;
        string? timelineError = null;

        try
        {
            // 0. Cost guards
            if (_usageTracker.CurrentUsage.InteractionsCount >= config.DailyInteractionLimit)
            {
                diag.GuardBlocked = true;
                diag.BlockReason = "DailyInteractionLimit reached";
                timelineError = diag.BlockReason;
                _stateManager.SetState(CompanionState.Error, "Limit reached");
                System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("Dzisiejszy limit interakcji został osiągnięty."));
                return;
            }

            // 1. Determine provider
            IAiResponseProvider provider;
            string effectiveAiProvider = _providerPolicy.GetEffectiveAiProvider();

            if (providerOverride == ProviderOverride.ForceFake && !_providerPolicy.CanUseFakeProviders())
            {
                await BlockInteractionAsync(
                    diag,
                    ProviderPolicyService.FakeProvidersDeveloperOnlyMessage,
                    "Fake provider blocked");
                timelineError = ProviderPolicyService.FakeProvidersDeveloperOnlyMessage;
                return;
            }

            if (providerOverride == ProviderOverride.ForceClaude && !_providerPolicy.CanUseRealProviders())
            {
                await BlockInteractionAsync(
                    diag,
                    ProviderPolicyService.SafeModeBannerMessage,
                    "Real provider blocked");
                timelineError = ProviderPolicyService.SafeModeBannerMessage;
                return;
            }

            if (providerOverride == ProviderOverride.ForceClaude)
            {
                effectiveAiProvider = "Claude";
            }
            else if (providerOverride == ProviderOverride.ForceFake)
            {
                effectiveAiProvider = "Fake";
            }

            if (effectiveAiProvider == "Claude")
            {
                var validation = _providerPolicy.ValidateRealProviderConfiguration();
                if (!validation.IsValid)
                {
                    await BlockInteractionAsync(diag, validation.UserMessage, "Worker setup required");
                    timelineError = validation.UserMessage;
                    return;
                }
            }

            if (effectiveAiProvider == "Claude" && _resilienceMonitor?.FallbackActive == true)
            {
                if (_providerPolicy.CanFallbackToFake())
                {
                    effectiveAiProvider = "Fake";
                    _appLog?.Warning("Resilience", "Falling back to Fake AI provider due to Worker failures.");
                }
                else
                {
                    _appLog?.Warning("Resilience", ProviderPolicyService.WorkerUnavailableFakeFallbackDisabledMessage);
                    _resilienceMonitor.RecordEvent(
                        "ProviderFallback",
                        "Warning",
                        ProviderPolicyService.WorkerUnavailableFakeFallbackDisabledMessage);
                }
            }

            provider = effectiveAiProvider == "Fake" ? _fakeProvider : _claudeProvider;

            _stateManager.SetState(CompanionState.Processing, "LocalInteractionStarted");
            _usageTracker.IncrementInteractions();

            if (provider is ClaudeVisionResponseProvider)
            {
                if (_usageTracker.CurrentUsage.ClaudeRequestsCount >= config.DailyClaudeRequestLimit)
                {
                    diag.GuardBlocked = true;
                    diag.BlockReason = "DailyClaudeRequestLimit reached";
                    timelineError = diag.BlockReason;
                    _stateManager.SetState(CompanionState.Error, "AI Limit reached");
                    System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("Dzisiejszy limit zapytań AI został osiągnięty."));
                    return;
                }
                _usageTracker.IncrementClaudeRequests();
            }

            diag.ActiveProvider = effectiveAiProvider;
            diag.WorkerUrl = config.WorkerBaseUrl;
            _timelineService.SetProviderName(diag.ActiveProvider);

            // 2. Capture screen (only if enabled)
            CaptureResult? capture = null;
            if (screenshotEnabled)
            {
                bool saveToDisk = config.SaveDebugArtifacts && config.SaveScreenshots;
                var screenshotStep = _timelineService.StartStep(
                    InteractionTimelineStepNames.ScreenshotCapture,
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["MaxWidth"] = screenshotMaxWidth.ToString(),
                        ["JpegQuality"] = screenshotJpegQuality.ToString(),
                        ["SaveToDisk"] = saveToDisk.ToString()
                    });

                try
                {
                    capture = await Task.Run(() => _captureService.CaptureCurrentCursorMonitor(screenshotMaxWidth, screenshotJpegQuality, saveToDisk), token);
                    var metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["Width"] = (capture?.Image.Width ?? 0).ToString(),
                        ["Height"] = (capture?.Image.Height ?? 0).ToString(),
                        ["OriginalWidth"] = (capture?.OriginalWidth ?? 0).ToString(),
                        ["OriginalHeight"] = (capture?.OriginalHeight ?? 0).ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(capture?.ImagePath) && File.Exists(capture.ImagePath))
                    {
                        long bytes = new FileInfo(capture.ImagePath).Length;
                        metadata["ByteSize"] = bytes.ToString();
                        diag.ScreenshotByteSize = bytes;
                    }

                    _timelineService.CompleteStep(screenshotStep, metadata);
                    diag.LastScreenshotCaptureDurationMs = screenshotStep?.DurationMs ?? 0;
                }
                catch (Exception ex)
                {
                    _timelineService.FailStep(screenshotStep, ex.Message);
                    throw;
                }
            }

            if (capture == null && screenshotEnabled) throw new Exception("Failed to capture screen.");
            
            NativeMethods.GetCursorPos(out var pt);
            var cursorPos = new Point(pt.X, pt.Y);
            var bounds = ScreenUtilities.GetMonitorBounds(cursorPos);

            // 3. Prepare AI Request
            var aiRequest = new AiRequest
            {
                UserText = userText,
                ScreenshotPath = capture?.ImagePath ?? "",
                ScreenshotWidth = capture?.Image.Width ?? 0,
                ScreenshotHeight = capture?.Image.Height ?? 0,
                MonitorBounds = bounds,
                CursorScreenPosition = cursorPos,
                CursorImagePosition = capture?.CursorImagePosition ?? new Point(0, 0),
                PromptInstructions = ClaudePromptBuilder.BuildInstructions(interactionMode),
                InteractionMode = interactionMode,
                ScreenshotMimeType = "image/jpeg"
            };

            // 3b. Collect UI Automation Context
            if (uiAutomationEnabled)
            {
                var uiStep = _timelineService.StartStep(
                    InteractionTimelineStepNames.UiAutomationCapture,
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["TimeoutMs"] = config.UiAutomationTimeoutMs.ToString()
                    });

                aiRequest.UiAutomationContext = await _uiAutomationService.CaptureContextAsync(cursorPos, token);
                
                diag.UiAutomationEnabled = uiAutomationEnabled;
                diag.IncludeUiAutomationInPrompt = config.IncludeUiAutomationInPrompt;
                diag.ActiveWindowTitle = aiRequest.UiAutomationContext.ActiveWindowTitle ?? "";
                diag.ElementUnderCursor = aiRequest.UiAutomationContext.ElementUnderCursor?.Name ?? aiRequest.UiAutomationContext.ElementUnderCursor?.ControlType ?? "";
                diag.FocusedElement = aiRequest.UiAutomationContext.FocusedElement?.Name ?? aiRequest.UiAutomationContext.FocusedElement?.ControlType ?? "";
                diag.NearbyElementCount = aiRequest.UiAutomationContext.NearbyElements?.Count ?? 0;
                diag.UiCollectionDurationMs = aiRequest.UiAutomationContext.CollectionDurationMs;
                diag.UiAutomationError = aiRequest.UiAutomationContext.ErrorMessage ?? "";
                diag.LastTimelineUiAutomationDurationMs = aiRequest.UiAutomationContext.CollectionDurationMs;

                var uiMetadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["IsAvailable"] = aiRequest.UiAutomationContext.IsAvailable.ToString(),
                    ["NearbyElementCount"] = diag.NearbyElementCount.ToString(),
                    ["CollectionDurationMs"] = Math.Round(aiRequest.UiAutomationContext.CollectionDurationMs).ToString()
                };

                if (aiRequest.UiAutomationContext.IsAvailable)
                {
                    _timelineService.CompleteStep(uiStep, uiMetadata);
                }
                else
                {
                    _timelineService.FailStep(uiStep, aiRequest.UiAutomationContext.ErrorMessage, uiMetadata);
                }
                
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                diag.LatestUiContextPath = Path.Combine(appDataPath, "PointyPal", "debug", "latest-ui-context.json");
            }
            else
            {
                diag.UiAutomationEnabled = false;
            }

            var payloadStep = _timelineService.StartStep(InteractionTimelineStepNames.PromptPayloadBuild);
            try
            {
                // Read bytes for base64
                if (capture != null)
                {
                    if (File.Exists(capture.ImagePath))
                    {
                        aiRequest.ScreenshotBytes = await File.ReadAllBytesAsync(capture.ImagePath, token);
                    }
                    else
                    {
                        aiRequest.ScreenshotBytes = capture.GetJpegBytes(screenshotJpegQuality);
                    }

                    aiRequest.ScreenshotBase64 = Convert.ToBase64String(aiRequest.ScreenshotBytes);
                    diag.ScreenshotByteSize = aiRequest.ScreenshotBytes.Length;
                    _timelineService.AddStepMetadata(payloadStep, "ScreenshotBytes", aiRequest.ScreenshotBytes.Length.ToString());
                }

                // Save request for debug (redacted)
                var redactedRequest = new {
                    aiRequest.UserText,
                    aiRequest.ScreenshotPath,
                    aiRequest.ScreenshotMimeType,
                    aiRequest.ScreenshotWidth,
                    aiRequest.ScreenshotHeight,
                    aiRequest.MonitorBounds,
                    aiRequest.CursorScreenPosition,
                    aiRequest.CursorImagePosition,
                    aiRequest.Timestamp,
                    aiRequest.PromptInstructions,
                    aiRequest.ModelOverride,
                    ScreenshotBase64Length = aiRequest.ScreenshotBase64?.Length ?? 0,
                    UiAutomationContext = aiRequest.UiAutomationContext != null ? new {
                        aiRequest.UiAutomationContext.IsAvailable,
                        aiRequest.UiAutomationContext.ActiveWindowTitle,
                        NearbyElementsCount = aiRequest.UiAutomationContext.NearbyElements?.Count ?? 0,
                        aiRequest.UiAutomationContext.ErrorMessage
                    } : null
                };
                diag.LatestRequestPath = _debugLogger.SaveDebugJson("latest-ai-request.json", redactedRequest);
                
                if (capture != null)
                {
                    diag.PayloadPath = await _payloadBuilder.BuildAndSavePayloadAsync(userText, capture, bounds, cursorPos, aiRequest.UiAutomationContext, interactionMode);
                }

                _timelineService.CompleteStep(payloadStep, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["HasScreenshot"] = (capture != null).ToString(),
                    ["ScreenshotBase64Length"] = (aiRequest.ScreenshotBase64?.Length ?? 0).ToString(),
                    ["UiContextIncluded"] = (aiRequest.UiAutomationContext != null).ToString()
                });
            }
            catch (Exception ex)
            {
                _timelineService.FailStep(payloadStep, ex.Message);
                throw;
            }
            
            token.ThrowIfCancellationRequested();

            // 4. Call AI
            var aiStep = _timelineService.StartStep(
                InteractionTimelineStepNames.ClaudeRequest,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["Provider"] = diag.ActiveProvider
                });
            
            AiResponse aiResponse;
            try
            {
                aiResponse = await provider.GetResponseAsync(aiRequest, token);
                if (string.IsNullOrEmpty(aiResponse.ErrorMessage))
                {
                    _resilienceMonitor?.RecordInteractionSuccess();
                }
                else
                {
                    _resilienceMonitor?.RecordProviderFailure("AI", aiResponse.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _resilienceMonitor?.RecordProviderFailure("AI", ex.Message);
                throw;
            }

            diag.RawResponse = aiResponse.RawText;
            diag.DurationMs = aiResponse.DurationMs;
            diag.LastError = aiResponse.ErrorMessage;
            diag.RequestId = aiResponse.RequestId;
            diag.LastClaudeDurationMs = aiStep?.DurationMs ?? aiResponse.DurationMs;
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
            var aiMetadata = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Provider"] = aiResponse.ProviderName,
                ["ProviderDurationMs"] = aiResponse.DurationMs.ToString(),
                ["RawTextLength"] = aiResponse.RawText.Length.ToString(),
                ["RequestId"] = aiResponse.RequestId ?? ""
            };
            if (string.IsNullOrEmpty(aiResponse.ErrorMessage))
            {
                _timelineService.CompleteStep(aiStep, aiMetadata);
                diag.LastClaudeDurationMs = aiStep?.DurationMs ?? aiResponse.DurationMs;
            }
            else
            {
                _timelineService.FailStep(aiStep, aiResponse.ErrorMessage, aiMetadata);
            }
            diag.LastClaudeDurationMs = aiStep?.DurationMs ?? aiResponse.DurationMs;
            
            // Save response for debug
            diag.LatestResponsePath = _debugLogger.SaveDebugJson("latest-ai-response.json", aiResponse);

            if (!string.IsNullOrEmpty(aiResponse.ErrorMessage))
            {
                _usageTracker.IncrementErrors();
                throw new Exception(aiResponse.ErrorMessage);
            }

            token.ThrowIfCancellationRequested();

            // 5. Parse and Validate response
            var parseStep = _timelineService.StartStep(InteractionTimelineStepNames.AiResponseParse);
            PointTag tag;
            string cleanText;
            try
            {
                var responseVal = _responseValidator.Validate(aiResponse.RawText);
                diag.ResponseValid = responseVal.IsValid;
                diag.ResponseValidationWarning = responseVal.WarningMessage;

                tag = _parser.Parse(aiResponse.RawText);
                
                // Clean text is raw text without the tag
                cleanText = tag.CleanText;
                diag.CleanResponse = cleanText;
                _timelineService.CompleteStep(parseStep, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["ResponseValid"] = responseVal.IsValid.ToString(),
                    ["HasPoint"] = tag.HasPoint.ToString(),
                    ["CleanTextLength"] = cleanText.Length.ToString()
                });
            }
            catch (Exception ex)
            {
                _timelineService.FailStep(parseStep, ex.Message);
                throw;
            }

            // Coordinate mapping & validation
            if (capture != null)
            {
                var validationStep = _timelineService.StartStep(InteractionTimelineStepNames.PointValidation);
                var initialMappedPoint = _mapper.MapImagePointToScreenPoint(new Point(tag.X, tag.Y), capture);
                
                var (attempt, validation) = _validationService.ProcessPoint(
                    tag, capture, aiRequest.UiAutomationContext, initialMappedPoint,
                    userText, diag.ActiveProvider, aiResponse.RawText, cleanText);

                _lastAttempt = attempt;
                diag.ParsedTag = tag;
                diag.PointClamped = attempt.WasPointClamped;
                diag.ParsedPointImageX = attempt.ParsedPointImageX;
                diag.ParsedPointImageY = attempt.ParsedPointImageY;
                diag.MappedScreenX = attempt.MappedScreenX;
                diag.MappedScreenY = attempt.MappedScreenY;
                diag.FinalPointScreenX = attempt.FinalPointScreenX;
                diag.FinalPointScreenY = attempt.FinalPointScreenY;
                diag.PointSnapped = !string.IsNullOrEmpty(attempt.AdjustmentReason) && 
                                    (attempt.AdjustmentReason.StartsWith("Snapped"));
                diag.AdjustmentReason = attempt.AdjustmentReason;
                diag.NearestUiElement = attempt.NearestUiElement;
                diag.DistanceToNearestUiElement = attempt.DistanceToNearestUiElement;

                // Save latest pointing attempt
                if (config.SaveDebugArtifacts && tag.HasPoint)
                {
                    diag.LatestPointingAttemptPath = _debugLogger.SaveDebugJson("latest-pointing-attempt.json", attempt);
                }

                _timelineService.CompleteStep(validationStep, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["HasPoint"] = tag.HasPoint.ToString(),
                    ["WasPointClamped"] = attempt.WasPointClamped.ToString(),
                    ["PointSnapped"] = diag.PointSnapped.ToString(),
                    ["AdjustmentReason"] = attempt.AdjustmentReason ?? ""
                });
            }

            diag.CleanResponse = cleanText;

            // 6. Show clean response bubble
            if (config.TextBubbleEnabled)
            {
                var bubbleStep = _timelineService.StartStep(InteractionTimelineStepNames.BubbleDisplay);
                System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke(cleanText));
                _timelineService.CompleteStep(bubbleStep, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["TextLength"] = cleanText.Length.ToString()
                });
            }

            // 7. TTS and pointer flight
            bool ttsLimitReached = _usageTracker.CurrentUsage.TtsCharacters >= config.DailyTtsCharLimit;
            bool skipShortNoPointTts =
                config.SkipTtsForShortNoPointResponses &&
                !tag.HasPoint &&
                cleanText.Trim().Length <= 80;
            bool shouldRequestTts = ttsEnabled && !string.IsNullOrWhiteSpace(cleanText) && !skipShortNoPointTts;
            bool shouldFly = tag.HasPoint && pointerFlightEnabled && capture != null;

            if (skipShortNoPointTts)
            {
                diag.TtsError = "TTS skipped for short no-point response";
            }

            if (shouldRequestTts && ttsLimitReached && config.DisableTtsWhenLimitReached)
            {
                Debug.WriteLine("TTS skipped due to daily limit");
                diag.TtsError = "TTS skipped due to daily limit";
                shouldRequestTts = false;
            }

            if (config.EnableParallelTtsAndPointerFlight && shouldRequestTts && shouldFly)
            {
                await Task.WhenAll(
                    RunTtsFlowAsync(cleanText, hasPoint: true, config, diag, token),
                    FlyToPointAsync(tag, capture!, token));
            }
            else
            {
                if (shouldRequestTts)
                {
                    await RunTtsFlowAsync(cleanText, tag.HasPoint, config, diag, token);
                }

                if (shouldFly)
                {
                    await FlyToPointAsync(tag, capture!, token);
                }
                else if ((!ttsEnabled || skipShortNoPointTts) && !tag.HasPoint)
                {
                    // Just show bubble, then return to following cursor after a delay.
                    await Task.Delay(3000, token);
                }
            }

            _stateManager.SetState(CompanionState.FollowingCursor, "Interaction completed");
            
            // Save to history
            _lastResponse = new LastResponseData
            {
                LastResponseText = cleanText,
                LastRawResponse = aiResponse.RawText,
                LastCleanResponse = cleanText,
                LastMode = interactionMode,
                LastProvider = diag.ActiveProvider,
                LastPointLabel = tag.Label,
                RequestId = aiResponse.RequestId,
                LastTimestamp = DateTime.Now
            };

            await _historyService.AddEntryAsync(new InteractionHistoryItem
            {
                Timestamp = _lastResponse.LastTimestamp,
                Mode = _lastResponse.LastMode.ToString(),
                UserText = userText,
                Provider = _lastResponse.LastProvider,
                CleanResponse = _lastResponse.LastCleanResponse,
                HadPoint = tag.HasPoint,
                PointLabel = tag.Label,
                DurationMs = diag.DurationMs,
                RequestId = aiResponse.RequestId
            });

            capture?.Dispose();
        }
        catch (OperationCanceledException)
        {
            diag.LastError = "Cancelled";
            diag.TokenCancelled = true;
            diag.CancellationReason = string.IsNullOrWhiteSpace(_lastCancellationReason)
                ? "Cancelled"
                : _lastCancellationReason;
            timelineCancelled = true;
            timelineError = diag.CancellationReason;
            if (_stateManager.CurrentState == CompanionState.Processing || _stateManager.CurrentState == CompanionState.Speaking)
            {
                _stateManager.SetState(CompanionState.FollowingCursor, "Interaction cancelled");
            }
        }
        catch (Exception ex)
        {
            _usageTracker.IncrementErrors();
            diag.LastError = ex.Message;
            timelineError = ex.Message;
            _appLog?.Error("ProviderError", $"InteractionId={timelineId}; Provider={diag.ActiveProvider}; Error={ex.Message}");
            _stateManager.SetState(CompanionState.Error, $"Error: {ex.Message}");
            
            System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke($"Provider error: {ex.Message}"));
            
            await Task.Delay(2000);
            if (_stateManager.CurrentState == CompanionState.Error)
            {
                _stateManager.SetState(CompanionState.FollowingCursor, "Recovered from error");
            }
        }
        finally
        {
            sw.Stop();
            diag.LastInteractionTotalDurationMs = sw.Elapsed.TotalMilliseconds;
            if (diag.DurationMs == 0) diag.DurationMs = sw.ElapsedMilliseconds;
            await _timelineService.CompleteTimelineAsync(timelineId, timelineCancelled, timelineError);
            var summary = _performanceSummaryService.RefreshSummary(_timelineService.LastTimeline);
            ApplyTimelineDiagnostics(diag, summary);
            _appLog?.Info(
                "InteractionEnd",
                $"InteractionId={timelineId}; DurationMs={Math.Round(diag.LastInteractionTotalDurationMs)}; Provider={diag.ActiveProvider}; Mode={diag.InteractionMode}; Cancelled={timelineCancelled}; Error={(string.IsNullOrWhiteSpace(timelineError) ? "None" : timelineError)}");
            DiagnosticsUpdated?.Invoke(diag);
        }
    }

    public async Task RunSelfTestAsync(SelfTestMode mode)
    {
        var harness = new InteractionSimulationHarness(_configService);
        var reportService = new SelfTestReportService(_configService);
        await reportService.RunAndSaveAsync(harness, mode);
    }

    public void ShowStatusMessage(string message, int milliseconds = 3000)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke(message));
        if (milliseconds > 0)
        {
            _ = Task.Delay(milliseconds).ContinueWith(_ =>
                System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("")));
        }
    }

    private async Task BlockInteractionAsync(InteractionDiagnostics diag, string message, string stateReason)
    {
        diag.GuardBlocked = true;
        diag.BlockReason = message;
        diag.LastError = message;
        _usageTracker.IncrementErrors();
        _stateManager.SetState(CompanionState.Error, stateReason);
        ShowStatusMessage(message);
        await Task.Delay(2000);
        if (_stateManager.CurrentState == CompanionState.Error)
        {
            _stateManager.SetState(CompanionState.FollowingCursor, stateReason);
        }
    }

    public void CancelCurrentInteraction(string reason = "Requested")
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _lastCancellationReason = reason;
            _timelineService.CancelActiveStep(reason);
            _cts.Cancel();
        }
        _audioService.Stop();
        System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke(""));
    }

    private void ApplyTimelineDiagnostics(InteractionDiagnostics diag, PerformanceSummary? summary = null)
    {
        try
        {
            summary ??= _performanceSummaryService.LastSummary;
            var timeline = _timelineService.LastTimeline;

            diag.ActiveTimelineId = _timelineService.ActiveTimelineId;
            diag.CurrentActiveStep = _timelineService.CurrentActiveStep;
            diag.TimelineLoggingEnabled = _configService.Config.EnableTimelineLogging;
            diag.LatestTimelinePath = _timelineService.LatestTimelinePath;
            diag.PerformanceSummaryPath = _performanceSummaryService.SummaryPath;
            diag.P50TotalDurationMs = summary.P50TotalDurationMs;
            diag.P95TotalDurationMs = summary.P95TotalDurationMs;
            diag.LastSlowestStep = summary.SlowestStepName;
            diag.LastTimelineErrorOrCancellationReason = summary.LastErrorOrCancellationReason;

            if (timeline != null)
            {
                diag.LastInteractionTotalDurationMs = timeline.TotalDurationMs > 0
                    ? timeline.TotalDurationMs
                    : (DateTime.Now - timeline.StartedAt).TotalMilliseconds;
                diag.LastSttDurationMs = GetTimelineStepDuration(timeline, InteractionTimelineStepNames.TranscriptionRequest);
                diag.LastClaudeDurationMs = GetTimelineStepDuration(timeline, InteractionTimelineStepNames.ClaudeRequest);
                diag.LastTimelineTtsDurationMs = GetTimelineStepDuration(timeline, InteractionTimelineStepNames.TtsRequest);
                diag.LastTimelineUiAutomationDurationMs = GetTimelineStepDuration(timeline, InteractionTimelineStepNames.UiAutomationCapture);
                diag.LastScreenshotCaptureDurationMs = GetTimelineStepDuration(timeline, InteractionTimelineStepNames.ScreenshotCapture);

                if (string.IsNullOrWhiteSpace(diag.LastTimelineErrorOrCancellationReason))
                {
                    diag.LastTimelineErrorOrCancellationReason = timeline.WasCancelled
                        ? timeline.ErrorMessage ?? "Cancelled"
                        : timeline.ErrorMessage ?? "";
                }
            }
            else
            {
                diag.LastInteractionTotalDurationMs = summary.LastTotalDurationMs;
                diag.LastSttDurationMs = summary.LastSttDurationMs;
                diag.LastClaudeDurationMs = summary.LastClaudeDurationMs;
                diag.LastTimelineTtsDurationMs = summary.LastTtsDurationMs;
                diag.LastTimelineUiAutomationDurationMs = summary.LastUiAutomationDurationMs;
                diag.LastScreenshotCaptureDurationMs = summary.LastScreenshotCaptureDurationMs;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Apply timeline diagnostics failed: {ex.Message}");
        }
    }

    private static double GetTimelineStepDuration(InteractionTimeline timeline, string stepName)
    {
        return timeline.Steps
            .Where(s => s.Name == stepName)
            .Select(s => s.DurationMs)
            .DefaultIfEmpty(0)
            .Last();
    }

    private async Task RunTtsFlowAsync(
        string cleanText,
        bool hasPoint,
        AppConfig config,
        InteractionDiagnostics diag,
        CancellationToken token)
    {
        _stateManager.SetState(CompanionState.Speaking, "TTS starting");
        diag.TtsEnabled = true;
        string effectiveTtsProvider = _providerPolicy.GetEffectiveTtsProvider();
        diag.TtsProvider = effectiveTtsProvider;
        diag.TtsVoiceId = config.ElevenLabsVoiceId;
        diag.TtsModelId = config.ElevenLabsModelId;

        if (effectiveTtsProvider == "Worker")
        {
            var validation = _providerPolicy.ValidateRealProviderConfiguration();
            if (!validation.IsValid)
            {
                diag.TtsError = validation.UserMessage;
                _appLog?.Warning("ProviderError", $"Provider=Worker; Operation=TTS; Error={validation.UserMessage}");
                ShowStatusMessage(validation.UserMessage);
                return;
            }
        }

        string ttsText = cleanText;
        if (ttsText.Length > config.MaxTtsChars)
        {
            ttsText = ttsText.Substring(0, config.MaxTtsChars);
        }

        diag.TtsTextLength = ttsText.Length;
        _usageTracker.AddTtsCharacters(ttsText.Length);

        var ttsRequest = new Voice.TtsRequest
        {
            Text = ttsText,
            VoiceId = config.ElevenLabsVoiceId,
            ModelId = config.ElevenLabsModelId,
            OutputFormat = config.ElevenLabsOutputFormat
        };

        ITtsProvider ttsProvider = effectiveTtsProvider == "Worker" ? _workerTtsProvider : _fakeTtsProvider;
        
        diag.LatestTtsRequestPath = _debugLogger.SaveDebugJson("latest-tts-request.json", ttsRequest);
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        diag.LatestTtsAudioPath = Path.Combine(appDataPath, "PointyPal", "debug", "latest-tts.mp3");

        var ttsStep = _timelineService.StartStep(
            InteractionTimelineStepNames.TtsRequest,
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["Provider"] = effectiveTtsProvider,
                ["TextLength"] = ttsText.Length.ToString()
            });

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (config.TtsRequestTimeoutSeconds > 0)
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TtsRequestTimeoutSeconds));
            }

            var ttsResult = await ttsProvider.GetSpeechAsync(ttsRequest, timeoutCts.Token);
            diag.TtsDurationMs = ttsResult.DurationMs;
            diag.LastTimelineTtsDurationMs = ttsStep?.DurationMs ?? ttsResult.DurationMs;
            diag.TtsError = ttsResult.ErrorMessage;
            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

            var metadata = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Provider"] = ttsResult.ProviderName,
                ["ProviderDurationMs"] = Math.Round(ttsResult.DurationMs).ToString(),
                ["Success"] = ttsResult.Success.ToString(),
                ["AudioAvailable"] = (!string.IsNullOrEmpty(ttsResult.AudioPath)).ToString()
            };

            if (ttsResult.Success)
            {
                _timelineService.CompleteStep(ttsStep, metadata);
            }
            else
            {
                _timelineService.FailStep(ttsStep, ttsResult.ErrorMessage, metadata);
            }

            diag.LastTimelineTtsDurationMs = ttsStep?.DurationMs ?? ttsResult.DurationMs;

            if (ttsResult.Success && !string.IsNullOrEmpty(ttsResult.AudioPath))
            {
                diag.PlaybackActive = true;
                DiagnosticsUpdated?.Invoke(diag);

                var playbackStep = _timelineService.StartStep(InteractionTimelineStepNames.AudioPlaybackStart);
                var playTask = _audioService.PlayAsync(ttsResult.AudioPath, token);
                _timelineService.CompleteStep(playbackStep, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["AudioAvailable"] = "True"
                });

                await playTask;
                diag.PlaybackActive = false;
            }
            else if (!hasPoint)
            {
                await Task.Delay(3000, token);
            }
        }
        catch (OperationCanceledException)
        {
            _timelineService.FailStep(ttsStep, "TTS cancelled");
            throw;
        }
        catch (Exception ex)
        {
            diag.TtsError = ex.Message;
            _appLog?.Warning("ProviderError", $"Provider={effectiveTtsProvider}; Operation=TTS; Error={ex.Message}");
            _timelineService.FailStep(ttsStep, ex.Message);
        }
    }

    private async Task FlyToPointAsync(PointTag tag, CaptureResult capture, CancellationToken token)
    {
        if (_lastAttempt == null) return;

        var screenPoint = new Point(_lastAttempt.FinalPointScreenX, _lastAttempt.FinalPointScreenY);
        var flightStep = _timelineService.StartStep(
            InteractionTimelineStepNames.PointerFlight,
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["TargetX"] = Math.Round(screenPoint.X).ToString(),
                ["TargetY"] = Math.Round(screenPoint.Y).ToString()
            });
        
        try
        {
            // Fly to target
            _stateManager.SetState(CompanionState.FlyingToTarget, "Interaction target mapped");
            System.Windows.Application.Current.Dispatcher.Invoke(() => FlightRequested?.Invoke(screenPoint, TimeSpan.FromSeconds(0.6)));
            
            // Wait for flight and pointing to complete
            await Task.Delay(3000, token);
            _timelineService.CompleteStep(flightStep);
        }
        catch (OperationCanceledException)
        {
            _timelineService.FailStep(flightStep, "Pointer flight cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _timelineService.FailStep(flightStep, ex.Message);
            throw;
        }
    }

    public async Task ReplayLastPointAsync()
    {
        if (_lastAttempt == null || _lastAttempt.ParsedPointImageX == null) return;

        var screenPoint = new Point(_lastAttempt.FinalPointScreenX, _lastAttempt.FinalPointScreenY);
        
        _stateManager.SetState(CompanionState.FlyingToTarget, "Replay last point");
        System.Windows.Application.Current.Dispatcher.Invoke(() => FlightRequested?.Invoke(screenPoint, TimeSpan.FromSeconds(0.6)));
        
        await Task.Delay(3000);
        _stateManager.SetState(CompanionState.FollowingCursor, "Replay completed");
    }

    public async Task RepeatLastTtsAsync()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string audioPath = Path.Combine(appData, "PointyPal", "debug", "latest-tts.mp3");

        if (File.Exists(audioPath))
        {
            _stateManager.SetState(CompanionState.Speaking, "Repeating last TTS");
            await _audioService.PlayAsync(audioPath, CancellationToken.None);
            _stateManager.SetState(CompanionState.FollowingCursor, "Repeat completed");
        }
        else if (!string.IsNullOrEmpty(_lastResponse.LastCleanResponse))
        {
            // Regenerate TTS if file is missing but response exists
            System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke(_lastResponse.LastCleanResponse));
        }
    }

    public void CopyLastResponseToClipboard()
    {
        if (!string.IsNullOrEmpty(_lastResponse.LastCleanResponse))
        {
            Clipboard.SetText(_lastResponse.LastCleanResponse);
            System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("Skopiowano do schowka."));
            Task.Delay(1500).ContinueWith(_ => System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("")));
        }
    }

    public void SubmitManualRating(int rating)
    {
        if (_lastAttempt == null) return;
        if (!_configService.Config.SaveDebugArtifacts) return;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string debugDir = Path.Combine(appData, "PointyPal", "debug");
        Directory.CreateDirectory(debugDir);
        string feedbackPath = Path.Combine(debugDir, "pointing-feedback.jsonl");

        var feedback = new
        {
            Timestamp = DateTime.Now,
            Rating = rating,
            Summary = new
            {
                _lastAttempt.UserText,
                _lastAttempt.ProviderName,
                _lastAttempt.ParsedPointImageX,
                _lastAttempt.ParsedPointImageY,
                _lastAttempt.FinalPointScreenX,
                _lastAttempt.FinalPointScreenY,
                _lastAttempt.AdjustmentReason
            }
        };

        string json = JsonSerializer.Serialize(feedback);
        File.AppendAllLines(feedbackPath, new[] { json });
        
        _lastRating = rating;
        _debugLogger.Log($"Manual rating submitted: {rating}");
        
        System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke($"Dziękuję! Ocena: {rating}"));
        Task.Delay(1500).ContinueWith(_ => System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("")));
    }

    public async Task PerformManualUiCaptureAsync()
    {
        try
        {
            NativeMethods.GetCursorPos(out var pt);
            var cursorPos = new Point(pt.X, pt.Y);
            
            var context = await _uiAutomationService.CaptureContextAsync(cursorPos);
            
            // Save to debug if allowed
            if (_configService.Config.SaveDebugArtifacts && _configService.Config.SaveUiAutomationDebug)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string debugDir = Path.Combine(appData, "PointyPal", "debug");
                Directory.CreateDirectory(debugDir);
                
                string uiContextPath = Path.Combine(debugDir, "latest-ui-context.json");
                var uiJson = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(uiContextPath, uiJson);
            }

            // Update diagnostics
            var diag = new InteractionDiagnostics
            {
                LastUserInput = "[Manual UI Capture]",
                DurationMs = (long)context.CollectionDurationMs,
                ActiveProvider = "UI Automation",
                UiAutomationEnabled = _configService.Config.UiAutomationEnabled,
                IncludeUiAutomationInPrompt = _configService.Config.IncludeUiAutomationInPrompt,
                ActiveWindowTitle = context.ActiveWindowTitle ?? "",
                ElementUnderCursor = context.ElementUnderCursor?.Name ?? context.ElementUnderCursor?.ControlType ?? "",
                FocusedElement = context.FocusedElement?.Name ?? context.FocusedElement?.ControlType ?? "",
                NearbyElementCount = context.NearbyElements?.Count ?? 0,
                UiCollectionDurationMs = context.CollectionDurationMs,
                UiAutomationError = context.ErrorMessage ?? ""
            };
            
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            diag.LatestUiContextPath = Path.Combine(appDataPath, "PointyPal", "debug", "latest-ui-context.json");
            
            DiagnosticsUpdated?.Invoke(diag);

            System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke("UI context captured."));
            await Task.Delay(2000);
            System.Windows.Application.Current.Dispatcher.Invoke(() => ResponseBubbleRequested?.Invoke(""));
        }
        catch (Exception ex)
        {
            _debugLogger.Log($"Manual UI capture failed: {ex}");
        }
    }

    public void Dispose()
    {
        try
        {
            if (_cts != null)
            {
                if (!_cts.IsCancellationRequested)
                {
                    _lastCancellationReason = "Application shutdown";
                    _cts.Cancel();
                }

                _cts.Dispose();
                _cts = null;
            }
        }
        catch
        {
            // Shutdown cleanup is best effort.
        }
    }
}
