using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PointyPal.Core;
using Point = System.Windows.Point;

namespace PointyPal.Infrastructure;

public class ResilienceMonitorService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly AppStateManager _stateManager;
    private readonly ProviderHealthCheckService _healthService;
    private readonly InteractionTimelineService _timelineService;
    
    private readonly List<ResilienceEvent> _events = new();
    private readonly object _lock = new();
    private DateTime _lastInteractionTime = DateTime.MinValue;
    private int _consecutiveProviderFailures = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private bool _fallbackActive = false;
    private ResilienceStatus _currentStatus = ResilienceStatus.Healthy;
    
    private Timer? _resourceTimer;
    private DateTime _startTime = DateTime.Now;
    private int _interactionCountSinceStart = 0;
    
    private DateTime _lastCpuTime = DateTime.MinValue;
    private TimeSpan _lastCpuProcessTime = TimeSpan.Zero;
    private double _lastCpuUsage = 0;
    
    private DateTime? _lastResourceWarningAt;
    private string? _lastResourceWarningMessage;

    public ResilienceStatus CurrentStatus => _currentStatus;
    public int ConsecutiveFailures => _consecutiveProviderFailures;
    public bool FallbackActive => _fallbackActive;
    public DateTime LastInteractionTime => _lastInteractionTime;
    public IReadOnlyList<ResilienceEvent> RecentEvents 
    {
        get { lock (_lock) return _events.TakeLast(50).ToList(); }
    }

    public event Action? HealthStatusChanged;
    public event Action? DisplayTopologyChanged;
    public event Action<bool>? MicrophoneAvailabilityChanged;
    public event Action? PowerStateChanged;

    public ResilienceMonitorService(
        ConfigService configService,
        AppStateManager stateManager,
        ProviderHealthCheckService healthService,
        InteractionTimelineService timelineService,
        AppLogService? appLog = null)
    {
        _configService = configService;
        _stateManager = stateManager;
        _healthService = healthService;
        _timelineService = timelineService;
        _appLog = appLog;

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (_configService.Config.EnableResourceMonitoring)
        {
            StartResourceMonitoring();
        }

        RecordEvent("System", "Info", "Resilience monitor started.");
    }

    public void RecordInteractionSuccess()
    {
        _lastInteractionTime = DateTime.Now;
        _interactionCountSinceStart++;
        if (_consecutiveProviderFailures > 0)
        {
            _consecutiveProviderFailures = 0;
            UpdateStatus();
        }
    }

    public void RecordProviderFailure(string category, string message)
    {
        _consecutiveProviderFailures++;
        _lastFailureTime = DateTime.Now;
        
        RecordEvent(category, "Error", message);
        UpdateStatus();
    }

    public void ResetFailures()
    {
        _consecutiveProviderFailures = 0;
        _fallbackActive = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var config = _configService.Config;
        var oldStatus = _currentStatus;

        if (_consecutiveProviderFailures >= config.ProviderFailureThreshold)
        {
            _currentStatus = ResilienceStatus.Degraded;
            var providerPolicy = new ProviderPolicyService(_configService);
            if (providerPolicy.CanFallbackToFake())
            {
                _fallbackActive = true;
            }
            else
            {
                _fallbackActive = false;
                if (config.EnableProviderFallback && config.FallbackToFakeOnWorkerFailure)
                {
                    RecordEvent(
                        "ProviderFallback",
                        "Warning",
                        ProviderPolicyService.WorkerUnavailableFakeFallbackDisabledMessage);
                }
            }
        }
        else if (_consecutiveProviderFailures == 0)
        {
            _currentStatus = ResilienceStatus.Healthy;
            _fallbackActive = false;
        }

        if (oldStatus != _currentStatus)
        {
            _appLog?.Info("Resilience", $"Status changed from {oldStatus} to {_currentStatus}");
            HealthStatusChanged?.Invoke();
        }
    }

    public void RecordEvent(string category, string severity, string message, string? suggestedAction = null, Dictionary<string, string>? metadata = null)
    {
        var evt = new ResilienceEvent
        {
            Category = category,
            Severity = severity,
            Message = message,
            SuggestedAction = suggestedAction,
            Metadata = metadata
        };

        lock (_lock)
        {
            _events.Add(evt);
            if (_events.Count > 500) _events.RemoveAt(0);
        }

        if (_configService.Config.SaveDebugArtifacts)
        {
            AppendEventToFile(evt);
        }
    }

    private void AppendEventToFile(ResilienceEvent evt)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            Directory.CreateDirectory(debugDir);
            string filePath = Path.Combine(debugDir, "resilience-events.jsonl");

            string line = JsonSerializer.Serialize(evt) + Environment.NewLine;
            File.AppendAllText(filePath, line);
        }
        catch { /* ignore */ }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        RecordEvent("Power", "Info", $"Power mode changed: {e.Mode}");
        if (e.Mode == PowerModes.Suspend)
        {
            HandleSuspend("System suspend");
        }
        else if (e.Mode == PowerModes.Resume)
        {
            HandleResume();
        }
        PowerStateChanged?.Invoke();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        RecordEvent("Session", "Info", $"Session switch: {e.Reason}");
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            HandleSuspend("Session lock");
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            HandleResume();
        }
        PowerStateChanged?.Invoke();
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e)
    {
        RecordEvent("Display", "Info", "Display settings changed (topology change detected).");
        DisplayTopologyChanged?.Invoke();
    }

    private void HandleSuspend(string reason)
    {
        // Cancel active work
        _stateManager.SetState(CompanionState.FollowingCursor, reason);
    }

    private async void HandleResume()
    {
        RecordEvent("System", "Info", "Resuming from sleep/lock. Refreshing state...");
        
        // Wait a bit for network/devices to initialize
        await Task.Delay(2000);
        
        await _healthService.CheckWorkerAsync();
        
        // Trigger availability checks
        MicrophoneAvailabilityChanged?.Invoke(IsMicrophoneAvailable());
        DisplayTopologyChanged?.Invoke();
    }

    public bool IsMicrophoneAvailable()
    {
        try
        {
            return NAudio.Wave.WaveIn.DeviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public ResilienceSnapshot GetCurrentSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        double cpu = GetCpuUsage(process);
        uint gdi = NativeMethods.GetGuiResources(process.Handle, NativeMethods.GR_GDIOBJECTS);
        uint user = NativeMethods.GetGuiResources(process.Handle, NativeMethods.GR_USEROBJECTS);

        return new ResilienceSnapshot
        {
            Status = _currentStatus,
            ProcessWorkingSetMb = process.WorkingSet64 / 1024.0 / 1024.0,
            PrivateMemoryMb = process.PrivateMemorySize64 / 1024.0 / 1024.0,
            CpuUsagePercent = cpu,
            GdiObjectCount = (int)gdi,
            UserObjectCount = (int)user,
            HandleCount = process.HandleCount,
            ThreadCount = process.Threads.Count,
            AppUptime = DateTime.Now - _startTime,
            InteractionCountSinceStart = _interactionCountSinceStart,
            ConsecutiveProviderFailures = _consecutiveProviderFailures,
            MicrophoneAvailable = IsMicrophoneAvailable(),
            DisplayCount = GetDisplayCount(),
            LastResourceWarningAt = _lastResourceWarningAt,
            LastResourceWarningMessage = _lastResourceWarningMessage
        };
    }

    private double GetCpuUsage(Process process)
    {
        var now = DateTime.Now;
        var currentCpuTime = process.TotalProcessorTime;

        if (_lastCpuTime == DateTime.MinValue)
        {
            _lastCpuTime = now;
            _lastCpuProcessTime = currentCpuTime;
            return 0;
        }

        double elapsedSeconds = (now - _lastCpuTime).TotalSeconds;
        double cpuSeconds = (currentCpuTime - _lastCpuProcessTime).TotalSeconds;

        if (elapsedSeconds > 0)
        {
            _lastCpuUsage = (cpuSeconds / elapsedSeconds) * 100 / Environment.ProcessorCount;
        }

        _lastCpuTime = now;
        _lastCpuProcessTime = currentCpuTime;

        return Math.Max(0, Math.Min(100, _lastCpuUsage));
    }

    private int GetDisplayCount()
    {
        int count = 0;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
        {
            count++;
            return true;
        }, IntPtr.Zero);
        return count > 0 ? count : 1;
    }

    private void StartResourceMonitoring()
    {
        int interval = _configService.Config.ResourceMonitoringIntervalSeconds;
        if (interval < 5) interval = 60;
        _resourceTimer = new Timer(MonitorResources, null, TimeSpan.FromSeconds(interval), TimeSpan.FromSeconds(interval));
    }

    private void MonitorResources(object? state)
    {
        var snapshot = GetCurrentSnapshot();
        var config = _configService.Config;
        
        List<string> warnings = new();

        if (snapshot.ProcessWorkingSetMb > config.MemoryWarningThresholdMb)
            warnings.Add($"High memory: {snapshot.ProcessWorkingSetMb:F1} MB");
        
        if (snapshot.CpuUsagePercent > config.CpuWarningThresholdPercent)
            warnings.Add($"High CPU: {snapshot.CpuUsagePercent:F1}%");

        if (snapshot.GdiObjectCount > config.GdiObjectWarningThreshold)
            warnings.Add($"High GDI objects: {snapshot.GdiObjectCount}");

        if (snapshot.UserObjectCount > config.UserObjectWarningThreshold)
            warnings.Add($"High USER objects: {snapshot.UserObjectCount}");

        if (snapshot.HandleCount > config.HandleWarningThreshold)
            warnings.Add($"High handles: {snapshot.HandleCount}");

        if (snapshot.ThreadCount > config.ThreadWarningThreshold)
            warnings.Add($"High threads: {snapshot.ThreadCount}");

        if (warnings.Count > 0)
        {
            string message = string.Join(", ", warnings);
            if (message != _lastResourceWarningMessage || (DateTime.Now - (_lastResourceWarningAt ?? DateTime.MinValue)).TotalMinutes > 5)
            {
                _lastResourceWarningAt = DateTime.Now;
                _lastResourceWarningMessage = message;
                RecordEvent("Resource", "Warning", message, "Check for leaks or high load.");
                _appLog?.Warning("ResourceMonitor", message);
            }
        }

        if (config.SaveDebugArtifacts)
        {
            SaveSnapshotToFile(snapshot);
        }
    }

    private void SaveSnapshotToFile(ResilienceSnapshot snapshot)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            string filePath = Path.Combine(debugDir, "resilience-snapshot.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ignore */ }
    }

    public async Task<SoakTestReport> RunSoakTestAsync(int minutes, int intervalMs)
    {
        RecordEvent("SoakTest", "Info", $"Starting soak test for {minutes} minutes at {intervalMs}ms interval.");
        
        var startSnapshot = GetCurrentSnapshot();
        var report = new SoakTestReport
        {
            StartTime = DateTime.Now,
            TotalIterations = 0,
            PassedIterations = 0,
            FailedIterations = 0
        };

        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes));
        var sw = Stopwatch.StartNew();
        var interactionDurations = new List<double>();
        double maxMem = 0;
        int maxHandles = 0;
        int maxThreads = 0;
        int initialFallbackCount = _fallbackActive ? 1 : 0; // Simplified

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                report.TotalIterations++;
                var iterSw = Stopwatch.StartNew();
                
                try
                {
                    // Simulate offline interaction
                    await Task.Delay(100, cts.Token);
                    report.PassedIterations++;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    report.FailedIterations++;
                    report.Errors.Add($"Iteration {report.TotalIterations}: {ex.Message}");
                }

                iterSw.Stop();
                interactionDurations.Add(iterSw.Elapsed.TotalMilliseconds);
                
                var current = GetCurrentSnapshot();
                maxMem = Math.Max(maxMem, current.ProcessWorkingSetMb);
                maxHandles = Math.Max(maxHandles, current.HandleCount);
                maxThreads = Math.Max(maxThreads, current.ThreadCount);

                await Task.Delay(intervalMs, cts.Token);
            }
        }
        catch (OperationCanceledException) { }

        sw.Stop();
        var endSnapshot = GetCurrentSnapshot();

        report.EndTime = DateTime.Now;
        report.DurationMinutes = sw.Elapsed.TotalMinutes;
        report.AverageInteractionDurationMs = interactionDurations.Count > 0 ? interactionDurations.Average() : 0;
        
        if (interactionDurations.Count > 0)
        {
            var sorted = interactionDurations.OrderBy(x => x).ToList();
            report.P95InteractionDurationMs = sorted[(int)(sorted.Count * 0.95)];
        }

        report.MaxMemoryMb = maxMem;
        report.MemoryDeltaMb = endSnapshot.ProcessWorkingSetMb - startSnapshot.ProcessWorkingSetMb;
        report.MaxHandles = maxHandles;
        report.MaxThreads = maxThreads;
        // FallbackActivations and CancellationFailures would need more hooks in a real scenario
        
        if (_configService.Config.SaveDebugArtifacts)
        {
            SaveSoakReport(report);
        }

        RecordEvent("SoakTest", "Info", $"Soak test completed. Total: {report.TotalIterations}, Passed: {report.PassedIterations}");
        return report;
    }

    private void SaveSoakReport(SoakTestReport report)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            string filePath = Path.Combine(debugDir, "soak-test-report.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ignore */ }
    }

    public void ClearEvents()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _resourceTimer?.Dispose();
    }
}
