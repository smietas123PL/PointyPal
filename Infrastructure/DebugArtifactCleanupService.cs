using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PointyPal.Infrastructure;

public class DebugArtifactCleanupService
{
    private readonly ConfigService _configService;
    private readonly string _debugDir;
    private readonly string _logsDir;
    private readonly AppLogService? _appLog;

    public DebugArtifactCleanupService(ConfigService configService, AppLogService? appLog = null)
        : this(
            configService,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "debug"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "logs"),
            appLog)
    {
    }

    internal DebugArtifactCleanupService(
        ConfigService configService,
        string debugDir,
        string logsDir,
        AppLogService? appLog = null)
    {
        _configService = configService;
        _debugDir = debugDir;
        _logsDir = logsDir;
        _appLog = appLog;
    }

    public void RunStartupCleanup()
    {
        if (_configService.Config.AutoDeleteDebugArtifacts)
        {
            Task.Run(() => CleanupOldFiles(_configService.Config.DebugArtifactRetentionHours));
        }

        Task.Run(() => CleanupOldLogs(_configService.Config.LogRetentionDays));
    }

    public void CleanupAll()
    {
        Task.Run(() => {
            try
            {
                if (!Directory.Exists(_debugDir)) return;

                var files = Directory.GetFiles(_debugDir);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        // File might be locked, skip
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting file {file}: {ex.Message}");
                    }
                }

                _appLog?.Info("DebugFilesCleared", $"Directory={_debugDir}");
            }
            catch (Exception ex)
            {
                _appLog?.Warning("DebugFilesClearFailed", $"Error={ex.Message}");
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        });
    }

    public void CleanupOldFiles(int retentionHours)
    {
        try
        {
            if (!Directory.Exists(_debugDir)) return;

            var cutoff = DateTime.Now.AddHours(-retentionHours);
            var files = Directory.GetFiles(_debugDir);

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (IOException)
                {
                    // Locked
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting old file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during old files cleanup: {ex.Message}");
        }
    }

    public void CleanupOldLogs(int retentionDays)
    {
        CleanupOldLogs(retentionDays, DateTime.Now);
    }

    internal void CleanupOldLogs(int retentionDays, DateTime now)
    {
        try
        {
            if (retentionDays < 1 || !Directory.Exists(_logsDir)) return;

            var cutoff = now.AddDays(-retentionDays);
            var files = Directory.GetFiles(_logsDir, "*.log*");

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (IOException)
                {
                    // Locked
                }
                catch (Exception ex)
                {
                    _appLog?.Warning("OldLogDeleteFailed", $"File={Path.GetFileName(file)}; Error={ex.Message}");
                    Debug.WriteLine($"Error deleting old log {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _appLog?.Warning("OldLogCleanupFailed", $"Error={ex.Message}");
            Debug.WriteLine($"Error during old log cleanup: {ex.Message}");
        }
    }

    public void CleanupAllLogs()
    {
        try
        {
            if (!Directory.Exists(_logsDir)) return;

            foreach (var file in Directory.GetFiles(_logsDir, "*.log*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // File might be locked; skip it.
                }
                catch (Exception ex)
                {
                    _appLog?.Warning("LogDeleteFailed", $"File={Path.GetFileName(file)}; Error={ex.Message}");
                }
            }

            _appLog?.Info("LogsCleared", $"Directory={_logsDir}");
        }
        catch (Exception ex)
        {
            _appLog?.Warning("LogsClearFailed", $"Error={ex.Message}");
        }
    }

    public DebugFolderInfo GetFolderInfo()
    {
        var info = new DebugFolderInfo { Path = _debugDir };
        try
        {
            if (Directory.Exists(_debugDir))
            {
                var files = Directory.GetFiles(_debugDir).Select(f => new FileInfo(f)).ToList();
                info.FileCount = files.Count;
                info.TotalSize = files.Sum(f => f.Length);
            }
        }
        catch { /* ignore */ }
        return info;
    }
}

public class DebugFolderInfo
{
    public string Path { get; set; } = "";
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
}
