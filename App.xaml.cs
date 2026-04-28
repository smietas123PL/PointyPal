using System;
using System.IO;
using System.Linq;
using System.Windows;
using PointyPal.AI;
using PointyPal.Capture;
using PointyPal.Core;
using PointyPal.Infrastructure;
using PointyPal.Input;
using PointyPal.Overlay;
using PointyPal.Tray;

namespace PointyPal;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstanceService;
    private AppLifecycleService? _lifecycleService;
    private AppLogService? _appLogService;
    private CrashLogger? _crashLogger;
    private StartupRegistrationService? _startupRegistrationService;
    private AppStateManager? _stateManager;
    private PushToTalkService? _pttService;
    private Voice.MicrophoneCaptureService? _micService;
    private Voice.AudioPlaybackService? _audioService;
    private CursorOverlayWindow? _overlayWindow;
    private TrayManager? _trayManager;
    private ConfigService? _configService;
    private UsageTracker? _usageTracker;
    private ProviderHealthCheckService? _healthService;
    private SelfTestReportService? _selfTestReportService;
    private InteractionTimelineService? _timelineService;
    private PerformanceSummaryService? _performanceSummaryService;
    private InteractionCoordinator? _coordinator;
    private DebugArtifactCleanupService? _cleanupService;
    private ResilienceMonitorService? _resilienceMonitor;
    private StartupCrashLoopGuard? _crashLoopGuard;
    private PreflightCheckService? _preflightService;
    private RcReadinessService? _readinessService;
    private RcValidationService? _validationService;
    private PointerQualityService? _qualityService;

    internal InteractionCoordinator? Coordinator => _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any(arg => arg.Equals("--version", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleOutput.WriteLine(AppInfo.VersionCliText);
            Shutdown(0);
            return;
        }

        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.TryAcquirePrimary(OnSecondInstanceActivated))
        {
            _singleInstanceService.Dispose();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _stateManager = new AppStateManager();
        _configService = new ConfigService();
        _appLogService = new AppLogService(_configService);
        _crashLoopGuard = new StartupCrashLoopGuard(_configService, _appLogService);

        bool forceSafeMode = false;
        bool runSelfTest = false;
        bool runSoakTest = false;
        string soakScenario = "quick";
        int soakMinutes = 10;
        int soakInterval = 1000;

        for (int i = 0; i < e.Args.Length; i++)
        {
            var arg = e.Args[i];
            if (arg.Equals("--safe-mode", StringComparison.OrdinalIgnoreCase)) forceSafeMode = true;
            if (arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)) runSelfTest = true;
            if (arg.Equals("--soak-test", StringComparison.OrdinalIgnoreCase)) runSoakTest = true;
            if (arg.Equals("--soak-test-scenario", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                soakScenario = e.Args[++i];
            }
            if (arg.Equals("--soak-test-minutes", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                int.TryParse(e.Args[++i], out soakMinutes);
            }
            if (arg.Equals("--soak-test-interval-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                int.TryParse(e.Args[++i], out soakInterval);
            }

            // Build020: Recovery commands
            if (arg.Equals("--reset-safe-mode", StringComparison.OrdinalIgnoreCase))
            {
                ResetSafeMode();
                return;
            }
            if (arg.Equals("--backup-config", StringComparison.OrdinalIgnoreCase))
            {
                BackupConfig();
                return;
            }
            if (arg.Equals("--restore-latest-config", StringComparison.OrdinalIgnoreCase))
            {
                RestoreLatestConfig();
                return;
            }
            if (arg.Equals("--factory-reset-local-state", StringComparison.OrdinalIgnoreCase))
            {
                bool confirmed = (i + 1 < e.Args.Length && e.Args[i + 1].Equals("--confirm", StringComparison.OrdinalIgnoreCase));
                FactoryResetLocalState(confirmed);
                return;
            }
            if (arg.Equals("--validate-rc", StringComparison.OrdinalIgnoreCase))
            {
                RunCommandLineValidation();
                return;
            }
        }

        _crashLoopGuard.RecordStartupAttempt();

        if (forceSafeMode || _configService.Config.ForceSafeMode || _configService.SafeModeActive)
        {
            string reason = forceSafeMode ? "Command-line argument" : (_configService.SafeModeActive ? _configService.SafeModeReason : "Config flag");
            _configService.SetSafeMode(true, reason);
        }
        _appLogService = new AppLogService(_configService);
        _configService.SetAppLogService(_appLogService);
        _lifecycleService = new AppLifecycleService(_appLogService);
        _startupRegistrationService = new StartupRegistrationService(_appLogService);

        _crashLogger = new CrashLogger(
            _configService,
            _appLogService,
            () => _stateManager?.CurrentState.ToString() ?? "Unavailable",
            () => _timelineService?.ActiveTimelineId);
        _crashLogger.Install(this);

        _lifecycleService.MarkStartupStep("Core services");
        _usageTracker = new UsageTracker();
        _healthService = new ProviderHealthCheckService(_configService, _appLogService);
        _selfTestReportService = new SelfTestReportService(_configService, _appLogService);
        _timelineService = new InteractionTimelineService(_configService);
        _performanceSummaryService = new PerformanceSummaryService(_configService, _appLogService);
        _qualityService = new PointerQualityService();
        var debugLogger = new DebugLogger(_configService);
        _cleanupService = new DebugArtifactCleanupService(_configService, _appLogService);
        _cleanupService.RunStartupCleanup();

        _resilienceMonitor = new ResilienceMonitorService(
            _configService, _stateManager, _healthService, _timelineService, _appLogService);

        _preflightService = new PreflightCheckService(_configService, _appLogService, _healthService);
        _readinessService = new RcReadinessService(
            _configService, _preflightService, _selfTestReportService, _healthService, _resilienceMonitor, _appLogService);
        _validationService = new RcValidationService(
            _configService, _selfTestReportService, _preflightService!, _readinessService, _appLogService);

        _lifecycleService.MarkStartupStep("Interaction services");
        var captureService = new ScreenCaptureService();
        var mapper = new CoordinateMapper();
        var parser = new PointTagParser();
        var payloadBuilder = new PromptPayloadBuilder();

        IAiResponseProvider fakeProvider = new FakeAiResponseProvider();
        IAiResponseProvider claudeProvider = new ClaudeVisionResponseProvider(_configService.Config);
        var uiAutomationService = new UiAutomationContextService(_configService);
        var validationService = new PointValidationService(_configService);
        var responseValidator = new AiResponseValidator();
        var historyService = new InteractionHistoryService(_configService);
        var providerPolicy = new ProviderPolicyService(_configService);
        var hotkeyPolicy = new HotkeyPolicyService(_configService);

        Voice.ITranscriptProvider fakeTranscriptProvider = new Voice.FakeTranscriptProvider(_configService.Config.FakeTranscriptText);
        Voice.ITranscriptProvider workerTranscriptProvider = new Voice.WorkerTranscriptProvider(_configService.Config);
        Voice.ITranscriptProvider transcriptProvider = providerPolicy.GetEffectiveTranscriptProvider() == "Fake"
            ? fakeTranscriptProvider
            : workerTranscriptProvider;
        Voice.ITtsProvider workerTtsProvider = new Voice.WorkerTtsProvider(_configService);
        var fakeTtsProvider = new Voice.FakeTtsProvider();

        if (_configService.SafeModeActive)
        {
            // Safe Mode: Force fakes
            claudeProvider = new FakeAiResponseProvider(); // Use fake for Claude too
            workerTtsProvider = fakeTtsProvider;
        }

        _audioService = new Voice.AudioPlaybackService();

        _coordinator = new InteractionCoordinator(
            _stateManager, captureService, mapper, parser, payloadBuilder,
            uiAutomationService,
            fakeProvider, claudeProvider, _configService,
            validationService, responseValidator,
            fakeTtsProvider, workerTtsProvider, _audioService,
            _usageTracker, debugLogger, historyService,
            _timelineService, _performanceSummaryService, _qualityService!, _resilienceMonitor, _appLogService, providerPolicy);

        var coordinator = _coordinator;

        _micService = new Voice.MicrophoneCaptureService();
        _pttService = new PushToTalkService(
            _stateManager, _micService, transcriptProvider,
            _configService, coordinator, _usageTracker, debugLogger, _timelineService, _resilienceMonitor,
            providerPolicy, hotkeyPolicy, fakeTranscriptProvider, workerTranscriptProvider);

        _pttService.InteractionRequested += async (input, providerOverride) =>
        {
            await coordinator.StartInteractionAsync(input, providerOverride);
        };

        _pttService.UiCaptureRequested += async (s, e) =>
        {
            await coordinator.PerformManualUiCaptureAsync();
        };

        _pttService.CalibrationToggled += (s, e) =>
        {
            _configService.Config.ShowCalibrationGrid = !_configService.Config.ShowCalibrationGrid;
            _configService.SaveConfig(_configService.Config);
        };

        _pttService.RatingSubmitted += rating =>
        {
            coordinator.SubmitManualRating(rating);
        };

        _pttService.QuickAskRequested += (s, e) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(OpenQuickAsk);
        };

        _lifecycleService.MarkStartupStep("Overlay and tray");
        _overlayWindow = new CursorOverlayWindow(
            _stateManager, _pttService, coordinator,
            _configService, _usageTracker, _cleanupService, _healthService, _selfTestReportService,
            _timelineService, _performanceSummaryService,
            _qualityService!,
            _lifecycleService, _startupRegistrationService, _appLogService, _crashLogger, _singleInstanceService,
            _resilienceMonitor);
        _trayManager = new TrayManager(
            _overlayWindow,
            _pttService,
            _configService,
            _cleanupService,
            coordinator,
            _selfTestReportService,
            _startupRegistrationService,
            _appLogService,
            _resilienceMonitor);

        RegisterLifecycleDisposers();

        _overlayWindow.Show();

        if (runSelfTest)
        {
            _appLogService?.Info("SelfTestMode", "Running minimal self-test and exiting.");
            RunCommandLineSelfTest();
            return;
        }

        if (runSoakTest)
        {
            _appLogService?.Info("SoakTestMode", $"Running soak test for {soakMinutes} minutes.");
            RunCommandLineSoakTest(soakMinutes, soakInterval);
            return;
        }

        if (_configService.IsFirstRun || (_configService.Config.ShowSetupWizardOnStartup && !_configService.Config.SetupWizardCompleted))
        {
            OpenControlCenter();
        }

        _lifecycleService.MarkStarted();
        _crashLoopGuard.RecordSuccessfulStartup();
    }

    private async void RunCommandLineSelfTest()
    {
        if (_coordinator == null) return;
        
        try
        {
            await _coordinator.RunSelfTestAsync(SelfTestMode.Quick);
            _appLogService?.Info("SelfTestPassed", "Command-line self-test successful.");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-Test Failed: {ex.Message}");
            _appLogService?.Error("SelfTestFailed", ex.Message);
            Shutdown(1);
        }
    }

    private async void RunCommandLineSoakTest(int minutes, int intervalMs)
    {
        if (_resilienceMonitor == null) return;
        
        try
        {
            await _resilienceMonitor.RunSoakTestAsync(minutes, intervalMs);
            _appLogService?.Info("SoakTestFinished", "Command-line soak test finished.");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Soak-Test Failed: {ex.Message}");
            _appLogService?.Error("SoakTestFailed", ex.Message);
            Shutdown(1);
        }
    }

    public void OpenControlCenter()
    {
        if (_configService == null || _usageTracker == null || _healthService == null ||
            _selfTestReportService == null || _timelineService == null || _performanceSummaryService == null) return;

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is UI.ControlCenterWindow)
            {
                window.Activate();
                return;
            }
        }

        var cc = new UI.ControlCenterWindow(
            _configService,
            _usageTracker,
            _healthService,
            _pttService,
            _selfTestReportService,
            _timelineService,
            _performanceSummaryService,
            _startupRegistrationService,
            _cleanupService,
            _appLogService,
            _crashLogger,
            _lifecycleService,
            _singleInstanceService,
            _resilienceMonitor,
            _crashLoopGuard,
            _preflightService,
            _readinessService,
            _validationService,
            _qualityService);
        cc.Show();
    }

    public void OpenQuickAsk()
    {
        if (_coordinator == null || _configService == null) return;

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is UI.QuickAskWindow)
            {
                window.Activate();
                return;
            }
        }

        var qa = new UI.QuickAskWindow(_coordinator, _configService);
        qa.Show();
    }

    public void RequestShutdown(string reason = "User requested")
    {
        _lifecycleService?.Shutdown(reason);
        Shutdown(0);
    }

    public async Task OpenHealthRefreshAsync()
    {
        if (_healthService == null) return;
        await _healthService.CheckAllAsync();
        _trayManager?.ShowAttention($"Health check finished. Status: {_healthService.WorkerStatus}");
    }

    private void RegisterLifecycleDisposers()
    {
        if (_lifecycleService == null)
        {
            return;
        }

        _lifecycleService.Register(AppLifecycleDisposalStage.KeyboardHooks, () => _pttService?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.MicrophoneCapture, () => _micService?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.Playback, () => _audioService?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.OverlayWindows, () => _overlayWindow?.Close());
        _lifecycleService.Register(AppLifecycleDisposalStage.TrayIcon, () => _trayManager?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.CancellationTokens, () => _coordinator?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.CancellationTokens, () => _resilienceMonitor?.Dispose());
        _lifecycleService.Register(AppLifecycleDisposalStage.CancellationTokens, () => _singleInstanceService?.Dispose());
    }

    private void OnSecondInstanceActivated()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _appLogService?.Info(
                "SecondInstanceDetected",
                $"Count={_singleInstanceService?.SecondInstanceDetectedCount ?? 0}");
            OpenControlCenter();
            _trayManager?.ShowAttention("PointyPal is already running. Control Center is open.");
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        string reason = _lifecycleService?.ShutdownReason == "Running"
            ? "Application exit"
            : _lifecycleService?.ShutdownReason ?? "Application exit";
        _lifecycleService?.Shutdown(reason);

        base.OnExit(e);
    }

    private void ResetSafeMode()
    {
        if (_configService == null || _crashLoopGuard == null) return;
        _configService.Config.ForceSafeMode = false;
        _configService.SaveConfig(_configService.Config);
        _crashLoopGuard.Reset();
        Console.WriteLine("Safe Mode flags and crash-loop marker cleared.");
        Shutdown(0);
    }

    private void BackupConfig()
    {
        if (_configService == null) return;
        var backupService = new ConfigBackupService(_configService, _appLogService);
        backupService.CreateBackup();
        Console.WriteLine("Config backup created.");
        Shutdown(0);
    }

    private void RestoreLatestConfig()
    {
        if (_configService == null) return;
        var backupService = new ConfigBackupService(_configService, _appLogService);
        if (backupService.RestoreLatest())
        {
            Console.WriteLine("Latest config backup restored.");
            Shutdown(0);
        }
        else
        {
            Console.WriteLine("No config backups found.");
            Shutdown(1);
        }
    }

    private void FactoryResetLocalState(bool confirmed)
    {
        if (!confirmed)
        {
            Console.WriteLine("WARNING: This will delete all local settings, history, and logs.");
            Console.WriteLine("To proceed, run with: --factory-reset-local-state --confirm");
            Shutdown(1);
            return;
        }

        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal");
            if (Directory.Exists(appData))
            {
                // We can't delete everything while running, but we can delete subdirs
                foreach (var dir in Directory.GetDirectories(appData))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
                foreach (var file in Directory.GetFiles(appData))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            Console.WriteLine("Local state reset successfully.");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Factory reset failed: {ex.Message}");
            Shutdown(1);
        }
    }

    private void RunCommandLineValidation()
    {
        if (_configService == null || _selfTestReportService == null || _healthService == null || _resilienceMonitor == null)
        {
            // Re-initialize what's needed for offline validation
            _configService = new ConfigService();
            _appLogService = new AppLogService(_configService);
            _healthService = new ProviderHealthCheckService(_configService, _appLogService);
            _selfTestReportService = new SelfTestReportService(_configService, _appLogService);
            _timelineService = new InteractionTimelineService(_configService);
            _resilienceMonitor = new ResilienceMonitorService(_configService, null, _healthService, _timelineService, _appLogService);
            _preflightService = new PreflightCheckService(_configService, _appLogService, _healthService);
            _readinessService = new RcReadinessService(_configService, _preflightService, _selfTestReportService, _healthService, _resilienceMonitor, _appLogService);
            _validationService = new RcValidationService(_configService, _selfTestReportService, _preflightService, _readinessService, _appLogService);
        }

        try
        {
            Console.WriteLine("Running offline RC validation...");
            var task = _validationService!.RunFullValidationAsync(runLatency: false, runSoak: false);
            task.Wait();
            var report = task.Result;

            Console.WriteLine($"Overall Status: {report.OverallStatus}");
            foreach (var issue in report.BlockingIssues)
            {
                Console.WriteLine($"[BLOCKER] {issue}");
            }
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"[WARNING] {warning}");
            }

            if (report.OverallStatus == RcValidationStatus.Fail)
            {
                Shutdown(1);
            }
            else
            {
                Console.WriteLine("Validation passed (with warnings if any).");
                Shutdown(0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation Error: {ex.Message}");
            Shutdown(1);
        }
    }
}
