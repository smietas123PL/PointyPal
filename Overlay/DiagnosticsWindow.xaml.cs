using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using PointyPal.AI;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using PointyPal.Core;
using System.Text.Json;
using Point = System.Windows.Point;

namespace PointyPal.Overlay;

public partial class DiagnosticsWindow : Window
{
    private ConfigService? _configService;
    private UsageTracker? _usageTracker;
    private DebugArtifactCleanupService? _cleanupService;
    private ProviderHealthCheckService? _healthService;
    private SelfTestReportService? _selfTestReportService;
    private InteractionTimelineService? _timelineService;
    private PerformanceSummaryService? _performanceSummaryService;
    private AppLifecycleService? _lifecycleService;
    private StartupRegistrationService? _startupRegistrationService;
    private AppLogService? _appLogService;
    private CrashLogger? _crashLogger;
    private SingleInstanceService? _singleInstanceService;
    private ResilienceMonitorService? _resilienceMonitor;
    private System.Windows.Threading.DispatcherTimer _refreshTimer;

    public DiagnosticsWindow()
    {
        InitializeComponent();
        Left = 10;
        Top = 10;

        _refreshTimer = new System.Windows.Threading.DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(2);
        _refreshTimer.Tick += (s, e) => UpdateUsageAndFolderInfo();
        _refreshTimer.Start();
    }

    public void SetServices(
        ConfigService config,
        UsageTracker usage,
        DebugArtifactCleanupService cleanup,
        ProviderHealthCheckService health,
        SelfTestReportService selfTestReportService,
        InteractionTimelineService timelineService,
        PerformanceSummaryService performanceSummaryService,
        AppLifecycleService? lifecycleService = null,
        StartupRegistrationService? startupRegistrationService = null,
        AppLogService? appLogService = null,
        CrashLogger? crashLogger = null,
        SingleInstanceService? singleInstanceService = null,
        ResilienceMonitorService? resilienceMonitor = null)
    {
        _configService = config;
        _usageTracker = usage;
        _cleanupService = cleanup;
        _healthService = health;
        _selfTestReportService = selfTestReportService;
        _timelineService = timelineService;
        _performanceSummaryService = performanceSummaryService;
        _lifecycleService = lifecycleService;
        _startupRegistrationService = startupRegistrationService;
        _appLogService = appLogService;
        _crashLogger = crashLogger;
        _singleInstanceService = singleInstanceService;
        _resilienceMonitor = resilienceMonitor;
        UpdateUsageAndFolderInfo();
    }

    private void UpdateUsageAndFolderInfo()
    {
        if (_configService == null || _usageTracker == null || _cleanupService == null || _healthService == null) return;

        var config = _configService.Config;
        var usage = _usageTracker.CurrentUsage;

        // Build 011
        bool ccOpen = false;
        foreach (Window w in System.Windows.Application.Current.Windows)
        {
            if (w is UI.ControlCenterWindow && w.IsVisible) { ccOpen = true; break; }
        }
        CcOpenText.Text = ccOpen ? "Yes" : "No";
        LastSaveText.Text = _configService.LastSaveTime?.ToString("HH:mm:ss") ?? "-";
        FirstRunText.Text = _configService.IsFirstRun ? "Pending" : "Completed";
        DiagWorkerStatusText.Text = _healthService.WorkerStatus;
        DiagHealthErrorText.Text = _healthService.LastErrorMessage ?? "-";

        UiEnabledText.Text = config.UiAutomationEnabled ? "Enabled" : "Disabled";
        AppVersionText.Text = AppInfo.DisplayText;
        AppChannelText.Text = AppInfo.BuildChannel;
        AppReleaseText.Text = string.IsNullOrWhiteSpace(AppInfo.ReleaseLabel) ? "-" : AppInfo.ReleaseLabel;
        WorkerContractText.Text = AppInfo.WorkerContractVersion;
        UptimeText.Text = _lifecycleService == null ? "-" : FormatUptime(_lifecycleService.Uptime);
        PrimaryInstanceText.Text = _singleInstanceService?.IsPrimaryInstance == true ? "Yes" : "No";
        SecondInstanceCountText.Text = (_singleInstanceService?.SecondInstanceDetectedCount ?? 0).ToString();
        StartWithWindowsText.Text = config.StartWithWindows || (_startupRegistrationService?.IsRegistered() == true) ? "Yes" : "No";
        CrashLoggingText.Text = config.CrashLoggingEnabled ? "Yes" : "No";
        AppLoggingText.Text = config.AppLoggingEnabled ? "Yes" : "No";
        LogsFolderText.Text = _appLogService?.LogDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs");
        LatestCrashPathText.Text = string.IsNullOrWhiteSpace(_crashLogger?.LatestCrashPath) ? "-" : Path.GetFileName(_crashLogger.LatestCrashPath);
        LastShutdownReasonText.Text = _lifecycleService?.ShutdownReason ?? "-";
        CurrentLogLevelText.Text = config.LogLevel;
        TimelineLoggingEnabledText.Text = config.EnableTimelineLogging ? "Yes" : "No";
        ActiveTimelineIdText.Text = _timelineService?.ActiveTimelineId ?? "-";
        ActiveTimelineStepText.Text = _timelineService?.CurrentActiveStep ?? "-";
        LatestTimelinePathText.Text = _timelineService == null ? "-" : Path.GetFileName(_timelineService.LatestTimelinePath);
        PerformanceSummaryPathText.Text = _performanceSummaryService == null ? "-" : Path.GetFileName(_performanceSummaryService.SummaryPath);

        var summary = _performanceSummaryService?.LastSummary;
        if (summary != null)
        {
            LastSlowestStepText.Text = string.IsNullOrWhiteSpace(summary.SlowestStepName) ? "-" : summary.SlowestStepName;
            P50TotalText.Text = $"{summary.P50TotalDurationMs:F0}ms";
            P95TotalText.Text = $"{summary.P95TotalDurationMs:F0}ms";
        }

        PrivacySaveDebugText.Text = config.SaveDebugArtifacts ? "Yes" : "No";
        PrivacyRedactText.Text = config.RedactDebugPayloads ? "Yes" : "No";

        UsageInteractionsText.Text = usage.InteractionsCount.ToString();
        LimitInteractionsText.Text = config.DailyInteractionLimit.ToString();

        UsageClaudeText.Text = usage.ClaudeRequestsCount.ToString();
        LimitClaudeText.Text = config.DailyClaudeRequestLimit.ToString();

        UsageSttText.Text = Math.Round(usage.SttSeconds).ToString();
        LimitSttText.Text = config.DailySttSecondsLimit.ToString();

        UsageTtsText.Text = usage.TtsCharacters.ToString();
        LimitTtsText.Text = config.DailyTtsCharLimit.ToString();

        var folderInfo = _cleanupService.GetFolderInfo();
        DebugFolderCountText.Text = $"{folderInfo.FileCount} files";
        DebugFolderSizeText.Text = FormatSize(folderInfo.TotalSize);

        UpdateSelfTestInfo();
        UpdateRcPrepInfo();
        UpdateResilienceInfo();
        UpdateRcReadinessInfo();
    }

    private void UpdateRcReadinessInfo()
    {
        if (_configService == null) return;
        string debug = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug");
        string reportPath = Path.Combine(debug, "rc-readiness-report.json");
        
        if (File.Exists(reportPath))
        {
            try
            {
                string json = File.ReadAllText(reportPath);
                var result = JsonSerializer.Deserialize<RcReadinessResult>(json);
                if (result != null)
                {
                    RcReadinessStatusText.Text = result.Status.ToString();
                    RcReadinessScoreText.Text = result.Score.ToString();
                    RcReadinessTimeText.Text = result.Timestamp.ToString("HH:mm:ss");
                }
            }
            catch { }
        }
    }

    private void UpdateResilienceInfo()
    {
        if (_resilienceMonitor == null) return;
 
        var snapshot = _resilienceMonitor.GetCurrentSnapshot();
        ResStatusText.Text = snapshot.Status.ToString();
        ResFailuresText.Text = snapshot.ConsecutiveProviderFailures.ToString();
        ResFallbackText.Text = _resilienceMonitor.FallbackActive ? "Yes (Fake)" : "No";
        ResMemoryText.Text = $"{snapshot.ProcessWorkingSetMb:F0}MB";
        ResCpuText.Text = $"{snapshot.CpuUsagePercent:F1}%";
        ResHandlesText.Text = snapshot.HandleCount.ToString();
        ResThreadsText.Text = snapshot.ThreadCount.ToString();
        ResGdiUserText.Text = $"{snapshot.GdiObjectCount}/{snapshot.UserObjectCount}";
        ResDisplayCntText.Text = snapshot.DisplayCount.ToString();
        ResWarningText.Text = snapshot.LastResourceWarningAt.HasValue ? "YES" : "No";
    }

    private void UpdateRcPrepInfo()
    {
        if (_configService == null) return;

        SafeModeActiveText.Text = _configService.SafeModeActive ? "YES" : "No";
        SafeModeReasonText.Text = string.IsNullOrWhiteSpace(_configService.SafeModeReason) ? "-" : _configService.SafeModeReason;

        if (_configService.SafeModeActive)
        {
            SafeModeActiveText.Foreground = System.Windows.Media.Brushes.Red;
            SafeModeActiveText.FontWeight = FontWeights.Bold;
        }
        else
        {
            SafeModeActiveText.Foreground = System.Windows.Media.Brushes.White;
            SafeModeActiveText.FontWeight = FontWeights.Normal;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string statePath = Path.Combine(appData, "PointyPal", "state", "startup-state.json");
        if (File.Exists(statePath))
        {
            try
            {
                string json = File.ReadAllText(statePath);
                var state = JsonSerializer.Deserialize<StartupCrashLoopState>(json);
                if (state != null)
                {
                    FailedStartupsText.Text = state.ConsecutiveFailedStartups.ToString();
                    CrashLoopSafeModeText.Text = state.SafeModeAutoTriggered ? "YES" : "No";
                }
            }
            catch { }
        }

        var backupService = new ConfigBackupService(_configService, _appLogService);
        BackupCountText.Text = backupService.GetBackupCount().ToString();
        LatestBackupPathText.Text = Path.GetFileName(backupService.GetLatestBackupPath()) ?? "-";

        string debug = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "debug");
        string reportPath = Path.Combine(debug, "preflight-report.json");
        PreflightReportPathText.Text = File.Exists(reportPath) ? "preflight-report.json" : "-";
        
        CommandLineArgsText.Text = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
    }

    private void UpdateSelfTestInfo()
    {
        if (_selfTestReportService == null)
        {
            return;
        }

        var result = _selfTestReportService.LastResult;
        var failedScenario = result?.ScenarioResults.FirstOrDefault(s => !s.Passed);

        SelfTestStatusText.Text = _selfTestReportService.IsRunning
            ? "Running"
            : result == null
                ? "-"
                : result.Passed ? "Passed" : "Failed";
        SelfTestModeText.Text = _selfTestReportService.LastMode?.ToString() ?? "-";
        SelfTestDurationText.Text = result == null ? "-" : $"{result.DurationMs}ms";
        SelfTestFailedScenarioText.Text = failedScenario == null
            ? "-"
            : $"{failedScenario.ScenarioName}: {failedScenario.ErrorMessage ?? failedScenario.AssertionsSummary}";
        SelfTestReportPathText.Text = string.IsNullOrWhiteSpace(_selfTestReportService.LastReportPath)
            ? "-"
            : _selfTestReportService.LastReportPath;
        SelfTestRunningText.Text = _selfTestReportService.IsRunning ? "Yes" : "No";
        SelfTestLastErrorText.Text = string.IsNullOrWhiteSpace(_selfTestReportService.LastError)
            ? "-"
            : _selfTestReportService.LastError;
    }

    private string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F1}{units[unitIndex]}";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | 
            NativeMethods.WS_EX_TRANSPARENT | 
            NativeMethods.WS_EX_TOOLWINDOW | 
            NativeMethods.WS_EX_NOACTIVATE);
    }

    public void UpdateData(
        double cx, double cy, 
        double ax, double ay, 
        Rect bounds, 
        string state,
        CaptureResult? capture,
        PointTag? parsedTag,
        Point? mappedScreenTarget,
        string stateReason)
    {
        CursorXText.Text = Math.Round(cx).ToString();
        CursorYText.Text = Math.Round(cy).ToString();
        AvatarXText.Text = Math.Round(ax).ToString();
        AvatarYText.Text = Math.Round(ay).ToString();
        MonitorBoundsText.Text = $"{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}";
        StateText.Text = state;
        ReasonText.Text = stateReason;

        if (capture != null)
        {
            CaptureSizeText.Text = $"{capture.Image.Width}x{capture.Image.Height}";
            CursorImgXText.Text = Math.Round(capture.CursorImagePosition.X).ToString();
            CursorImgYText.Text = Math.Round(capture.CursorImagePosition.Y).ToString();
            
            // PT011
            MonitorDpiText.Text = $"{capture.Geometry.DpiScaleX:F2} ({capture.Geometry.DpiScaleX*96:F0} DPI)";
            MonitorBoundsDipText.Text = $"{capture.Geometry.MonitorBoundsDip.Width:F0}x{capture.Geometry.MonitorBoundsDip.Height:F0}";
        }

        if (parsedTag != null && parsedTag.HasPoint)
        {
            TargetImgXText.Text = Math.Round(parsedTag.X).ToString();
            TargetImgYText.Text = Math.Round(parsedTag.Y).ToString();
        }
    }

    public void UpdateInteractionData(InteractionDiagnostics diag)
    {
        LastTimelineTtsText.Text = $"{diag.LastTimelineTtsDurationMs:F0}ms";
        LastTimelineUiaText.Text = $"{diag.LastTimelineUiAutomationDurationMs:F0}ms";
        LastTimelineScreenshotText.Text = $"{diag.LastScreenshotCaptureDurationMs:F0}ms";
        TimelineLoggingEnabledText.Text = diag.TimelineLoggingEnabled ? "Yes" : "No";
        LatestTimelinePathText.Text = string.IsNullOrWhiteSpace(diag.LatestTimelinePath) ? "-" : Path.GetFileName(diag.LatestTimelinePath);
        PerformanceSummaryPathText.Text = string.IsNullOrWhiteSpace(diag.PerformanceSummaryPath) ? "-" : Path.GetFileName(diag.PerformanceSummaryPath);
        P50TotalText.Text = $"{diag.P50TotalDurationMs:F0}ms";
        P95TotalText.Text = $"{diag.P95TotalDurationMs:F0}ms";
        TimelineReasonText.Text = string.IsNullOrWhiteSpace(diag.LastTimelineErrorOrCancellationReason)
            ? "-"
            : diag.LastTimelineErrorOrCancellationReason;
        
        GuardBlockedText.Text = diag.GuardBlocked ? "YES" : "No";
        BlockReasonText.Text = diag.BlockReason ?? "-";
        CancelReasonText.Text = diag.CancellationReason ?? (diag.TokenCancelled ? "Cancelled" : "-");

        TranscriptText.Text = diag.LastUserInput;
        TransDurText.Text = $"{diag.TranscriptDurationMs}ms";
        TransErrText.Text = diag.TranscriptError ?? "-";
        
        TtsEnabledText.Text = diag.TtsEnabled ? "Yes" : "No";
        TtsProviderText.Text = diag.TtsProvider;
        TtsDurationText.Text = $"{diag.TtsDurationMs}ms";
        TtsErrText.Text = diag.TtsError ?? "-";
        PlaybackText.Text = diag.PlaybackActive ? "Yes" : "No";
        
        // Build 012 UI Automation
        UiEnabledText.Text = diag.UiAutomationEnabled ? "Enabled" : "Disabled";
        ActiveWinText.Text = string.IsNullOrEmpty(diag.ActiveWindowTitle) ? "-" : diag.ActiveWindowTitle;
        CursorElemText.Text = string.IsNullOrEmpty(diag.ElementUnderCursor) ? "-" : diag.ElementUnderCursor;
        FocusElemText.Text = string.IsNullOrEmpty(diag.FocusedElement) ? "-" : diag.FocusedElement;
        NearbyCountText.Text = diag.NearbyElementCount.ToString();
        UiCaptureDurText.Text = $"{diag.UiCollectionDurationMs:F1}ms";
        UiErrorText.Text = string.IsNullOrEmpty(diag.UiAutomationError) ? "-" : diag.UiAutomationError;

        // Build 013 Point Accuracy
        MappedScreenTargetText.Text = diag.MappedScreenX.HasValue ? $"{Math.Round(diag.MappedScreenX.Value)},{Math.Round(diag.MappedScreenY!.Value)}" : "-";
        FinalScreenTargetText.Text = diag.FinalPointScreenX.HasValue ? $"{Math.Round(diag.FinalPointScreenX.Value)},{Math.Round(diag.FinalPointScreenY!.Value)}" : "-";
        SnappedText.Text = diag.PointSnapped ? "Yes" : "No";
        AdjustReasonText.Text = diag.AdjustmentReason ?? "-";
        NearestUiaText.Text = diag.NearestUiElement ?? "-";
        UiaDistanceText.Text = diag.DistanceToNearestUiElement.HasValue ? $"{Math.Round(diag.DistanceToNearestUiElement.Value)}px" : "-";
        ResponseValidText.Text = diag.ResponseValid ? "Yes" : "No";
        ResponseValidText.Foreground = diag.ResponseValid ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Orange;
        ValWarningText.Text = diag.ResponseValidationWarning ?? "-";
        ManualRatingText.Text = diag.LastManualRating.HasValue ? diag.LastManualRating.Value.ToString() : "-";
        LatestAttemptPathText.Text = string.IsNullOrEmpty(diag.LatestPointingAttemptPath) ? "-" : Path.GetFileName(diag.LatestPointingAttemptPath);

        // PT011 Accuracy fields
        if (diag.LastAttempt?.Target != null)
        {
            var target = diag.LastAttempt.Target;
            TargetDipText.Text = $"{target.FinalOverlayDipPoint.X:F0}, {target.FinalOverlayDipPoint.Y:F0}";
            MonitorDeviceText.Text = target.MonitorDeviceName ?? "-";
            ClampedImgText.Text = $"{target.ClampedImagePoint.X:F0}, {target.ClampedImagePoint.Y:F0}";
        }

        // Build 014
        DefaultModeText.Text = _configService?.Config.DefaultInteractionMode.ToString() ?? "-";
        LastModeText.Text = diag.InteractionMode;
        QuickAskOpenText.Text = diag.QuickAskOpen ? "Yes" : "No";
        LastQuickAskText.Text = string.IsNullOrEmpty(diag.LastQuickAskText) ? "-" : diag.LastQuickAskText;
        HistoryEnabledText.Text = diag.HistoryEnabled ? "Yes" : "No";
        HistoryFileText.Text = string.IsNullOrEmpty(diag.HistoryFilePath) ? "-" : Path.GetFileName(diag.HistoryFilePath);
        HistoryCountText.Text = diag.HistoryItemCount.ToString();
        LastCleanRespText.Text = string.IsNullOrEmpty(diag.CleanResponse) ? "-" : (diag.CleanResponse.Length > 30 ? diag.CleanResponse.Substring(0, 27) + "..." : diag.CleanResponse);
        LastProviderText.Text = diag.ActiveProvider;

        UpdateUsageAndFolderInfo();
    }

    public void UpdatePointerQuality(PointerQualityStats stats)
    {
        AccuracyScoreText.Text = $"{stats.OverallScore:F1}%";
        FeedbackSampleText.Text = stats.SampleSize.ToString();
        CcwPercentText.Text = $"{stats.CorrectPercentage:F0}/{stats.ClosePercentage:F0}/{stats.WrongPercentage:F0}";
    }

    public void UpdateVoiceData(bool micAvail, bool recording, string recPath, double durationMs)
    {
        MicAvailText.Text = micAvail ? "Yes" : "No";
        RecordingText.Text = recording ? "Yes" : "No";
        RecDurationText.Text = $"{Math.Round(durationMs)}ms";
    }

    public void Toggle()
    {
        if (Visibility == Visibility.Visible) Hide();
        else Show();
    }
}
