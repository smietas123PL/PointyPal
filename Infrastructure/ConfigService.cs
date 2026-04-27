using System;
using System.IO;
using System.Text.Json;

namespace PointyPal.Infrastructure;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig _config;
    private AppLogService? _appLog;

    public AppConfig Config => _config;
    public string ConfigPath => _configPath;
    public bool IsFirstRun { get; private set; }
    public DateTime? LastSaveTime { get; private set; }
    public bool SafeModeActive { get; private set; }
    public string SafeModeReason { get; private set; } = "";

    public ConfigService() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "config.json"))
    {
    }

    public ConfigService(string configPath)
    {
        _configPath = configPath;
        string? dir = Path.GetDirectoryName(_configPath);
        if (dir != null) Directory.CreateDirectory(dir);
        
        IsFirstRun = !File.Exists(_configPath);
        _config = LoadConfig();
    }

    public void SetAppLogService(AppLogService appLog)
    {
        _appLog = appLog;
        _appLog.Info("ConfigLoaded", $"Path={_configPath}; FirstRun={IsFirstRun}; SafeMode={SafeModeActive}");
    }

    public void SetSafeMode(bool active, string reason)
    {
        SafeModeActive = active;
        SafeModeReason = reason;
        _appLog?.Warning("SafeModeChanged", $"Active={active}; Reason={reason}");
    }

    private AppConfig LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    _appLog?.Info("ConfigLoaded", $"Path={_configPath}");
                    return config;
                }
            }
            catch (Exception ex)
            {
                _appLog?.Warning("ConfigLoadFailed", $"Path={_configPath}; Error={ex.Message}");
                SetSafeMode(true, $"Config load error: {ex.Message}");
            }
        }

        var defaultConfig = new AppConfig();
        if (File.Exists(_configPath))
        {
            // If it exists but we are here, it was corrupt
            try
            {
                string invalidPath = _configPath.Replace(".json", $".invalid-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(_configPath, invalidPath);
                _appLog?.Warning("ConfigMoved", $"Invalid config moved to {invalidPath}");
            }
            catch { }
        }
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    public void SaveConfig(AppConfig config)
    {
        try
        {
            _config = config;
            
            // Backup before saving
            try
            {
                var backupService = new ConfigBackupService(this, _appLog);
                backupService.CreateBackup();
            }
            catch (Exception ex)
            {
                _appLog?.Warning("BackupFailed", ex.Message);
            }

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            LastSaveTime = DateTime.Now;
            _appLog?.Info("ConfigSaved", $"Path={_configPath}");
        }
        catch (Exception ex)
        {
            _appLog?.Error("ConfigSaveFailed", $"Path={_configPath}; Error={ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public void ReloadConfig()
    {
        _config = LoadConfig();
    }

    public void ResetToDefaults()
    {
        var defaultConfig = new AppConfig();
        SaveConfig(defaultConfig);
    }

    public bool RestoreLatestBackup()
    {
        var backupService = new ConfigBackupService(this, _appLog);
        return backupService.RestoreLatest();
    }

    public void FactoryResetLocalState()
    {
        _appLog?.Warning("FactoryResetStarted", "Clearing all local state except config backups if possible.");
        
        string appData = Path.GetDirectoryName(_configPath) ?? "";
        if (string.IsNullOrEmpty(appData)) return;

        // Folders to clear
        string[] folders = { "debug", "logs", "history", "usage", "timeline", "feedback" };
        foreach (var folder in folders)
        {
            string path = Path.Combine(appData, folder);
            if (Directory.Exists(path))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(path)) File.Delete(file);
                    _appLog?.Info("FactoryResetFolderCleared", folder);
                }
                catch { }
            }
        }

        ResetToDefaults();
    }
}
