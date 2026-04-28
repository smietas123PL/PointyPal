namespace PointyPal.Infrastructure;

public class AppConfig
{
    public string AiProvider { get; set; } = "Claude"; // "Fake" or "Claude"
    public string WorkerBaseUrl { get; set; } = "https://YOUR-WORKER.workers.dev";
    public string ClaudeModel { get; set; } = "claude-sonnet-4-5";
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int MaxImageWidth { get; set; } = 1280;
    public int JpegQuality { get; set; } = 70;
    public int ClaudeRequestTimeoutSeconds { get; set; } = 60;
    public int TranscriptRequestTimeoutSeconds { get; set; } = 60;
    public int TtsRequestTimeoutSeconds { get; set; } = 60;
    public int ScreenshotMaxWidth { get; set; } = 1280;
    public int ScreenshotJpegQuality { get; set; } = 70;
    public string WorkerClientKey { get; set; } = "";

    public bool VoiceEnabled { get; set; } = true;
    public string TranscriptProvider { get; set; } = "Worker";
    public string TranscriptionLanguage { get; set; } = "pl";
    public long MaxAudioUploadBytes { get; set; } = 10485760; // 10MB
    public string FakeTranscriptText { get; set; } = "Co powinienem kliknąć na tym ekranie?";
    public int MinRecordingMs { get; set; } = 300;

    public bool TtsEnabled { get; set; } = false;
    public string TtsProvider { get; set; } = "Worker";
    public string ElevenLabsVoiceId { get; set; } = "";
    public string ElevenLabsModelId { get; set; } = "eleven_flash_v2_5";
    public string ElevenLabsOutputFormat { get; set; } = "mp3_44100_128";
    public int MaxTtsChars { get; set; } = 700;

    // Build010: Privacy settings
    public bool SaveDebugArtifacts { get; set; } = false;
    public bool AutoDeleteDebugArtifacts { get; set; } = false;
    public int DebugArtifactRetentionHours { get; set; } = 24;
    public bool SaveScreenshots { get; set; } = false;
    public bool SaveRecordings { get; set; } = false;
    public bool SaveTtsAudio { get; set; } = false;
    public bool RedactDebugPayloads { get; set; } = true;

    // Build010: Cost guards
    public int DailyInteractionLimit { get; set; } = 100;
    public int DailyTtsCharLimit { get; set; } = 10000;
    public int DailySttSecondsLimit { get; set; } = 1800; // 30 minutes
    public int DailyClaudeRequestLimit { get; set; } = 100;
    public int RequireTtsConfirmationAboveChars { get; set; } = 700;
    public bool DisableTtsWhenLimitReached { get; set; } = true;

    // Build010: Interaction mode toggles
    public bool ScreenshotEnabled { get; set; } = true;
    public bool VoiceInputEnabled { get; set; } = true;
    public bool TextBubbleEnabled { get; set; } = true;
    public bool PointerFlightEnabled { get; set; } = true;

    // Build010: New interaction behavior
    public string NewInteractionBehavior { get; set; } = "CancelPrevious"; // "CancelPrevious" or "IgnoreNew"

    // Build012: UI Automation settings
    public bool UiAutomationEnabled { get; set; } = true;
    public int UiAutomationRadiusPx { get; set; } = 500;
    public int MaxUiElementsInPrompt { get; set; } = 30;
    public bool IncludeUiAutomationInPrompt { get; set; } = true;
    public bool SaveUiAutomationDebug { get; set; } = false;

    // Build013: Pointing diagnostics & snapping
    public bool PointSnappingEnabled { get; set; } = false;
    public int PointSnappingMaxDistancePx { get; set; } = 80;
    public bool PointSnappingPreferButtons { get; set; } = true;
    public bool PointSnappingSnapToElementCenter { get; set; } = true;
    public string[] PointSnappingUsefulControlTypes { get; set; } = new[] 
    { 
        "Button", "Edit", "ComboBox", "MenuItem", "TabItem", "CheckBox", "RadioButton", "Hyperlink", "ListItem" 
    };

    public bool ShowCalibrationGrid { get; set; } = false;
    public bool ShowPointAccuracyDiagnostics { get; set; } = false;

    // PT011: Pointer Accuracy Polish
    public bool PointerMarkerEnabled { get; set; } = true;
    public int PointerMarkerDurationMs { get; set; } = 1400;
    public int PointerFlightDurationMs { get; set; } = 450;
    public int PointerReturnDurationMs { get; set; } = 350;
    public int PointerTargetOffsetPx { get; set; } = 18;
    public int PointerLabelMaxLength { get; set; } = 40;
    public bool PointerFeedbackPromptEnabled { get; set; } = false;
    public bool PointerFeedbackPromptDeveloperOnly { get; set; } = true;

    // Build014: Interaction modes
    public Core.InteractionMode DefaultInteractionMode { get; set; } = Core.InteractionMode.Assist;
    public Core.InteractionMode VoiceInteractionMode { get; set; } = Core.InteractionMode.Assist;
    public Core.InteractionMode QuickAskDefaultMode { get; set; } = Core.InteractionMode.Assist;

    // Build014: Interaction history
    public bool SaveInteractionHistory { get; set; } = false;
    public int InteractionHistoryRetentionDays { get; set; } = 7;
    public int MaxInteractionHistoryItems { get; set; } = 200;

    // Build016: timeline diagnostics and performance controls
    public bool EnableTimelineLogging { get; set; } = true;
    public bool SaveTimelineHistory { get; set; } = true;
    public int MaxTimelineHistoryItems { get; set; } = 200;
    public int UiAutomationTimeoutMs { get; set; } = 1000;
    public bool EnableParallelTtsAndPointerFlight { get; set; } = true;
    public bool SkipTtsForShortNoPointResponses { get; set; } = false;

    // Build017: lifecycle, startup, and local logging
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool CrashLoggingEnabled { get; set; } = true;
    public bool AppLoggingEnabled { get; set; } = true;
    public string LogLevel { get; set; } = "Info";
    public int LogRetentionDays { get; set; } = 7;

    // Build018: RC prep
    public bool ForceSafeMode { get; set; } = false;
    public int MaxConfigBackups { get; set; } = 10;

    // Build019: Resilience and hardening
    public bool EnableProviderFallback { get; set; } = false;
    public bool FallbackToFakeOnWorkerFailure { get; set; } = false;
    public int ProviderFailureThreshold { get; set; } = 3;
    public int ProviderFailureCooldownSeconds { get; set; } = 300;
    public bool AutoRefreshOverlayOnDisplayChange { get; set; } = true;
    public bool EnableResourceMonitoring { get; set; } = true;
    public int ResourceMonitoringIntervalSeconds { get; set; } = 60;
    public int MemoryWarningThresholdMb { get; set; } = 800;

    // Build020: Crash loop and resource alerts
    public bool CrashLoopDetectionEnabled { get; set; } = true;
    public int CrashLoopFailureThreshold { get; set; } = 3;
    public int CrashLoopWindowMinutes { get; set; } = 10;
    public bool AutoSafeModeOnCrashLoop { get; set; } = true;

    public int CpuWarningThresholdPercent { get; set; } = 80;
    public int GdiObjectWarningThreshold { get; set; } = 8000;
    public int UserObjectWarningThreshold { get; set; } = 8000;
    public int HandleWarningThreshold { get; set; } = 5000;
    public int ThreadWarningThreshold { get; set; } = 200;
    
    // Build021: Onboarding and RC polish
    public bool OnboardingCompleted { get; set; } = false;
    public bool ShowOnboardingOnStartup { get; set; } = true;
    public bool SetupWizardCompleted { get; set; } = false;
    public bool ShowSetupWizardOnStartup { get; set; } = true;


    // PT006: user modes and production UX simplification
    public bool DeveloperModeEnabled { get; set; } = false;
    public bool ShowDeveloperTrayItems { get; set; } = false;
    public bool EnableDeveloperHotkeys { get; set; } = false;
    public bool ShowAdvancedDiagnostics { get; set; } = false;
    public bool AllowFakeProvidersInDeveloperMode { get; set; } = true;
    public bool AllowFakeProviderFallbackInNormalMode { get; set; } = false;

    // PT013: Pointer Overlay v2
    public string PointerVisualStyle { get; set; } = "TriangleV2";
    public bool PointerAuraEnabled { get; set; } = true;
    public bool PointerStatusSlotEnabled { get; set; } = true;

    // PT013: Pointer Scale Hotfix
    public double PointerVisualSizeDip { get; set; } = 22;
    public double PointerVisualMinSizeDip { get; set; } = 18;
    public double PointerVisualMaxSizeDip { get; set; } = 36;
    public double PointerVisualGlowScale { get; set; } = 1.10;
    public double PointerStatusSlotScale { get; set; } = 1.15;
}
