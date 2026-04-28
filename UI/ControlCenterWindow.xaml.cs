using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using PointyPal.Infrastructure;
using PointyPal.Input;
using PointyPal.Core;

namespace PointyPal.UI;

public partial class ControlCenterWindow : Window
{
    private readonly ConfigService _configService;
    private readonly UsageTracker _usageTracker;
    private readonly ProviderHealthCheckService _healthService;
    private readonly PushToTalkService _pttService;
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
    private readonly PointerQualityService _qualityService;
    private readonly ProviderPolicyService _providerPolicy;
    private readonly HotkeyPolicyService _hotkeyPolicy;
    private AppConfig _currentConfig;


    public ControlCenterWindow(
        ConfigService configService,
        UsageTracker usageTracker,
        ProviderHealthCheckService healthService,
        PushToTalkService pttService,
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
        RcValidationService? validationService = null,
        PointerQualityService? qualityService = null)
    {
        InitializeComponent();
        _configService = configService;
        _usageTracker = usageTracker;
        _healthService = healthService;
        _pttService = pttService;
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
        _qualityService = qualityService ?? new PointerQualityService();
        _providerPolicy = new ProviderPolicyService(_configService);
        _hotkeyPolicy = new HotkeyPolicyService(_configService);

        _currentConfig = CloneConfig(_configService.Config);
        DataContext = _currentConfig;
        
        // Ensure PasswordBox is synced (it doesn't support direct binding)
        if (AdvancedWorkerKeyBox != null)
        {
            AdvancedWorkerKeyBox.Password = _currentConfig.WorkerClientKey;
        }

        RefreshAllDisplays();

        if (_configService.Config.ShowSetupWizardOnStartup && !_configService.Config.SetupWizardCompleted)
        {
            SidebarList.SelectedIndex = 1; // Setup
            // Use Background priority to ensure the window is rendered before we try to show the dialog
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => RunWizardBtn_Click(this, new RoutedEventArgs())));
        }
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    private void QuickAskBtn_Click(object sender, RoutedEventArgs e) => OpenQuickAskBtn_Click(sender, e);
    private void OpenCalibration_Click(object sender, RoutedEventArgs e) => RunCalibrationBtn_Click(sender, e);
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e) => OpenLogsFolderBtn_Click(sender, e);
    private void ClearHistory_Click(object sender, RoutedEventArgs e) => ClearLogsBtn_Click(sender, e);
    private void OpenSetupWizard_Click(object sender, RoutedEventArgs e) => RunWizardBtn_Click(sender, e);
    private void CheckWorkerBtn_Click(object sender, RoutedEventArgs e) => CheckAllBtn_Click(sender, e);

    private void RefreshAllDisplays()
    {
        UpdateUsageDisplay();
        UpdateHealthDisplay();
        UpdateSelfTestDisplay(_selfTestReportService.LastResult);
        UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
        UpdateLifecycleDisplay();
        UpdateLogDisplay();
        UpdateSafeModeVisibility();
        UpdateResilienceDisplay();
        UpdateHotkeysDisplay();
        UpdateModeVisibility();
        UpdatePointingDisplay();
    }

    private void SidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabs == null || SidebarList == null) return;
        MainTabs.SelectedIndex = SidebarList.SelectedIndex;
    }

    private void UpdateSafeModeVisibility()
    {
        if (_configService.SafeModeActive)
        {
            // Safe mode is handled in UpdateHomeDisplay for the hero section
            SaveBtn.IsEnabled = true;
        }
    }

    private void UpdateModeVisibility()
    {
        bool developerMode = _configService.Config.DeveloperModeEnabled;
        bool showAdvanced = developerMode || _configService.SafeModeActive;
        var advancedVisibility = showAdvanced ? Visibility.Visible : Visibility.Collapsed;

        AdvancedSidebarItem.Visibility = advancedVisibility;

        if (!showAdvanced && SidebarList.SelectedIndex == 5)
        {
            SidebarList.SelectedIndex = 0; // Home
        }

        UpdateHomeDisplay();
    }

    private void UpdateHomeDisplay()
    {
        bool setupDone = _configService.Config.SetupWizardCompleted;
        bool workerOk = _healthService.WorkerStatus.Contains("Healthy") || _healthService.WorkerStatus.Contains("OK");
        bool safeMode = _configService.SafeModeActive;
        bool devMode = _configService.Config.DeveloperModeEnabled;

        if (safeMode)
        {
            HomeStatusTitle.Text = "System in Safe Mode";
            ApplyHomeBadge("EMERGENCY", "SurfaceLighterBrush", "ErrorBrush", "ErrorBrush");
            HomeStatusDescription.Text = "A critical issue was detected. Recovery tools are available while real providers stay disabled.";
            HomePrimaryBtn.Content = "Open Recovery Tools";
            HomePrimaryBtn.Tag = "Recovery";
            HomeSecondaryBtn.Visibility = Visibility.Collapsed;
        }
        else if (!setupDone)
        {
            HomeStatusTitle.Text = "Finish setup to start";
            ApplyHomeBadge("SETUP REQUIRED", "SurfaceLighterBrush", "WarningBrush", "WarningBrush");
            HomeStatusDescription.Text = "Connect your Worker, test voice, and prepare PointyPal for guided screen assistance.";
            HomePrimaryBtn.Content = "Start Setup Wizard";
            HomePrimaryBtn.Tag = "Setup";
            HomeSecondaryBtn.Visibility = Visibility.Visible;
            HomeSecondaryBtn.Content = "How it Works";
            HomeSecondaryBtn.Tag = "Help";
        }
        else if (!workerOk)
        {
            HomeStatusTitle.Text = "Connection problem";
            ApplyHomeBadge("OFFLINE", "SurfaceLighterBrush", "WarningBrush", "WarningBrush");
            HomeStatusDescription.Text = "PointyPal cannot reach the Worker service right now.";
            HomePrimaryBtn.Content = "Retry Connection";
            HomePrimaryBtn.Tag = "Retry";
            HomeSecondaryBtn.Visibility = Visibility.Visible;
            HomeSecondaryBtn.Content = "Connection Settings";
            HomeSecondaryBtn.Tag = "Setup";
        }
        else if (devMode)
        {
            HomeStatusTitle.Text = "Developer Mode Active";
            ApplyHomeBadge("DEVELOPER", "CyanAccentSubtleBrush", "AccentBorderBrush", "PrimaryAccentBrush");
            HomeStatusDescription.Text = "Advanced tools and diagnostics are enabled for testing.";
            HomePrimaryBtn.Content = "Open Advanced Tools";
            HomePrimaryBtn.Tag = "Advanced";
            HomeSecondaryBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            HomeStatusTitle.Text = "PointyPal is ready";
            ApplyHomeBadge("READY", "CyanAccentSubtleBrush", "AccentBorderBrush", "PrimaryAccentBrush");
            HomeStatusDescription.Text = "Your private assistant is connected, listening for intent, and ready to guide you on screen.";
            HomePrimaryBtn.Content = "Open Quick Ask";
            HomePrimaryBtn.Tag = "QuickAsk";
            HomeSecondaryBtn.Visibility = Visibility.Visible;
            HomeSecondaryBtn.Content = "Learn Commands";
            HomeSecondaryBtn.Tag = "Help";
        }

        var status = _providerPolicy.GetProviderStatusForUi();
        var readyBrush = UiBrush("SuccessBrush", Brushes.Green);
        var mutedBrush = UiBrush("TextMutedBrush", Brushes.Gray);
        VoiceStatusDot.Fill = status.VoiceInputReady ? readyBrush : mutedBrush;
        QuickAskStatusDot.Fill = readyBrush;
        ScreenStatusDot.Fill = readyBrush;
        PointerStatusDot.Fill = status.AiReady ? readyBrush : mutedBrush;
    }

    private void ApplyHomeBadge(string text, string backgroundBrushKey, string borderBrushKey, string textBrushKey)
    {
        HomeStatusBadgeText.Text = text;
        HomeStatusBadge.Background = UiBrush(backgroundBrushKey, Brushes.Transparent);
        HomeStatusBadge.BorderBrush = UiBrush(borderBrushKey, Brushes.Transparent);
        HomeStatusBadgeText.Foreground = UiBrush(textBrushKey, Brushes.White);
    }

    private Brush UiBrush(string resourceKey, Brush fallback)
    {
        return TryFindResource(resourceKey) as Brush ?? fallback;
    }

    private async void HomePrimaryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string action)
        {
            switch (action)
            {
                case "QuickAsk":
                    OpenQuickAskBtn_Click(sender, e);
                    break;
                case "Setup":
                    SidebarList.SelectedIndex = 1;
                    break;
                case "Recovery":
                    SidebarList.SelectedIndex = 5;
                    AdvancedTabs.SelectedItem = RecoveryTab;
                    break;
                case "Retry":
                    await _healthService.CheckAllAsync();
                    UpdateHomeDisplay();
                    break;
                case "Advanced":
                    SidebarList.SelectedIndex = 5;
                    break;
                case "Usage":
                    SidebarList.SelectedIndex = 2;
                    break;
            }
        }
    }

    private void HomeSecondaryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string action)
        {
            switch (action)
            {
                case "Help":
                    SidebarList.SelectedIndex = 4;
                    break;
                case "Setup":
                    SidebarList.SelectedIndex = 1;
                    break;
            }
        }
    }

    private void RunWizardBtn_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new SetupWizardWindow(_configService, _healthService, _appLogService, (Application.Current as App)?.Coordinator);
        
        // Safety check: WPF windows must be shown before they can be owners
        if (this.IsVisible)
        {
            wizard.Owner = this;
        }
        
        wizard.ShowDialog();
        
        if (wizard.Result.Completed)
        {
            _currentConfig = CloneConfig(_configService.Config);
            DataContext = _currentConfig;
            RefreshAllDisplays();
        }
    }

    private void ResetUsageBtn_Click(object sender, RoutedEventArgs e)
    {
        _usageTracker.ResetDailyUsage();
        UpdateUsageDisplay();
    }

    private void ApplyPrivacyDefaultsBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.SaveScreenshots = false;
        _currentConfig.SaveRecordings = false;
        _currentConfig.SaveInteractionHistory = false;
        _currentConfig.RedactDebugPayloads = true;
        
        DataContext = null;
        DataContext = _currentConfig;
        
        MessageBox.Show("Privacy-safe defaults applied. Click Save to persist.", "Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void RunTutorial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tutorialId)
        {
            var app = Application.Current as App;
            switch (tutorialId)
            {
                case "VoiceAsk":
                    MessageBox.Show("Hold Right Ctrl and speak. Release to send.", "Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Pointer":
                    if (app?.Coordinator != null) await app.Coordinator.ReplayLastPointAsync();
                    break;
                case "Cancel":
                    MessageBox.Show("Press Escape to cancel any action.", "Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }
    }

    private void OpenQuickAskBtn_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.OpenQuickAsk();
    }

    private void OpenDoc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                if (File.Exists(fullPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
                }
            }
            catch { }
        }
    }

    private async void CheckAllBtn_Click(object sender, RoutedEventArgs e)
    {
        await _healthService.CheckAllAsync();
        UpdateHealthDisplay();
        UpdateHomeDisplay();
    }

    private async void RunLatencySelfTestBtn_Click(object sender, RoutedEventArgs e)
    {
        RunLatencySelfTestBtn.IsEnabled = false;
        try
        {
            await _performanceSummaryService.RunLatencyBenchmarkAsync();
            UpdatePerformanceDisplay(_performanceSummaryService.LastSummary);
        }
        finally { RunLatencySelfTestBtn.IsEnabled = true; }
    }

    private async void RunQuickSelfTestBtn_Click(object sender, RoutedEventArgs e)
    {
        RunQuickSelfTestBtn.IsEnabled = false;
        try
        {
            var result = await _selfTestReportService.RunQuickSelfTestAsync();
            UpdateSelfTestDisplay(result);
        }
        finally { RunQuickSelfTestBtn.IsEnabled = true; }
    }

    private async void RunFullValidationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_validationService == null) return;
        RunFullValidationBtn.IsEnabled = false;
        try
        {
            await _validationService.RunFullValidationAsync();
            MessageBox.Show("Validation complete.", "Validation");
        }
        finally { RunFullValidationBtn.IsEnabled = true; }
    }

    private void OpenSelfTestReportBtn_Click(object sender, RoutedEventArgs e)
    {
        _selfTestReportService.OpenLastReport();
    }

    private void OpenLogsFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_appLogService?.LogDirectory))
            System.Diagnostics.Process.Start("explorer.exe", _appLogService.LogDirectory);
    }

    private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_appLogService?.LogDirectory))
        {
            foreach (var f in Directory.GetFiles(_appLogService.LogDirectory, "*.log*"))
                try { File.Delete(f); } catch { }
        }
        UpdateLogDisplay();
    }

    private async void RunPreflightBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_preflightService == null) return;
        RunPreflightBtn.IsEnabled = false;
        try
        {
            var result = await _preflightService.RunAllChecksAsync();
            UpdatePreflightDisplay(result);
        }
        finally { RunPreflightBtn.IsEnabled = true; }
    }

    private void ResetConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Reset settings?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _configService.ResetToDefaults();
            ReloadBtn_Click(sender, e);
        }
    }

    private void RestoreLatestBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        var backupService = new ConfigBackupService(_configService, _appLogService);
        if (backupService.RestoreLatest())
        {
            MessageBox.Show("Backup restored.");
            ReloadBtn_Click(sender, e);
        }
    }

    private void FactoryResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("FACTORY RESET?", "WARNING", MessageBoxButton.YesNo, MessageBoxImage.Stop) == MessageBoxResult.Yes)
        {
            _configService.FactoryReset();
            Application.Current.Shutdown();
        }
    }

    private void ReloadBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.ReloadConfig();
        _currentConfig = CloneConfig(_configService.Config);
        DataContext = _currentConfig;
        RefreshAllDisplays();
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.WorkerClientKey = AdvancedWorkerKeyBox.Password;
        _configService.SaveConfig(_currentConfig);
        _configService.ReloadConfig();
        RefreshAllDisplays();
        MessageBox.Show("Settings saved.");
    }

    private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.OpenConfigInEditor();
    }

    private void OpenDebugBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void ClearResilienceEventsBtn_Click(object sender, RoutedEventArgs e)
    {
        _resilienceMonitor?.ClearEvents();
        UpdateResilienceDisplay();
    }

    private void OpenSoakReportBtn_Click(object sender, RoutedEventArgs e)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string path = Path.Combine(appData, "PointyPal", "debug", "soak-test-report.json");
        if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void OpenHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "history");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void ClearInteractionHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear history?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "history", "interactions.jsonl");
            if (File.Exists(path)) try { File.Delete(path); } catch { }
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
        if (MessageBox.Show("Clear timeline?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _timelineService.ClearTimelineHistory();
            UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
        }
    }

    private void RefreshPerformanceSummaryBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdatePerformanceDisplay(_performanceSummaryService.RefreshSummary(_timelineService.LastTimeline));
    }

    private void OpenLatestLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(_appLogService?.LogPath))
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_appLogService.LogPath}\"");
    }

    private void OpenLatestCrashBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_crashLogger?.LatestCrashPath) && File.Exists(_crashLogger.LatestCrashPath))
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_crashLogger.LatestCrashPath}\"");
    }

    private void ExportPreflightBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "debug", "preflight-report.json");
        if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void BackupNowBtn_Click(object sender, RoutedEventArgs e)
    {
        new ConfigBackupService(_configService, _appLogService).CreateBackup();
        MessageBox.Show("Backup created.");
    }

    private void OpenBackupsFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "backups");
        if (Directory.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private async void RunReadinessBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_readinessService == null) return;
        var result = await _readinessService.RunReadinessCheckAsync();
        UpdateReadinessDisplay(result);
    }

    private void UpdateReadinessDisplay(RcReadinessResult result)
    {
        // Readiness logic if needed, but XAML might not have all labels now
    }

    private void ExportReadinessBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug", "rc-readiness-report.json");
        if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void ClearDebugFilesBtn_Click(object sender, RoutedEventArgs e)
    {
        _cleanupService?.RunStartupCleanup();
        MessageBox.Show("Debug files cleared.");
    }

    private void ClearUsageDataBtn_Click(object sender, RoutedEventArgs e)
    {
        _usageTracker.ResetDailyUsage();
        UpdateUsageDisplay();
    }

    private void ClearPointingFeedbackBtn_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "feedback");
        if (Directory.Exists(path)) { foreach (var f in Directory.GetFiles(path)) File.Delete(f); }
    }

    private void ResetProviderFailuresBtn_Click(object sender, RoutedEventArgs e)
    {
        _resilienceMonitor?.ResetFailures();
        UpdateResilienceDisplay();
    }

    private void OpenSoakTestBtn_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder if needed
    }

    private async void RunSoakTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_resilienceMonitor == null) return;
        RunSoakTestBtn.IsEnabled = false;
        try
        {
            int minutes = int.TryParse(SoakDurationBox.Text, out var m) ? m : 10;
            int interval = int.TryParse(SoakIntervalBox.Text, out var i) ? i : 1000;
            await _resilienceMonitor.RunSoakTestAsync(minutes, interval);
            UpdateResilienceDisplay();
        }
        finally { RunSoakTestBtn.IsEnabled = true; }
    }

    private void HideAdvancedBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentConfig.DeveloperModeEnabled = false;
        _configService.Config.DeveloperModeEnabled = false;
        _configService.SaveConfig(_configService.Config);
        UpdateModeVisibility();
        SidebarList.SelectedIndex = 0;
    }

    private void RunCalibrationBtn_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.Coordinator?.CancelCurrentInteraction("Calibration requested");
        // Trigger calibration toggle
        _pttService.RequestCalibrationToggle();
    }

    private async void ReplayLastPointBtn_Click(object sender, RoutedEventArgs e)
    {
        if ((Application.Current as App)?.Coordinator != null)
            await (Application.Current as App)!.Coordinator!.ReplayLastPointAsync();
    }

    private void ExportPointingReportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = _qualityService.ExportReport(_currentConfig);
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdatePointingDisplay()
    {
        var stats = _qualityService.GetStats();
        var report = _qualityService.GenerateReport(_currentConfig);

        PointingCorrectText.Text = $"{stats.CorrectPercentage:F0}%";
        PointingCloseText.Text = $"{stats.ClosePercentage:F0}%";
        PointingWrongText.Text = $"{stats.WrongPercentage:F0}%";
        PointingSampleSizeText.Text = $"Sample Size: {stats.SampleSize}";
        
        PointingRecommendationText.Text = report.Recommendation.ToString();
        PointingRecommendationText.Foreground = report.Recommendation switch
        {
            PointerQaRecommendation.Good => Brushes.Green,
            PointerQaRecommendation.NeedsCalibration => Brushes.Gray,
            PointerQaRecommendation.NeedsThresholdTuning => Brushes.Orange,
            PointerQaRecommendation.NeedsInvestigation => Brushes.Red,
            _ => Brushes.Black
        };
    }

    private void OpenSoakReportBtn_Click_1(object sender, RoutedEventArgs e)
    {
        // Placeholder if needed
    }

    // Helper Displays
    private void UpdateUsageDisplay()
    {
        var usage = _usageTracker.CurrentUsage;
        UsageInteractionsText.Text = usage.InteractionsCount.ToString();
        UsageClaudeText.Text = usage.ClaudeRequestsCount.ToString();
        UsageSttText.Text = usage.SttSeconds.ToString("F0");
        UsageTtsText.Text = usage.TtsCharacters.ToString();
        EmptyUsagePanel.Visibility = usage.InteractionsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateHealthDisplay()
    {
        WorkerStatusText.Text = _healthService.WorkerStatus;
        AiStatusText.Text = _healthService.AiStatus;
        SttStatusText.Text = _healthService.TranscriptStatus;
        TtsStatusText.Text = _healthService.TtsStatus;
        LastCheckTimeText.Text = _healthService.LastCheckTime?.ToString("HH:mm") ?? "-";
    }

    private void UpdateSelfTestDisplay(SelfTestResult? result)
    {
        if (result == null) return;
        SelfTestStatusText.Text = result.Passed ? "Passed" : "Failed";
    }

    private void UpdatePerformanceDisplay(PerformanceSummary summary)
    {
        LastTotalDurationText.Text = $"{summary.LastTotalDurationMs}ms";
        P50DurationText.Text = $"{summary.P50TotalDurationMs}ms";
        SlowestStepText.Text = summary.SlowestStepName;
    }

    private void UpdateLifecycleDisplay()
    {
        // Placeholder
    }

    private void UpdateLogDisplay()
    {
        AppLogPathText.Text = _appLogService?.LogPath ?? "Default";
    }

    private void UpdateResilienceDisplay()
    {
        if (_resilienceMonitor == null) return;
        var snapshot = _resilienceMonitor.GetCurrentSnapshot();
        ResilienceStatusText.Text = snapshot.Status.ToString();
        ResilienceMemoryText.Text = $"{snapshot.ProcessWorkingSetMb:F0} MB";
    }

    private void UpdateHotkeysDisplay()
    {
        var items = _hotkeyPolicy.GetVisibleHotkeyReferenceItems()
            .Select(item => new HotkeyItem { Key = item.Key, Description = item.Description })
            .ToList();
        HelpHotkeysList.ItemsSource = items;
    }

    private void UpdatePreflightDisplay(PreflightCheckResult? report = null)
    {
        if (report == null) return;
        PreflightOverallStatusText.Text = report.OverallStatus.ToString();
    }

    private AppConfig CloneConfig(AppConfig source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private Brush GetStatusBrush(string status)
    {
        if (status.Contains("Healthy") || status.Contains("OK")) return Brushes.Green;
        if (status.Contains("Error") || status.Contains("Failed")) return Brushes.Red;
        return Brushes.Gray;
    }

    private class HotkeyItem
    {
        public string Key { get; set; } = "";
        public string Description { get; set; } = "";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
    }
}
