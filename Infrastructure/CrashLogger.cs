using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PointyPal.Infrastructure;

public class CrashLogger
{
    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly Func<string>? _stateProvider;
    private readonly Func<string?>? _activeInteractionIdProvider;
    private bool _installed;

    public string LogDirectory { get; }

    public string LatestCrashPath
    {
        get
        {
            try
            {
                return Directory.Exists(LogDirectory)
                    ? Directory.GetFiles(LogDirectory, "crash-*.log")
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }
    }

    public DateTime? LastCrashTimestamp
    {
        get
        {
            string path = LatestCrashPath;
            return string.IsNullOrWhiteSpace(path) ? null : File.GetLastWriteTime(path);
        }
    }

    public CrashLogger(
        ConfigService configService,
        AppLogService? appLog = null,
        Func<string>? stateProvider = null,
        Func<string?>? activeInteractionIdProvider = null)
        : this(
            configService,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppInfo.AppName,
                "logs"),
            appLog,
            stateProvider,
            activeInteractionIdProvider)
    {
    }

    internal CrashLogger(
        ConfigService configService,
        string logDirectory,
        AppLogService? appLog = null,
        Func<string>? stateProvider = null,
        Func<string?>? activeInteractionIdProvider = null)
    {
        _configService = configService;
        _appLog = appLog;
        _stateProvider = stateProvider;
        _activeInteractionIdProvider = activeInteractionIdProvider;
        LogDirectory = logDirectory;
    }

    public void Install(Application application)
    {
        if (_installed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _installed = true;
    }

    public string WriteCrashLog(Exception exception, string source)
    {
        try
        {
            if (!_configService.Config.CrashLoggingEnabled)
            {
                return "";
            }

            Directory.CreateDirectory(LogDirectory);
            string path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            string body = BuildCrashBody(exception, source);
            File.WriteAllText(path, body);
            _appLog?.Error("CrashLogged", $"Source={source}; Path={path}; ExceptionType={exception.GetType().FullName}");
            return path;
        }
        catch
        {
            return "";
        }
    }

    private string BuildCrashBody(Exception exception, string source)
    {
        string state = SafeGet(_stateProvider) ?? "Unavailable";
        string activeInteractionId = SafeGet(_activeInteractionIdProvider) ?? "";
        string stackTrace = exception.StackTrace ?? "";

        return AppLogService.Redact(string.Join(
            Environment.NewLine,
            $"Timestamp: {DateTime.Now:O}",
            $"Source: {source}",
            $"AppName: {AppInfo.AppName}",
            $"Version: {AppInfo.Version}",
            $"ReleaseLabel: {AppInfo.ReleaseLabel}",
            $"BuildChannel: {AppInfo.BuildChannel}",
            $"BuildDate: {AppInfo.BuildDate}",
            $"BaselineDate: {AppInfo.BaselineDate}",
            $"GitCommit: {AppInfo.GitCommit}",
            $"WorkerContractVersion: {AppInfo.WorkerContractVersion}",
            $"CurrentState: {state}",
            $"ActiveInteractionId: {activeInteractionId}",
            $"ExceptionType: {exception.GetType().FullName}",
            $"Message: {exception.Message}",
            "StackTrace:",
            stackTrace,
            ""));
    }

    private static string? SafeGet(Func<string>? provider)
    {
        try
        {
            return provider?.Invoke();
        }
        catch
        {
            return null;
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog(ex, "AppDomain.UnhandledException");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "DispatcherUnhandledException");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "TaskScheduler.UnobservedTaskException");
    }
}
