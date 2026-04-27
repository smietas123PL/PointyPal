using System;
using System.Drawing;
using System.Windows;
using PointyPal.Infrastructure;
using PointyPal.Input;
using PointyPal.Overlay;
using PointyPal.Core;
using System.IO;

namespace PointyPal.Tray;

public class TrayManager : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly CursorOverlayWindow _overlayWindow;
    private readonly PushToTalkService _pttService;
    private readonly ConfigService _configService;
    private readonly DebugArtifactCleanupService _cleanupService;
    private readonly InteractionCoordinator _coordinator;
    private readonly SelfTestReportService _selfTestReportService;
    private readonly StartupRegistrationService? _startupRegistrationService;
    private readonly AppLogService? _appLog;
    private readonly ResilienceMonitorService? _resilienceMonitor;
    private bool _isDisposed;

    public TrayManager(
        CursorOverlayWindow overlayWindow,
        PushToTalkService pttService,
        ConfigService configService,
        DebugArtifactCleanupService cleanupService,
        InteractionCoordinator coordinator,
        SelfTestReportService selfTestReportService,
        StartupRegistrationService? startupRegistrationService = null,
        AppLogService? appLog = null,
        ResilienceMonitorService? resilienceMonitor = null)
    {
        _overlayWindow = overlayWindow;
        _pttService = pttService;
        _configService = configService;
        _cleanupService = cleanupService;
        _coordinator = coordinator;
        _selfTestReportService = selfTestReportService;
        _startupRegistrationService = startupRegistrationService;
        _appLog = appLog;
        _resilienceMonitor = resilienceMonitor;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PointyPal Companion",
            Visible = true
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        
        // 1. Quick Ask & Primary
        var quickAskItem = new System.Windows.Forms.ToolStripMenuItem("Quick Ask");
        quickAskItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenQuickAsk();
        };
        quickAskItem.Font = new System.Drawing.Font(quickAskItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(quickAskItem);

        var openCCItem = new System.Windows.Forms.ToolStripMenuItem("Control Center");
        openCCItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        contextMenu.Items.Add(openCCItem);

        var setupWizardItem = new System.Windows.Forms.ToolStripMenuItem("Setup Wizard");
        setupWizardItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter(); // We open CC first then it can trigger wizard
        };
        contextMenu.Items.Add(setupWizardItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var toggleMenu = new System.Windows.Forms.ToolStripMenuItem("Toggle Settings");
        AddToggleItem(toggleMenu, "Voice Input", () => _configService.Config.VoiceInputEnabled, (v) => _configService.Config.VoiceInputEnabled = v);
        AddToggleItem(toggleMenu, "Screen Context", () => _configService.Config.ScreenshotEnabled, (v) => _configService.Config.ScreenshotEnabled = v);
        AddToggleItem(toggleMenu, "Voice Output", () => _configService.Config.TtsEnabled, (v) => _configService.Config.TtsEnabled = v);
        AddToggleItem(toggleMenu, "Pointer Flight", () => _configService.Config.PointerFlightEnabled, (v) => _configService.Config.PointerFlightEnabled = v);
        AddStartWithWindowsItem(toggleMenu);
        contextMenu.Items.Add(toggleMenu);

        var healthRefreshItem = new System.Windows.Forms.ToolStripMenuItem("Status");
        healthRefreshItem.Click += async (s, e) => {
            if (System.Windows.Application.Current is App app) await app.OpenHealthRefreshAsync();
        };
        contextMenu.Items.Add(healthRefreshItem);

        var helpMenu = new System.Windows.Forms.ToolStripMenuItem("Help");
        
        var releaseGuideItem = new System.Windows.Forms.ToolStripMenuItem("Open Release Guide");
        releaseGuideItem.Click += (s, e) => OpenDoc("docs/local-release.md");
        helpMenu.DropDownItems.Add(releaseGuideItem);

        var rcChecklistItem = new System.Windows.Forms.ToolStripMenuItem("Open RC Checklist");
        rcChecklistItem.Click += (s, e) => OpenDoc("docs/rc-checklist.md");
        helpMenu.DropDownItems.Add(rcChecklistItem);

        var hotkeysItem = new System.Windows.Forms.ToolStripMenuItem("Hotkeys Reference");
        hotkeysItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        helpMenu.DropDownItems.Add(hotkeysItem);

        var tutorialItem = new System.Windows.Forms.ToolStripMenuItem("Getting Started / Tutorials");
        tutorialItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        helpMenu.DropDownItems.Add(tutorialItem);

        contextMenu.Items.Add(helpMenu);

        if (_configService.Config.DeveloperModeEnabled || _configService.Config.ShowDeveloperTrayItems)
        {
            contextMenu.Items.Add(BuildDeveloperMenu());
        }

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit PointyPal");
        exitItem.Click += ExitItem_Click;
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += ToggleItem_Click;
    }

    private System.Windows.Forms.ToolStripMenuItem BuildDeveloperMenu()
    {
        var developerMenu = new System.Windows.Forms.ToolStripMenuItem("Developer");

        var diagnosticsItem = new System.Windows.Forms.ToolStripMenuItem("Show Diagnostics");
        diagnosticsItem.Click += (s, e) => _notifyIcon.ShowBalloonTip(2500, "PointyPal", "Use F9 in Developer Mode for full diagnostics.", System.Windows.Forms.ToolTipIcon.Info);
        developerMenu.DropDownItems.Add(diagnosticsItem);

        var selfTestItem = new System.Windows.Forms.ToolStripMenuItem("Run Self-Test");
        selfTestItem.Click += RunSelfTestItem_Click;
        developerMenu.DropDownItems.Add(selfTestItem);

        var preflightItem = new System.Windows.Forms.ToolStripMenuItem("Run Preflight");
        preflightItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        developerMenu.DropDownItems.Add(preflightItem);

        var readinessItem = new System.Windows.Forms.ToolStripMenuItem("Run RC Readiness");
        readinessItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        developerMenu.DropDownItems.Add(readinessItem);

        var replayItem = new System.Windows.Forms.ToolStripMenuItem("Replay Last Point");
        replayItem.Click += async (s, e) => await _coordinator.ReplayLastPointAsync();
        developerMenu.DropDownItems.Add(replayItem);

        var clearDebugItem = new System.Windows.Forms.ToolStripMenuItem("Clear Debug Files");
        clearDebugItem.Click += (s, e) => _cleanupService.CleanupAll();
        developerMenu.DropDownItems.Add(clearDebugItem);

        var openDebugItem = new System.Windows.Forms.ToolStripMenuItem("Open Debug Folder");
        openDebugItem.Click += (s, e) => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug"));
        developerMenu.DropDownItems.Add(openDebugItem);

        var openLogsItem = new System.Windows.Forms.ToolStripMenuItem("Open Logs Folder");
        openLogsItem.Click += (s, e) => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs"));
        developerMenu.DropDownItems.Add(openLogsItem);

        var calibrationItem = new System.Windows.Forms.ToolStripMenuItem("Calibration");
        calibrationItem.Click += (s, e) => _notifyIcon.ShowBalloonTip(2500, "PointyPal", "Use Ctrl+Shift+F9 with developer hotkeys enabled.", System.Windows.Forms.ToolTipIcon.Info);
        developerMenu.DropDownItems.Add(calibrationItem);

        var fakeInteractionItem = new System.Windows.Forms.ToolStripMenuItem("Fake Interaction");
        fakeInteractionItem.Click += (s, e) => _notifyIcon.ShowBalloonTip(2500, "PointyPal", "Use F12 with developer hotkeys enabled.", System.Windows.Forms.ToolTipIcon.Info);
        developerMenu.DropDownItems.Add(fakeInteractionItem);

        var providerOverridesItem = new System.Windows.Forms.ToolStripMenuItem("Provider Overrides");
        providerOverridesItem.Click += (s, e) => {
            if (System.Windows.Application.Current is App app) app.OpenControlCenter();
        };
        developerMenu.DropDownItems.Add(providerOverridesItem);

        return developerMenu;
    }

    private void AddToggleItem(System.Windows.Forms.ToolStripMenuItem menu, string text, Func<bool> getter, Action<bool> setter)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text);
        item.CheckOnClick = true;
        item.Checked = getter();
        item.Click += (s, e) => {
            setter(item.Checked);
            _configService.SaveConfig(_configService.Config);
        };
        menu.DropDownItems.Add(item);
    }

    private void AddStartWithWindowsItem(System.Windows.Forms.ToolStripMenuItem menu)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = _configService.Config.StartWithWindows
        };
        item.Click += (s, e) =>
        {
            bool requested = item.Checked;
            _configService.Config.StartWithWindows = requested;
            _configService.SaveConfig(_configService.Config);

            if (_startupRegistrationService == null)
            {
                return;
            }

            var result = _startupRegistrationService.ApplyConfig(_configService.Config);
            if (!result.Success)
            {
                item.Checked = !requested;
                _configService.Config.StartWithWindows = !requested;
                _configService.SaveConfig(_configService.Config);
                string error = result.ErrorMessage ?? "Unknown error";
                _appLog?.Warning("StartupToggleFailed", $"Error={error}");
                _notifyIcon.ShowBalloonTip(
                    4000,
                    "PointyPal",
                    $"Could not update Windows startup: {error}",
                    System.Windows.Forms.ToolTipIcon.Warning);
            }
        };
        menu.DropDownItems.Add(item);
    }

    private async void RunSelfTestItem_Click(object? sender, EventArgs e)
    {
        var menuItem = sender as System.Windows.Forms.ToolStripMenuItem;
        if (menuItem != null)
        {
            menuItem.Enabled = false;
        }

        try
        {
            var harness = new InteractionSimulationHarness(_configService);
            var result = await System.Threading.Tasks.Task.Run(() =>
                _selfTestReportService.RunAndSaveAsync(harness, SelfTestMode.Quick));

            string message = result.Passed
                ? "Self-test passed."
                : "Self-test failed. Open Control Center for details.";
            var icon = result.Passed
                ? System.Windows.Forms.ToolTipIcon.Info
                : System.Windows.Forms.ToolTipIcon.Warning;

            _notifyIcon.ShowBalloonTip(3000, "PointyPal", message, icon);
        }
        catch
        {
            _notifyIcon.ShowBalloonTip(3000, "PointyPal", "Self-test failed. Open Control Center for details.", System.Windows.Forms.ToolTipIcon.Warning);
        }
        finally
        {
            if (menuItem != null)
            {
                menuItem.Enabled = true;
            }
        }
    }

    private void OpenFolder(string path)
    {
        try {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
        } catch { /* ignore */ }
    }

    private void OpenFile(string path)
    {
        try {
            if (File.Exists(path)) {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        } catch { /* ignore */ }
    }

    private void OpenDoc(string relativePath)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath = Path.Combine(baseDir, relativePath);
            if (File.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
            }
        }
        catch { /* ignore */ }
    }

    private void ToggleItem_Click(object? sender, EventArgs e)
    {
        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
            _notifyIcon.ContextMenuStrip!.Items[1].Text = "Show Control Center"; // Index 1 is CC
        }
        else
        {
            _overlayWindow.Show();
            _notifyIcon.ContextMenuStrip!.Items[1].Text = "Hide Control Center";
        }
    }

    private void ExitItem_Click(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.RequestShutdown("Tray exit");
        }
        else
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    public void ShowAttention(string message)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(3000, "PointyPal", message, System.Windows.Forms.ToolTipIcon.Info);
        }
        catch
        {
            // Tray attention is best effort.
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _isDisposed = true;
        }
    }
}
