using System;

namespace PointyPal.Infrastructure;

/// <summary>
/// Central repository for user-facing text and error messages to ensure consistency.
/// </summary>
public static class UserMessages
{
    // --- Standard Terminology ---
    public const string AppName = "PointyPal";
    public const string NormalMode = "Normal Mode";
    public const string DeveloperMode = "Developer Mode";
    public const string SafeMode = "Safe Mode";
    public const string WorkerConnection = "Worker Connection";
    public const string VoiceInput = "Voice Input";
    public const string VoiceOutput = "Voice Output";
    public const string ScreenContext = "Screen Context";
    public const string Pointer = "Pointer";
    public const string SetupWizard = "Setup Wizard";
    public const string ControlCenter = "Control Center";

    // --- Status Labels ---
    public const string StatusReady = "Ready";
    public const string StatusSetupRequired = "Setup Required";
    public const string StatusDegraded = "Degraded";
    public const string StatusUnreachable = "Unreachable";
    public const string StatusChecking = "Checking...";
    public const string StatusError = "Error";

    // --- Error Messages ---
    public const string ErrorWorkerUrlMissing = "PointyPal could not reach your Worker. Check Worker URL and internet connection in Control Center -> Connection.";
    public const string ErrorWorkerKeyMissing = "Worker authentication failed. Please check your Client Key in Control Center -> Connection.";
    public const string ErrorWorkerBaseUrlMissing = "PointyPal could not reach your Worker. Check Worker URL and internet connection in Control Center -> Connection.";
    public const string ErrorWorkerClientKeyMissing = "Worker authentication failed. Please check your Client Key in Control Center -> Connection.";
    public const string ErrorWorkerUnreachable = "PointyPal could not connect to your Worker. Please check your internet connection.";
    public const string ErrorUnauthorized = "Access to Worker was denied. Please verify your Client Key in Control Center -> Connection.";
    public const string ErrorModelNotAllowed = "The requested AI model is not allowed by your Worker policy.";
    public const string ErrorSttFailed = "Voice transcription failed. This might be due to noise or a connection issue.";
    public const string ErrorTtsFailed = "Voice output failed. Check your ElevenLabs configuration on the Worker.";
    public const string ErrorAiRequestFailed = "AI request failed. Please try again or check Worker logs.";
    public const string ErrorMicUnavailable = "Microphone is unavailable or not detected. Check Windows Sound settings.";
    public const string ErrorRecordingTooShort = "Voice command was too short. Please hold the key a bit longer while speaking.";
    public const string ErrorScreenshotDisabled = "Screen context is disabled. Enable it in Control Center -> Visual.";
    public const string ErrorScreenCaptureFailed = "Failed to capture screen. Another app may be blocking capture.";
    public const string ErrorUiAutomationUnavailable = "UI Automation is currently unavailable. Screen context may be limited.";
    public const string ErrorSafeModeActive = "PointyPal is in Safe Mode (Recovery). Real AI features are disabled.";
    public const string ErrorDailyLimitReached = "Daily interaction limit reached. You can adjust limits in Control Center -> Limits.";

    // --- Hints ---
    public const string HintCheckWorkerConnection = "Check Worker URL and internet connection in Control Center -> Connection.";
    public const string HintCheckConnectionSettings = "Check Worker URL and internet connection in Control Center -> Connection.";
    public const string HintCheckWorkerKey = "Enter your POINTYPAL_CLIENT_KEY.";
    public const string HintCheckSoundSettings = "Check Windows sound settings.";
    public const string HintHowToGetWorker = "See the documentation for how to deploy your own Worker.";
}
