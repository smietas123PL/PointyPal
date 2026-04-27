using System;
using System.IO;
using System.Text.Json;

namespace PointyPal.Infrastructure;

public class StartupCrashLoopState
{
    public DateTime LastStartupAttemptAt { get; set; }
    public DateTime LastSuccessfulStartupAt { get; set; }
    public int ConsecutiveFailedStartups { get; set; }
    public DateTime? LastCrashAt { get; set; }
    public bool SafeModeAutoTriggered { get; set; }
    public string? LastSafeModeReason { get; set; }
}

public class StartupCrashLoopGuard
{
    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly string _stateFilePath;
    private StartupCrashLoopState _state = new();

    public StartupCrashLoopState State => _state;

    public StartupCrashLoopGuard(ConfigService configService, AppLogService? appLog = null)
    {
        _configService = configService;
        _appLog = appLog;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string stateDir = Path.Combine(appData, "PointyPal", "state");
        Directory.CreateDirectory(stateDir);
        _stateFilePath = Path.Combine(stateDir, "startup-state.json");

        LoadState();
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                string json = File.ReadAllText(_stateFilePath);
                _state = JsonSerializer.Deserialize<StartupCrashLoopState>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _appLog?.Warning("CrashLoopGuard", $"Failed to load startup state: {ex.Message}");
            _state = new();
        }
    }

    private void SaveState()
    {
        try
        {
            string json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _appLog?.Warning("CrashLoopGuard", $"Failed to save startup state: {ex.Message}");
        }
    }

    public void RecordStartupAttempt()
    {
        var config = _configService.Config;
        if (!config.CrashLoopDetectionEnabled) return;

        var now = DateTime.Now;
        
        // If last attempt was a long time ago, reset failure count?
        // Actually, the logic is: if we didn't reach "successful" state, it was a failure.
        // But if it was > X minutes ago, maybe it's a new context?
        // The prompt says "if failed startup count >= threshold, force Safe Mode".
        // Usually, we increment on startup, and decrement/reset on success.
        
        _state.LastStartupAttemptAt = now;
        _state.ConsecutiveFailedStartups++;
        
        SaveState();

        if (config.AutoSafeModeOnCrashLoop && _state.ConsecutiveFailedStartups >= config.CrashLoopFailureThreshold)
        {
            // Check window
            if ((now - _state.LastStartupAttemptAt).TotalMinutes <= config.CrashLoopWindowMinutes || _state.LastSuccessfulStartupAt == DateTime.MinValue)
            {
                TriggerSafeMode("Crash loop detected (consecutive failures exceeded threshold)");
            }
        }
    }

    public void RecordSuccessfulStartup()
    {
        _state.LastSuccessfulStartupAt = DateTime.Now;
        _state.ConsecutiveFailedStartups = 0;
        _state.SafeModeAutoTriggered = false;
        _state.LastSafeModeReason = null;
        SaveState();
    }

    private void TriggerSafeMode(string reason)
    {
        _state.SafeModeAutoTriggered = true;
        _state.LastSafeModeReason = reason;
        SaveState();
        
        _appLog?.Error("CrashLoopGuard", $"Forcing Safe Mode: {reason}");
        _configService.SetSafeMode(true, reason);
    }

    public void Reset()
    {
        _state.ConsecutiveFailedStartups = 0;
        _state.SafeModeAutoTriggered = false;
        _state.LastSafeModeReason = null;
        SaveState();
    }
}
