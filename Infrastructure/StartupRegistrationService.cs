using System;
using Microsoft.Win32;

namespace PointyPal.Infrastructure;

public interface IStartupRegistry
{
    string? GetValue(string subKey, string valueName);
    void SetValue(string subKey, string valueName, string value);
    void DeleteValue(string subKey, string valueName);
}

public class HkcuStartupRegistry : IStartupRegistry
{
    public void DeleteValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public string? GetValue(string subKey, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string subKey, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
        key?.SetValue(valueName, value, RegistryValueKind.String);
    }
}

public class StartupRegistrationResult
{
    public bool Success { get; set; }
    public bool Enabled { get; set; }
    public string Command { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

public class StartupRegistrationService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IStartupRegistry _registry;
    private readonly AppLogService? _appLog;

    public string ValueName { get; }

    public StartupRegistrationService(AppLogService? appLog = null)
        : this(new HkcuStartupRegistry(), AppInfo.AppName, appLog)
    {
    }

    internal StartupRegistrationService(IStartupRegistry registry, string valueName, AppLogService? appLog = null)
    {
        _registry = registry;
        ValueName = valueName;
        _appLog = appLog;
    }

    public bool IsRegistered()
    {
        try
        {
            string? value = _registry.GetValue(RunKeyPath, ValueName);
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            _appLog?.Warning("StartupRegistrationReadFailed", $"Error={ex.Message}");
            return false;
        }
    }

    public StartupRegistrationResult ApplyConfig(AppConfig config)
    {
        return SetEnabled(config.StartWithWindows, config.StartMinimizedToTray);
    }

    public StartupRegistrationResult SetEnabled(bool enabled, bool startMinimizedToTray = true)
    {
        string command = BuildStartupCommand(GetExecutablePath(), startMinimizedToTray);
        var result = new StartupRegistrationResult
        {
            Enabled = enabled,
            Command = command
        };

        try
        {
            if (enabled)
            {
                _registry.SetValue(RunKeyPath, ValueName, command);
                _appLog?.Info("StartupRegistrationEnabled", $"ValueName={ValueName}");
            }
            else
            {
                _registry.DeleteValue(RunKeyPath, ValueName);
                _appLog?.Info("StartupRegistrationDisabled", $"ValueName={ValueName}");
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _appLog?.Error("StartupRegistrationFailed", $"Enabled={enabled}; Error={ex.Message}");
        }

        return result;
    }

    public static string BuildStartupCommand(string executablePath, bool startMinimizedToTray = true)
    {
        string escapedPath = executablePath.Replace("\"", "\\\"");
        string command = $"\"{escapedPath}\"";
        if (startMinimizedToTray)
        {
            command += " --minimized";
        }

        return command;
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? "";
    }
}
