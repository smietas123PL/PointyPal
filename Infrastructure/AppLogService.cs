using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PointyPal.Infrastructure;

public enum AppLogLevel
{
    Error = 0,
    Warning = 1,
    Info = 2,
    Debug = 3
}

public class AppLogService
{
    private const long MaxLogBytes = 1024 * 1024;
    private readonly Func<AppConfig> _configProvider;
    private readonly object _lock = new();

    public string LogDirectory { get; }
    public string LogPath { get; }
    public string LastErrorSummary { get; private set; } = "";

    public AppLogService(ConfigService configService)
        : this(() => configService.Config, GetDefaultLogDirectory())
    {
    }

    public AppLogService(AppConfig config, string logDirectory)
        : this(() => config, logDirectory)
    {
    }

    private AppLogService(Func<AppConfig> configProvider, string logDirectory)
    {
        _configProvider = configProvider;
        LogDirectory = logDirectory;
        LogPath = Path.Combine(LogDirectory, "pointypal.log");
    }

    public void Error(string eventName, string metadata = "") => Log(AppLogLevel.Error, eventName, metadata);

    public void Warning(string eventName, string metadata = "") => Log(AppLogLevel.Warning, eventName, metadata);

    public void Info(string eventName, string metadata = "") => Log(AppLogLevel.Info, eventName, metadata);

    public void Debug(string eventName, string metadata = "") => Log(AppLogLevel.Debug, eventName, metadata);

    public void Log(AppLogLevel level, string eventName, string metadata = "")
    {
        try
        {
            var config = _configProvider();
            if (!config.AppLoggingEnabled || level > ParseLevel(config.LogLevel))
            {
                return;
            }

            string cleanMetadata = Redact(metadata);
            string cleanEventName = Redact(eventName);
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {cleanEventName}";
            if (!string.IsNullOrWhiteSpace(cleanMetadata))
            {
                line += $" | {cleanMetadata}";
            }

            lock (_lock)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }

            if (level == AppLogLevel.Error)
            {
                LastErrorSummary = string.IsNullOrWhiteSpace(cleanMetadata)
                    ? cleanEventName
                    : $"{cleanEventName}: {cleanMetadata}";
            }
        }
        catch
        {
            // Local logging must never block app startup or interaction handling.
        }
    }

    public static AppLogLevel ParseLevel(string? value)
    {
        return Enum.TryParse<AppLogLevel>(value, ignoreCase: true, out var level)
            ? level
            : AppLogLevel.Info;
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string result = value;

        result = Regex.Replace(
            result,
            @"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+",
            "Bearer [REDACTED_SECRET]");

        result = Regex.Replace(
            result,
            @"(?i)\b(sk|pk|rk|ak|key)-[A-Za-z0-9_\-]{16,}",
            "[REDACTED_SECRET]");

        result = Regex.Replace(
            result,
            @"(?i)(api[_-]?key|x-api-key|x-pointypal-client-key|authorization|token|secret|password)(\s*[:=]\s*)([""']?)[^,\s;""']+\3?",
            "$1$2[REDACTED_SECRET]");

        result = Regex.Replace(
            result,
            @"(?i)(""?(?:audio|screenshot|image|payload)?base64""?\s*[:=]\s*[""']?)[A-Za-z0-9+/]{24,}={0,2}",
            "$1[REDACTED_BASE64]");

        result = Regex.Replace(
            result,
            @"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{160,}={0,2}(?![A-Za-z0-9+/])",
            "[REDACTED_BASE64]");

        return result;
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length < MaxLogBytes)
        {
            return;
        }

        string archivePath = Path.Combine(LogDirectory, "pointypal.log.1");
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(LogPath, archivePath);
    }

    private static string GetDefaultLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.AppName,
            "logs");
    }
}
