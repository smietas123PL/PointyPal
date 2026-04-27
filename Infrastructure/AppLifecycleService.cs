using System;
using System.Collections.Generic;

namespace PointyPal.Infrastructure;

public enum AppLifecycleDisposalStage
{
    KeyboardHooks = 0,
    MicrophoneCapture = 1,
    Playback = 2,
    OverlayWindows = 3,
    TrayIcon = 4,
    CancellationTokens = 5
}

public class AppLifecycleService
{
    private readonly Dictionary<AppLifecycleDisposalStage, List<Action>> _disposers = new();
    private readonly AppLogService? _appLog;
    private bool _shutdownStarted;

    public DateTime AppStartedAt { get; } = DateTime.Now;
    public TimeSpan Uptime => DateTime.Now - AppStartedAt;
    public string ShutdownReason { get; private set; } = "Running";
    public bool StartupCompleted { get; private set; }

    public AppLifecycleService(AppLogService? appLog = null)
    {
        _appLog = appLog;
    }

    public void MarkStartupStep(string stepName)
    {
        _appLog?.Debug("StartupStep", $"Step={stepName}");
    }

    public void MarkStarted()
    {
        StartupCompleted = true;
        _appLog?.Info("AppStarted", AppInfo.LogMetadata);
    }

    public void RequestShutdown(string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            ShutdownReason = reason;
        }
    }

    public void Register(AppLifecycleDisposalStage stage, Action dispose)
    {
        if (!_disposers.TryGetValue(stage, out var actions))
        {
            actions = new List<Action>();
            _disposers[stage] = actions;
        }

        actions.Add(dispose);
    }

    public void Shutdown(string reason)
    {
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        RequestShutdown(reason);
        _appLog?.Info("AppShutdown", $"Reason={ShutdownReason}; UptimeSeconds={Math.Round(Uptime.TotalSeconds)}");

        foreach (AppLifecycleDisposalStage stage in Enum.GetValues<AppLifecycleDisposalStage>())
        {
            if (!_disposers.TryGetValue(stage, out var actions))
            {
                continue;
            }

            foreach (var action in actions)
            {
                try
                {
                    action();
                    _appLog?.Debug("LifecycleDispose", $"Stage={stage}");
                }
                catch (Exception ex)
                {
                    _appLog?.Warning("LifecycleDisposeFailed", $"Stage={stage}; Error={ex.Message}");
                }
            }
        }
    }
}
