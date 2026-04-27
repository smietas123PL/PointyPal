using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using PointyPal.Infrastructure;
using PointyPal.Core;

namespace PointyPal.UI;

public partial class ControlCenterWindow : Window
{
    private readonly ConfigService _configService;
    private readonly UsageTracker _usageTracker;
    private readonly ProviderHealthCheckService _healthService;
    private readonly SelfTestReportService _selfTestReportService;
    private readonly InteractionTimelineService _timelineService;
    private readonly PerformanceSummaryService _performanceSummaryService;
    private readonly StartupRegistrationService? _startupRegistrationService;
    private readonly DebugArtifactCleanupService? _cleanupService;
    private readonly AppLogService? _appLogService;
    private readonly CrashLogger? _crashLogger;
    private readonly AppLifecycleService? _lifecycleService;
    private readonly SingleInstanceService? _singleInstanceService;
    private readonly ResilienceMonitorService? _resilienceMonitor;
    private readonly StartupCrashLoopGuard? _crashLoopGuard;
    private readonly PreflightCheckService? _preflightService;
    private readonly RcReadinessService? _readinessService;
    private readonly RcValidationService? _validationService;
    private readonly ProviderPolicyService _providerPolicy;
    private readonly HotkeyPolicyService _hotkeyPolicy;
    private AppConfig _currentConfig;

    public ControlCenterWindow(ConfigService configService, UsageTracker usageTracker, ProviderHealthCheckService healthService)
        : this(
            configService,
            usageTracker,
            healthService,
            new SelfTestReportService(configService),
            new InteractionTimelineService(configService),
            new PerformanceSummaryService(configService))
    {
    }

    public ControlCenterWindow(
        ConfigService configService,
        UsageTracker usageTracker,
        ProviderHealthCheckService healthService,
        SelfTestReportService selfTestReportService,
        InteractionTimelineService timelineService,
        PerformanceSummaryService performanceSummaryService,
        StartupRegistrationService? startupRegistrationService = null,
        DebugArtifactCleanupService? cleanupService = null,
        AppLogService? appLogService = null,
        CrashLogger? crashLogger = null,
        AppLifecycleService? lifecycleService = null,
        SingleInstanceService? singleInstanceService = null,
        ResilienceMonitorService? resilienceMonitor = null,
        StartupCrashLoopGuard? crashLoopGuard = null,
        PreflightCheckService? preflightService = null,
        RcReadinessService? readinessService = null,
        RcValidationService? validationService = null)
    {
        InitializeComponent();
        _configService = configService;
        _usageTracker = usageTracker;
        _healthService = healthService;
        _selfTestReportService = selfTestReportService;
        _timelineService = timelineService;
        _performanceSummaryService = performanceSummaryService;
        _startupRegistrationService = startupRegistrationService;
        _cleanupService = cleanupService;
        _appLogService = appLogService;
        _crashLogger = crashLogger;
        _lifecycleService = lifecycleService;
        _singleInstanceService = singleInstanceService;
        _resilienceMonitor = resilienceMonitor;
        _crashLoopGuard = crashLoopGuard;
        _preflightService = preflightService;
        _readinessService = readinessService;
        _validationService = validationService;
        _providerPolicy = new ProviderPolicyService(_configService);
        _hotkeyPolicy = new HotkeyPolicyService(_configService);

        _currentConfig = CloneConfig(_configService.Config);
        DataContext = _currentConfig;
        WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;

        if (_configService.IsFirstRun)
        {
            FirstRunBanner.Visibility = Visibility.Visible;
        }

        UpdateUsageDisplay();
        UpdateHealthDisplay();
        UpdateSelfTestDisplay(_selfTestReportService.LastResult);
        UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
        UpdateLifecycleDisplay();
        UpdateLogDisplay();
        UpdateSafeModeVisibility();
        UpdateResilienceDisplay();

        UpdateOnboardingDisplay();
        UpdateHotkeysDisplay();
        UpdateStatusDisplay();
        UpdateModeVisibility();

        if (_configService.Config.ShowSetupWizardOnStartup && !_configService.Config.SetupWizardCompleted)
        {
            MainTabs.SelectedItem = GettingStartedTab;
            Dispatcher.BeginInvoke(new Action(() => RunWizardBtn_Click(this, new RoutedEventArgs())));
        }
        else if (_configService.Config.ShowOnboardingOnStartup && !_configService.Config.OnboardingCompleted)
        {
            MainTabs.SelectedItem = GettingStartedTab;
        }
    }

    private void UpdateSafeModeVisibility()
    {
        if (_configService.SafeModeActive)
        {
            SafeModeBanner.Visibility = Visibility.Visible;
            SafeModeReasonText.Text = ProviderPolicyService.SafeModeBannerMessage;
            SaveBtn.IsEnabled = true; // Allow fixing config in safe mode
        }
        else
        {
            SafeModeBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateModeVisibility()
    {
        var status = _providerPolicy.GetProviderStatusForUi();
        bool developerMode = _configService.Config.DeveloperModeEnabled;
        bool showAdvanced = developerMode || _configService.SafeModeActive;
        var advancedVisibility = showAdvanced ? Visibility.Visible : Visibility.Collapsed;

        DeveloperModeBanner.Visibility = developerMode && !_configService.SafeModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        ProviderSetupWarningText.Text = status.SetupWarning;
        ProviderSetupWarningText.Visibility = string.IsNullOrWhiteSpace(status.SetupWarning)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Hide Fake providers from Normal Mode
        AiFakeItem.Visibility = advancedVisibility;
        SttFakeItem.Visibility = advancedVisibility;
        TtsFakeItem.Visibility = advancedVisibility;

        AiProviderRow.Visibility = advancedVisibility;
        SttProviderRow.Visibility = advancedVisibility;
        TtsProviderRow.Visibility = advancedVisibility;
        FakeTranscriptRow.Visibility = advancedVisibility;
        DeveloperOptionsGroup.Visibility = advancedVisibility;
        OpenDebugBtn.Visibility = advancedVisibility;

        VoiceTab.Visibility = advancedVisibility;
        VisualTab.Visibility = advancedVisibility;
        LimitsTab.Visibility = advancedVisibility;
        HealthTab.Visibility = advancedVisibility;
        SelfTestTab.Visibility = advancedVisibility;
        PerformanceTab.Visibility = advancedVisibility;
        LogsTab.Visibility = advancedVisibility;
        UsageTab.Visibility = advancedVisibility;
        PointingTab.Visibility = advancedVisibility;
        InteractionsTab.Visibility = advancedVisibility;
        ResilienceTab.Visibility = advancedVisibility;
        ReleaseTab.Visibility = advancedVisibility;
        PreflightDetailsTab.Visibility = advancedVisibility;
        RecoveryTab.Visibility = advancedVisibility;

        if (!showAdvanced && MainTabs.SelectedItem is TabItem selected && selected.Visibility != Visibility.Visible)
        {
            MainTabs.SelectedItem = StatusTab;
        }
    }

    private AppConfig CloneConfig(AppConfig source)
    {
        // Simple clone for editing
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private void UpdateUsageDisplay()
    {
        var usage = _usageTracker.CurrentUsage;
        var config = _configService.Config;

        UsageInteractionsText.Text = $"{usage.InteractionsCount} / {config.DailyInteractionLimit}";
        UsageClaudeText.Text = $"{usage.ClaudeRequestsCount} / {config.DailyClaudeRequestLimit}";
        UsageSttText.Text = $"{Math.Round(usage.SttSeconds)} / {config.DailySttSecondsLimit}";
        UsageTtsText.Text = $"{usage.TtsCharacters} / {config.DailyTtsCharLimit}";
    }

    private void UpdateHealthDisplay()
    {
        WorkerStatusText.Text = _healthService.WorkerStatus;
        AiStatusText.Text = _healthService.AiStatus;
        SttStatusText.Text = _healthService.TranscriptStatus;
        TtsStatusText.Text = _healthService.TtsStatus;
        LastCheckTimeText.Text = _healthService.LastCheckTime?.ToString("HH:mm:ss") ?? "-";
        HealthErrorText.Text = _healthService.LastErrorMessage ?? "";

        // Colors
        WorkerStatusText.Foreground = GetStatusBrush(_healthService.WorkerStatus);
        AiStatusText.Foreground = GetStatusBrush(_healthService.AiStatus);
        SttStatusText.Foreground = GetStatusBrush(_healthService.TranscriptStatus);
        TtsStatusText.Foreground = GetStatusBrush(_healthService.TtsStatus);
    }

    private void UpdateSelfTestDisplay(SelfTestResult? result)
    {
        if (_selfTestReportService.IsRunning)
        {
            SelfTestStatusText.Text = $"Running {_selfTestReportService.LastMode?.ToString() ?? "self-test"}...";
            SelfTestStatusText.Foreground = Brushes.DarkOrange;
            SelfTestLastRunText.Text = "-";
            SelfTestDurationText.Text = "-";
            SelfTestScenarioCountText.Text = "-";
            SelfTestFailedScenarioText.Text = "-";
            SelfTestReportPathText.Text = string.IsNullOrWhiteSpace(_selfTestReportService.LastReportPath)
                ? "-"
                : _selfTestReportService.LastReportPath;
            return;
        }

        if (result == null)
        {
            SelfTestStatusText.Text = "Not run";
            SelfTestStatusText.Foreground = Brushes.Gray;
            SelfTestLastRunText.Text = "-";
            SelfTestDurationText.Text = "-";
            SelfTestScenarioCountText.Text = "-";
            SelfTestFailedScenarioText.Text = "-";
            SelfTestReportPathText.Text = "-";
            return;
        }

        var failedScenario = result.ScenarioResults.FirstOrDefault(s => !s.Passed);

        SelfTestStatusText.Text = result.Passed ? "Passed" : "Failed";
        SelfTestStatusText.Foreground = result.Passed ? Brushes.Green : Brushes.Red;
        SelfTestLastRunText.Text = result.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss");
        SelfTestDurationText.Text = $"{result.DurationMs}ms";
        SelfTestScenarioCountText.Text = $"{result.PassedScenarios} passed / {result.FailedScenarios} failed / {result.TotalScenarios} total";
        SelfTestFailedScenarioText.Text = failedScenario == null
            ? "-"
            : $"{failedScenario.ScenarioName}: {failedScenario.ErrorMessage ?? failedScenario.AssertionsSummary}";
        SelfTestReportPathText.Text = string.IsNullOrWhiteSpace(_selfTestReportService.LastReportPath)
            ? (_configService.Config.SaveDebugArtifacts ? _selfTestReportService.ReportPath : "In memory only")
            : _selfTestReportService.LastReportPath;
    }

    private void UpdatePerformanceDisplay(PerformanceSummary? summary = null)
    {
        summary ??= _performanceSummaryService.RefreshSummary(_timelineService.LastTimeline);
        var timeline = _timelineService.LastTimeline;

        LastTotalDurationText.Text = $"{(timeline?.TotalDurationMs > 0 ? timeline.TotalDurationMs : summary.LastTotalDurationMs):F0}ms";
        LastSttDurationText.Text = $"{GetStepDuration(timeline, InteractionTimelineStepNames.TranscriptionRequest, summary.LastSttDurationMs):F0}ms";
        LastClaudeDurationText.Text = $"{GetStepDuration(timeline, InteractionTimelineStepNames.ClaudeRequest, summary.LastClaudeDurationMs):F0}ms";
        LastTtsDurationText.Text = $"{GetStepDuration(timeline, InteractionTimelineStepNames.TtsRequest, summary.LastTtsDurationMs):F0}ms";
        LastUiaDurationText.Text = $"{GetStepDuration(timeline, InteractionTimelineStepNames.UiAutomationCapture, summary.LastUiAutomationDurationMs):F0}ms";
        LastScreenshotDurationText.Text = $"{GetStepDuration(timeline, InteractionTimelineStepNames.ScreenshotCapture, summary.LastScreenshotCaptureDurationMs):F0}ms";
        P50DurationText.Text = $"{summary.P50TotalDurationMs:F0}ms";
        P95DurationText.Text = $"{summary.P95TotalDurationMs:F0}ms";
        SlowestStepText.Text = string.IsNullOrWhiteSpace(summary.SlowestStepName) ? "-" : $"{summary.SlowestStepName} ({summary.SlowestStepDurationMs:F0}ms)";
        LastTimelineReasonText.Text = timeline?.WasCancelled == true
            ? timeline.ErrorMessage ?? "Cancelled"
            : timeline?.ErrorMessage ?? summary.LastErrorOrCancellationReason;
        if (string.IsNullOrWhiteSpace(LastTimelineReasonText.Text))
        {
            LastTimelineReasonText.Text = "-";
        }

        UpdatePreflightDisplay();
        UpdateRecoveryDisplay();
    }

    private void UpdatePreflightDisplay()
    {
        // Placeholder or load latest report if needed
        PreflightLastRunText.Text = "-";
    }
    private void UpdateRecoveryDisplay()
    {
        var backupService = new ConfigBackupService(_configService, _appLogService);
        BackupCountText.Text = backupService.GetBackupCount().ToString();
        LatestBackupText.Text = Path.GetFileName(backupService.GetLatestBackupPath()) ?? "None";
    }

    private void UpdateResilienceDisplay()
    {
        if (_resilienceMonitor == null) return;

        var snapshot = _resilienceMonitor.GetCurrentSnapshot();
        ResilienceStatusText.Text = snapshot.Status.ToString();
        ResilienceStatusText.Foreground = GetResilienceStatusBrush(snapshot.Status);
        ResilienceFailuresText.Text = snapshot.ConsecutiveProviderFailures.ToString();
        ResilienceFallbackText.Text = _resilienceMonitor.FallbackActive ? "Yes (Fake)" : "No";
        ResilienceFallbackText.Foreground = _resilienceMonitor.FallbackActive ? Brushes.Orange : Brushes.Black;
        ResilienceMicText.Text = snapshot.MicrophoneAvailable ? "Available" : "Unavailable";
        ResilienceMicText.Foreground = snapshot.MicrophoneAvailable ? Brushes.Green : Brushes.Red;
        ResilienceDisplayText.Text = snapshot.DisplayCount.ToString();

        ResilienceMemoryText.Text = $"{snapshot.ProcessWorkingSetMb:F0} MB";
        ResilienceCpuText.Text = $"{snapshot.CpuUsagePercent:F1}%";
        ResilienceUptimeText.Text = snapshot.AppUptime.ToString(@"hh\:mm\:ss");
        ResilienceHandlesText.Text = snapshot.HandleCount.ToString();
        ResilienceThreadsText.Text = snapshot.ThreadCount.ToString();
        ResilienceGdiUserText.Text = $"{snapshot.GdiObjectCount} / {snapshot.UserObjectCount}";

        if (snapshot.LastResourceWarningAt.HasValue)
        {
            ResourceWarningPanel.Visibility = Visibility.Visible;
            ResourceWarningText.Text = snapshot.LastResourceWarningMessage;
            ResourceWarningTimeText.Text = $"At {snapshot.LastResourceWarningAt.Value:HH:mm:ss}";
        }
        else
        {
            ResourceWarningPanel.Visibility = Visibility.Collapsed;
        }

        ResilienceEventsList.ItemsSource = _resilienceMonitor.RecentEvents.Reverse();
    }

    private SolidColorBrush GetResilienceStatusBrush(ResilienceStatus status)
    {
        return status switch
        {
            ResilienceStatus.Healthy => Brushes.Green,
            ResilienceStatus.Degraded => Brushes.Orange,
            ResilienceStatus.Offline => Brushes.Red,
            ResilienceStatus.Recovering => Brushes.Blue,
            ResilienceStatus.SafeModeRecommended => Brushes.DarkRed,
            _ => Brushes.Gray
        };
    }

    private void UpdateLifecycleDisplay()
    {
        AppInfoText.Text = AppInfo.DisplayText;
        AppChannelText.Text = $"{AppInfo.BuildChannel} / {AppInfo.ReleaseLabel}";
        WorkerContractText.Text = AppInfo.WorkerContractVersion;
        AppStartedText.Text = _lifecycleService?.AppStartedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        AppUptimeText.Text = _lifecycleService == null ? "-" : FormatUptime(_lifecycleService.Uptime);
    }

    private void UpdateLogDisplay()
    {
        string defaultLogsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs");
        AppLogPathText.Text = _appLogService?.LogPath ?? Path.Combine(defaultLogsDir, "pointypal.log");
        CrashLogsFolderText.Text = _crashLogger?.LogDirectory ?? defaultLogsDir;
        LastErrorSummaryText.Text = string.IsNullOrWhiteSpace(_appLogService?.LastErrorSummary)
            ? "-"
            : _appLogService.LastErrorSummary;
        LastCrashTimestampText.Text = _crashLogger?.LastCrashTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        LatestCrashLogPathText.Text = string.IsNullOrWhiteSpace(_crashLogger?.LatestCrashPath)
            ? "-"
            : _crashLogger!.LatestCrashPath;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private static double GetStepDuration(InteractionTimeline? timeline, string stepName, double fallback)
    {
        if (timeline == null) return fallback;

        return timeline.Steps
            .Where(s => s.Name == stepName)
            .Select(s => s.DurationMs)
            .DefaultIfEmpty(fallback)
            .Last();
    }

    private System.Windows.Media.Brush GetStatusBrush(string status)
    {
        if (status.Contains("Reachable") || status.Contains("OK") || status.Contains("Working"))
            return Brushes.Green;
        if (status.Contains("Error") || status.Contains("Unreachable") || status.Contains("Failed"))
            return Brushes.Red;
        return Brushes.Gray;
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.WorkerClientKey = WorkerClientKeyBox.Password;
        if (!Validate()) return;

        _configService.SaveConfig(_currentConfig);
        ApplyStartupRegistrationWithWarning();
        UpdateLogDisplay();
        UpdateLifecycleDisplay();
        MessageBox.Show("Settings saved successfully.", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
        
        if (_configService.IsFirstRun)
        {
            FirstRunBanner.Visibility = Visibility.Collapsed;
        }
        
        UpdateSafeModeVisibility();
        UpdateModeVisibility();
        UpdateHotkeysDisplay();
        UpdateStatusDisplay();
    }

    private bool Validate()
    {
        if (!string.IsNullOrWhiteSpace(_currentConfig.WorkerBaseUrl) && !_currentConfig.WorkerBaseUrl.StartsWith("https://"))
        {
            MessageBox.Show("Worker Base URL should start with https://", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.DailyInteractionLimit < 0 || _currentConfig.DailyClaudeRequestLimit < 0 ||
            _currentConfig.DailySttSecondsLimit < 0 || _currentConfig.DailyTtsCharLimit < 0)
        {
            MessageBox.Show("Daily limits must be >= 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.MaxTtsChars < 0 || _currentConfig.MinRecordingMs < 0 || _currentConfig.DebugArtifactRetentionHours < 1)
        {
            MessageBox.Show("Time and character limits must be valid positive numbers.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.UiAutomationRadiusPx < 0)
        {
            MessageBox.Show("UI Automation radius must be >= 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.MaxUiElementsInPrompt < 0 || _currentConfig.MaxUiElementsInPrompt > 200)
        {
            MessageBox.Show("Max UI elements must be between 0 and 200.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.UiAutomationTimeoutMs < 0 ||
            _currentConfig.ClaudeRequestTimeoutSeconds < 0 ||
            _currentConfig.TranscriptRequestTimeoutSeconds < 0 ||
            _currentConfig.TtsRequestTimeoutSeconds < 0 ||
            _currentConfig.ScreenshotMaxWidth < 0 ||
            _currentConfig.MaxTimelineHistoryItems < 0)
        {
            MessageBox.Show("Performance timeouts, screenshot width, and timeline history count must be >= 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.ScreenshotJpegQuality < 1 || _currentConfig.ScreenshotJpegQuality > 100)
        {
            MessageBox.Show("Screenshot JPEG quality must be between 1 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.PointSnappingMaxDistancePx < 0 || _currentConfig.PointSnappingMaxDistancePx > 500)
        {
            MessageBox.Show("Point snapping distance must be between 0 and 500px.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.LogRetentionDays < 1 || _currentConfig.LogRetentionDays > 365)
        {
            MessageBox.Show("Log retention must be between 1 and 365 days.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!Enum.TryParse<AppLogLevel>(_currentConfig.LogLevel, ignoreCase: true, out _))
        {
            MessageBox.Show("Log level must be Error, Warning, Info, or Debug.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_currentConfig.TtsEnabled && _currentConfig.TtsProvider == "Worker" && string.IsNullOrWhiteSpace(_currentConfig.ElevenLabsVoiceId))
        {
            var result = MessageBox.Show("ElevenLabs Voice ID is empty but TTS is enabled with Worker provider. Proceed?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No) return false;
        }

        if (_currentConfig.AiProvider == "Claude" && string.IsNullOrWhiteSpace(_currentConfig.WorkerBaseUrl))
        {
            var result = MessageBox.Show("Worker Base URL is empty but Claude provider is selected. Proceed?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No) return false;
        }

        return true;
    }

    private void ReloadBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.ReloadConfig();
        _currentConfig = CloneConfig(_configService.Config);
        DataContext = _currentConfig;
        WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;
        UpdateLogDisplay();
        UpdateLifecycleDisplay();
        UpdateModeVisibility();
        UpdateHotkeysDisplay();
        UpdateStatusDisplay();
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Reset all settings to defaults?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _configService.ResetToDefaults();
            _currentConfig = CloneConfig(_configService.Config);
            DataContext = _currentConfig;
            WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;
            ApplyStartupRegistrationWithWarning();
            UpdateLogDisplay();
            UpdateLifecycleDisplay();
            UpdateModeVisibility();
            UpdateHotkeysDisplay();
            UpdateStatusDisplay();
        }
    }

    private void ApplyStartupRegistrationWithWarning()
    {
        if (_startupRegistrationService == null)
        {
            return;
        }

        var result = _startupRegistrationService.ApplyConfig(_configService.Config);
        if (!result.Success)
        {
            MessageBox.Show(
                $"PointyPal could not update Windows startup registration. The app will keep running.\n\n{result.ErrorMessage}",
                "Startup Registration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "config.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }

    private void OpenDebugBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private async void CheckWorkerBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        CheckWorkerBtn.IsEnabled = false;
        await _healthService.CheckWorkerAsync();
        UpdateHealthDisplay();
        UpdateStatusDisplay();
        CheckWorkerBtn.IsEnabled = true;
        if (sender is Button buttonAfter) buttonAfter.IsEnabled = true;
    }

    private async void CheckAllBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        CheckAllBtn.IsEnabled = false;
        await _healthService.CheckAllAsync();
        UpdateHealthDisplay();
        UpdateStatusDisplay();
        CheckAllBtn.IsEnabled = true;
        if (sender is Button buttonAfter) buttonAfter.IsEnabled = true;
    }

    private async void TestTtsBtn_Click(object sender, RoutedEventArgs e)
    {
        TestTtsBtn.IsEnabled = false;
        bool ok = await _healthService.TestTtsAsync();
        UpdateHealthDisplay();
        if (ok) MessageBox.Show("TTS test request successful.", "Health Check", MessageBoxButton.OK, MessageBoxImage.Information);
        TestTtsBtn.IsEnabled = true;
    }

    private async void RunQuickSelfTestBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunSelfTestAsync(SelfTestMode.Quick);
    }

    private async void RunFullSelfTestBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunSelfTestAsync(SelfTestMode.Full);
    }

    private async System.Threading.Tasks.Task RunSelfTestAsync(SelfTestMode mode)
    {
        RunQuickSelfTestBtn.IsEnabled = false;
        RunFullSelfTestBtn.IsEnabled = false;

        try
        {
            SelfTestStatusText.Text = $"Running {mode} self-test...";
            SelfTestStatusText.Foreground = Brushes.DarkOrange;
            SelfTestLastRunText.Text = "-";
            SelfTestDurationText.Text = "-";
            SelfTestScenarioCountText.Text = "-";
            SelfTestFailedScenarioText.Text = "-";

            var harness = new InteractionSimulationHarness(_configService);
            var result = await System.Threading.Tasks.Task.Run(() =>
                _selfTestReportService.RunAndSaveAsync(harness, mode));
            UpdateSelfTestDisplay(result);
        }
        finally
        {
            RunQuickSelfTestBtn.IsEnabled = true;
            RunFullSelfTestBtn.IsEnabled = true;
        }
    }

    private void OpenSelfTestReportBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = _selfTestReportService.LastReportPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = _selfTestReportService.ReportPath;
        }

        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No self-test report file found. Run a self-test with Save Debug Artifacts enabled.", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ResetUsageBtn_Click(object sender, RoutedEventArgs e)
    {
        _usageTracker.ResetDailyUsage();
        UpdateUsageDisplay();
    }

    private void OpenUsageFileBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "usage.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }

    private void OpenLatestPointBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "latest-pointing-attempt.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No pointing attempt file found. Try interacting first.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFeedbackBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "pointing-feedback.jsonl");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No feedback log found. Use Ctrl+Alt+1/2/3 after a point.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ReplayLastBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        if (app?.Coordinator != null)
        {
            await app.Coordinator.ReplayLastPointAsync();
        }
    }

    private void OpenHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "history");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void ClearInteractionHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all interaction history?", "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "history", "interactions.jsonl");
            if (File.Exists(path))
            {
                try { File.Delete(path); MessageBox.Show("History cleared."); }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }
    }

    private void OpenQuickAskBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        app?.OpenQuickAsk();
    }

    private void OpenLatestTimelineBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = _timelineService.LatestTimelinePath;
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No timeline file found. Timeline may be in memory only or debug artifacts are disabled.", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenTimelineFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.GetDirectoryName(_timelineService.LatestTimelinePath)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void ClearTimelineHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Clear timeline history?", "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _timelineService.ClearTimelineHistory();
        UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
    }

    private void RefreshPerformanceSummaryBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
    }

    private async void RunLatencySelfTestBtn_Click(object sender, RoutedEventArgs e)
    {
        RunLatencySelfTestBtn.IsEnabled = false;
        try
        {
            var harness = new InteractionSimulationHarness(_configService);
            var result = await System.Threading.Tasks.Task.Run(() =>
                harness.RunLatencySelfTestAsync(_timelineService, _performanceSummaryService));
            await _selfTestReportService.SaveLatestAsync(result);
            UpdateSelfTestDisplay(result);
            UpdatePerformanceDisplay(_performanceSummaryService.LastSummary);
        }
        finally
        {
            RunLatencySelfTestBtn.IsEnabled = true;
        }
    }

    private void OpenLogsFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string defaultLogsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs");
        string path = _crashLogger?.LogDirectory ?? defaultLogsDir;
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void OpenLatestLogBtn_Click(object sender, RoutedEventArgs e)
    {
        string defaultLogsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs");
        string path = _appLogService?.LogPath ?? Path.Combine(defaultLogsDir, "pointypal.log");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No app log found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenLatestCrashBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = _crashLogger?.LatestCrashPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No crash log found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all logs? This will not clear crash logs.", "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                _cleanupService?.CleanupAllLogs();
                UpdateLogDisplay();
                MessageBox.Show("Logs cleared.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing logs: {ex.Message}");
            }
        }
    }

    private async void RunPreflightBtn_Click(object sender, RoutedEventArgs e)
    {
        bool showSummary = sender == RunPreflightConnectionBtn;
        if (sender is Button button) button.IsEnabled = false;
        RunPreflightBtn.IsEnabled = false;
        PreflightOverallStatusText.Text = "Running...";
        
        try
        {
            var service = new PreflightCheckService(_configService, _appLogService, _healthService);
            var result = await service.RunAllChecksAsync();
            
            PreflightOverallStatusText.Text = result.OverallStatus.ToString();
            PreflightLastRunText.Text = result.RunTime.ToString("yyyy-MM-dd HH:mm:ss");
            PreflightDurationText.Text = $"{(int)result.Duration.TotalMilliseconds} ms";
            PreflightResultsList.ItemsSource = result.Items;
            
            if (result.OverallStatus == PreflightStatus.Pass) PreflightOverallStatusText.Foreground = Brushes.Green;
            else if (result.OverallStatus == PreflightStatus.Warning) PreflightOverallStatusText.Foreground = Brushes.Orange;
            else PreflightOverallStatusText.Foreground = Brushes.Red;

            if (showSummary)
            {
                MessageBox.Show($"Preflight finished: {result.OverallStatus}", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Preflight failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunPreflightBtn.IsEnabled = true;
            if (sender is Button buttonAfter) buttonAfter.IsEnabled = true;
        }
    }

    private void ExportPreflightBtn_Click(object sender, RoutedEventArgs e)
    {
        string debug = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "debug");
        string path = Path.Combine(debug, "preflight-report.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No report found. Run preflight check first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BackupNowBtn_Click(object sender, RoutedEventArgs e)
    {
        var backupService = new ConfigBackupService(_configService, _appLogService);
        backupService.CreateBackup();
        UpdateRecoveryDisplay();
        MessageBox.Show("Config backup created.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestoreLatestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to restore the latest config backup? This will overwrite current settings.", "Confirm Restore", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
        {
            if (_configService.RestoreLatestBackup())
            {
                _currentConfig = CloneConfig(_configService.Config);
                DataContext = _currentConfig;
                WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;
                UpdateRecoveryDisplay();
                UpdateModeVisibility();
                UpdateHotkeysDisplay();
                UpdateStatusDisplay();
                MessageBox.Show("Config restored. Some changes may require restart.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to restore backup.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OpenBackupsFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "backups");
        if (Directory.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private async void RunReadinessBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_readinessService == null) return;
        RunReadinessBtn.IsEnabled = false;
        ReadinessStatusText.Text = "Checking...";
        ReadinessStatusText.Foreground = Brushes.Orange;

        try
        {
            var result = await _readinessService.RunReadinessCheckAsync();
            UpdateReadinessDisplay(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Readiness check failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunReadinessBtn.IsEnabled = true;
        }
    }

    private void UpdateReadinessDisplay(RcReadinessResult result)
    {
        ReadinessScoreText.Text = result.Score.ToString();
        ReadinessStatusText.Text = result.Status.ToString().ToUpper();
        ReadinessTimeText.Text = $"Last checked: {result.Timestamp:HH:mm:ss}";

        ReadinessStatusText.Foreground = result.Status switch
        {
            RcReadinessStatus.Ready => Brushes.Green,
            RcReadinessStatus.ReadyWithWarnings => Brushes.Orange,
            RcReadinessStatus.NotReady => Brushes.Red,
            _ => Brushes.Gray
        };

        ReadinessIssuesList.ItemsSource = result.BlockingIssues;
        ReadinessWarningsList.ItemsSource = result.Warnings;
        ReadinessActionsList.ItemsSource = result.RecommendedActions;
    }

    private void ExportReadinessBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "rc-readiness-report.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No report found. Run readiness check first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExitSafeModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.Config.ForceSafeMode = false;
        _configService.SaveConfig(_configService.Config);
        _crashLoopGuard?.Reset();
        UpdateSafeModeVisibility();
        MessageBox.Show("Safe Mode will be disabled on next launch.", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RecoveryDocsBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "recovery.md");
        if (File.Exists(path)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        else MessageBox.Show("Recovery documentation not found.");
    }

    private void RestoreLatestBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        RestoreLatestBtn_Click(sender, e);
    }

    private void ClearDebugFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        _cleanupService?.RunStartupCleanup();
        MessageBox.Show("Debug files cleared.", "Success");
    }

    private void ClearUsageDataBtn_Click(object sender, RoutedEventArgs e)
    {
        _usageTracker.ResetDailyUsage();
        UpdateUsageDisplay();
        MessageBox.Show("Usage data cleared.", "Success");
    }

    private void ClearPointingFeedbackBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "feedback");
        if (Directory.Exists(path))
        {
            try
            {
                foreach (var file in Directory.GetFiles(path)) File.Delete(file);
                MessageBox.Show("Pointing feedback cleared.", "Success");
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }
        else
        {
            MessageBox.Show("No feedback log found.", "Info");
        }
    }

    private void FactoryResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("WARNING: This will clear all logs, history, usage data, and reset configuration to defaults. This action cannot be undone.\n\nContinue with Factory Reset?", "Confirm Factory Reset", MessageBoxButton.OKCancel, MessageBoxImage.Stop) == MessageBoxResult.OK)
        {
            _configService.FactoryResetLocalState();
            _currentConfig = CloneConfig(_configService.Config);
            DataContext = _currentConfig;
            WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;
            UpdateUsageDisplay();
            UpdateLogDisplay();
            UpdateRecoveryDisplay();
            UpdateSafeModeVisibility();
            UpdateModeVisibility();
            UpdateHotkeysDisplay();
            UpdateStatusDisplay();
            MessageBox.Show("Factory reset complete. The application will now use default settings.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void RunSoakTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_resilienceMonitor == null) return;

        RunSoakTestBtn.IsEnabled = false;
        SoakStatusText.Text = "Running soak test...";
        SoakStatusText.Foreground = Brushes.DarkOrange;

        try
        {
            int minutes = int.TryParse(SoakDurationBox.Text, out var m) ? m : 10;
            int interval = int.TryParse(SoakIntervalBox.Text, out var i) ? i : 1000;

            var report = await _resilienceMonitor.RunSoakTestAsync(minutes, interval);
            
            SoakStatusText.Text = $"Finished: {report.PassedIterations}/{report.TotalIterations} passed";
            SoakStatusText.Foreground = report.FailedIterations == 0 ? Brushes.Green : Brushes.Red;
            UpdateResilienceDisplay();
            
            MessageBox.Show($"Soak test finished.\nPassed: {report.PassedIterations}\nFailed: {report.FailedIterations}", "Soak Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SoakStatusText.Text = "Soak test failed";
            SoakStatusText.Foreground = Brushes.Red;
            MessageBox.Show($"Soak test failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunSoakTestBtn.IsEnabled = true;
        }
    }

    private void OpenSoakReportBtn_Click(object sender, RoutedEventArgs e)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string path = Path.Combine(appData, "PointyPal", "debug", "soak-test-report.json");
        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            MessageBox.Show("No soak test report found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearResilienceEventsBtn_Click(object sender, RoutedEventArgs e)
    {
        _resilienceMonitor?.ClearEvents();
        UpdateResilienceDisplay();
    }

    private void ResetProviderFailuresBtn_Click(object sender, RoutedEventArgs e)
    {
        _resilienceMonitor?.ResetFailures();
        UpdateResilienceDisplay();
    }

    private void UpdateOnboardingDisplay()
    {
        var items = new System.Collections.Generic.List<OnboardingItem>();

        // 1. Worker Connectivity
        bool workerOk = _healthService.WorkerStatus == "Healthy";
        items.Add(new OnboardingItem
        {
            Name = "Worker Connectivity",
            Hint = workerOk ? "Connected to Cloudflare Worker." : "Worker is not reachable. Check your URL.",
            StatusIcon = workerOk ? "✅" : "❌",
            ActionText = "Check Health",
            ActionVisibility = Visibility.Visible,
            Action = () => MainTabs.SelectedItem = MainTabs.Items.Cast<TabItem>().FirstOrDefault(t => t.Header.ToString() == "Status")
        });

        // 2. AI Provider
        var providerStatus = _providerPolicy.GetProviderStatusForUi();
        bool aiOk = providerStatus.AiReady && workerOk;
        items.Add(new OnboardingItem
        {
            Name = "AI Provider",
            Hint = aiOk ? "Normal Mode is ready to use real Worker-backed providers." : "Normal Mode requires WorkerBaseUrl and WorkerClientKey.",
            StatusIcon = aiOk ? "✅" : "⚠️",
            ActionText = "Go to Connection",
            ActionVisibility = aiOk ? Visibility.Collapsed : Visibility.Visible,
            Action = () => MainTabs.SelectedItem = GeneralTab
        });

        items.Add(new OnboardingItem
        {
            Name = "Choose User Mode",
            Hint = "Normal Mode is recommended for daily use. Developer Mode exposes simulated providers, test hotkeys, calibration, and advanced diagnostics.",
            StatusIcon = _configService.Config.DeveloperModeEnabled ? "DEV" : "OK",
            ActionText = "Go to Basic Settings",
            ActionVisibility = Visibility.Visible,
            Action = () => MainTabs.SelectedItem = BasicTab
        });

        // 3. Microphone
        items.Add(new OnboardingItem
        {
            Name = "Microphone Access",
            Hint = "Verify if PointyPal can hear you.",
            StatusIcon = "🎤",
            ActionText = "Go to Voice",
            ActionVisibility = Visibility.Visible,
            Action = () => MainTabs.SelectedItem = MainTabs.Items.Cast<TabItem>().FirstOrDefault(t => t.Header.ToString() == "Voice")
        });

        // 4. Hotkeys
        items.Add(new OnboardingItem
        {
            Name = "Learn Hotkeys",
            Hint = "Right Ctrl for voice, Ctrl+Space for Quick Ask, Escape to cancel.",
            StatusIcon = "⌨️",
            ActionText = "View Hotkeys",
            ActionVisibility = Visibility.Visible,
            Action = () => MainTabs.SelectedItem = HotkeysTab
        });

        OnboardingList.ItemsSource = items;
        CompleteOnboardingBtn.IsEnabled = !_configService.Config.OnboardingCompleted;
        if (_configService.Config.OnboardingCompleted) CompleteOnboardingBtn.Content = "Onboarding Completed ✅";
    }

    private void UpdateHotkeysDisplay()
    {
        var items = _hotkeyPolicy.GetVisibleHotkeyReferenceItems()
            .Select(item => new HotkeyItem
            {
                Key = item.Key,
                Description = item.DeveloperOnly && _configService.Config.DeveloperModeEnabled && !_configService.Config.EnableDeveloperHotkeys
                    ? $"{item.Description} (developer hotkeys disabled)"
                    : item.Description
            })
            .ToList();
        HotkeysList.ItemsSource = items;
    }

    private void UpdateStatusDisplay()
    {
        var providerStatus = _providerPolicy.GetProviderStatusForUi();
        StatusModeText.Text = providerStatus.ModeLabel;
        StatusModeText.Foreground = _configService.SafeModeActive ? Brushes.Red :
            _configService.Config.DeveloperModeEnabled ? Brushes.Blue : Brushes.Black;
        
        bool workerConnected = _healthService.WorkerStatus.Contains("Reachable") ||
                               _healthService.WorkerStatus.Contains("OK") ||
                               _healthService.WorkerStatus.Contains("Healthy");
        StatusWorkerText.Text = workerConnected ? "Ready" : UserMessages.StatusUnreachable;
        StatusWorkerText.Foreground = workerConnected ? Brushes.Green : Brushes.Orange;

        bool hasAuth = providerStatus.WorkerAuthConfigured;
        StatusAuthText.Text = hasAuth ? "Ready" : "Missing";
        StatusAuthText.Foreground = hasAuth ? Brushes.Green : Brushes.Red;

        StatusAiReadyText.Text = providerStatus.AiReady ? "Yes" : "No";
        StatusAiReadyText.Foreground = providerStatus.AiReady ? Brushes.Green : Brushes.Red;
        StatusVoiceReadyText.Text = providerStatus.VoiceInputReady ? "Yes" : "No";
        StatusVoiceReadyText.Foreground = providerStatus.VoiceInputReady ? Brushes.Green : Brushes.Red;
        StatusTtsReadyText.Text = providerStatus.TtsReady ? "Yes" : "No";
        StatusTtsReadyText.Foreground = providerStatus.TtsReady ? Brushes.Green : Brushes.Red;
        StatusSafeModeText.Text = providerStatus.SafeModeActive ? "Yes" : "No";
        StatusSafeModeText.Foreground = providerStatus.SafeModeActive ? Brushes.Red : Brushes.Green;
        StatusDeveloperModeText.Text = providerStatus.DeveloperModeEnabled ? "Yes" : "No";
        StatusDeveloperModeText.Foreground = providerStatus.DeveloperModeEnabled ? Brushes.Blue : Brushes.Green;

        StatusInteractionsText.Text = $"{_usageTracker.CurrentUsage.InteractionsCount} / {_configService.Config.DailyInteractionLimit}";
        StatusBuildText.Text = $"{AppInfo.Version} {AppInfo.ReleaseLabel} ({AppInfo.BuildChannel})";
        
        var resilience = _resilienceMonitor?.GetCurrentSnapshot();
        StatusErrorText.Text = _resilienceMonitor?.RecentEvents.LastOrDefault(e => e.Severity == "Error")?.Message ?? "None";

        ActiveAiText.Text = $"{providerStatus.AiProvider} / Ready: {(providerStatus.AiReady ? "Yes" : "No")}";
        ActiveSttText.Text = $"{providerStatus.TranscriptProvider} / Voice input ready: {(providerStatus.VoiceInputReady ? "Yes" : "No")}";
        ActiveTtsText.Text = $"{providerStatus.TtsProvider} / Ready: {(providerStatus.TtsReady ? "Yes" : "No")}";

        var lastTimeline = _timelineService.LastTimeline;
        if (lastTimeline != null)
        {
            var claudeStep = lastTimeline.Steps.LastOrDefault(s => s.Name == "ClaudeRequest");
            if (claudeStep != null && claudeStep.Metadata.TryGetValue("RequestId", out var rid))
            {
                StatusRequestIdText.Text = rid;
            }
        }
    }

    private void OnboardingAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is OnboardingItem item)
        {
            item.Action?.Invoke();
        }
    }

    private void CompleteOnboardingBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.OnboardingCompleted = true;
        _configService.Config.OnboardingCompleted = true;
        _configService.SaveConfig(_configService.Config);
        UpdateOnboardingDisplay();
        MessageBox.Show("Onboarding marked as completed. You can always return here from the Onboarding tab.", "Onboarding", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ApplyPrivacyDefaultsBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.SaveDebugArtifacts = false;
        _currentConfig.SaveInteractionHistory = false;
        _currentConfig.SaveScreenshots = false;
        _currentConfig.SaveRecordings = false;
        _currentConfig.SaveTtsAudio = false;
        _currentConfig.RedactDebugPayloads = true;
        _currentConfig.SaveUiAutomationDebug = false;
        
        // Update UI bindings
        DataContext = null;
        DataContext = _currentConfig;
        
        MessageBox.Show("Privacy-safe defaults applied. Save settings to persist.", "Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void RunFullValidationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_validationService == null) return;
        
        RunFullValidationBtn.IsEnabled = false;
        RunFullValidationBtn.Content = "Validating...";
        
        try
        {
            var report = await _validationService.RunFullValidationAsync();
            
            string message = $"RC Validation Finished: {report.OverallStatus}\n\n" +
                             $"Blocking Issues: {report.BlockingIssues.Count}\n" +
                             $"Warnings: {report.Warnings.Count}\n\n" +
                             $"Report saved to: {report.ReportPath}";
                             
            MessageBox.Show(message, "RC Validation", MessageBoxButton.OK, 
                report.OverallStatus == RcValidationStatus.Pass ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Validation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunFullValidationBtn.IsEnabled = true;
            RunFullValidationBtn.Content = "Run Full RC Validation";
        }
    }

    private void RunWizardBtn_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new SetupWizardWindow(_configService, _healthService, _appLogService, (Application.Current as App)?.Coordinator);
        wizard.Owner = this;
        wizard.ShowDialog();
        
        if (wizard.Result.Completed)
        {
            _currentConfig = CloneConfig(_configService.Config);
            DataContext = _currentConfig;
            WorkerClientKeyBox.Password = _currentConfig.WorkerClientKey;
            UpdateStatusDisplay();
            UpdateHealthDisplay();
            UpdateOnboardingDisplay();
        }
    }

    private void ResetOnboardingBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.Config.SetupWizardCompleted = false;
        _configService.Config.OnboardingCompleted = false;
        _configService.Config.ShowSetupWizardOnStartup = true;
        _configService.Config.ShowOnboardingOnStartup = true;
        _configService.SaveConfig(_configService.Config);
        
        _currentConfig.SetupWizardCompleted = false;
        _currentConfig.OnboardingCompleted = false;
        _currentConfig.ShowSetupWizardOnStartup = true;
        _currentConfig.ShowOnboardingOnStartup = true;
        
        UpdateOnboardingDisplay();
        MessageBox.Show("Onboarding markers reset.", "PointyPal", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportBundleBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var backupService = new ConfigBackupService(_configService, _appLogService);
            string path = backupService.ExportBundle();
            MessageBox.Show($"Config bundle exported to:\n{path}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportBundleBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PointyPal Bundle (*.zip)|*.zip",
            Title = "Import Config Bundle"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = MessageBox.Show("This will overwrite your current configuration. A backup will be created first. Proceed?", "Import", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                var backupService = new ConfigBackupService(_configService, _appLogService);
                if (backupService.ImportBundle(dialog.FileName))
                {
                    MessageBox.Show("Import successful. Reloading configuration...", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                    ReloadBtn_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Import failed. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenDoc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string relativePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.Combine(baseDir, relativePath);
                if (File.Exists(fullPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show($"Document not found: {relativePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private class OnboardingItem
    {
        public string Name { get; set; } = "";
        public string Hint { get; set; } = "";
        public string StatusIcon { get; set; } = "⚪";
        public string ActionText { get; set; } = "";
        public Visibility ActionVisibility { get; set; } = Visibility.Collapsed;
        public Action? Action { get; set; }
    }

    private class HotkeyItem
    {
        public string Key { get; set; } = "";
        public string Description { get; set; } = "";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't close app, just hide/close this window
        base.OnClosing(e);
    }
}
