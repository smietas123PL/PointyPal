using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PointyPal.Infrastructure;

public class ConfigBackupService
{
    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly string _backupDir;

    public ConfigBackupService(ConfigService configService, AppLogService? appLog)
    {
        _configService = configService;
        _appLog = appLog;
        _backupDir = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "backups");
        Directory.CreateDirectory(_backupDir);
    }

    public void CreateBackup()
    {
        if (!File.Exists(_configService.ConfigPath)) return;

        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fileName = $"config-{timestamp}.json";
            string destPath = Path.Combine(_backupDir, fileName);

            File.Copy(_configService.ConfigPath, destPath, true);
            _appLog?.Info("ConfigBackupCreated", $"Path={destPath}");

            TrimBackups();
        }
        catch (Exception ex)
        {
            _appLog?.Error("ConfigBackupFailed", ex.Message);
        }
    }

    private void TrimBackups()
    {
        try
        {
            var files = Directory.GetFiles(_backupDir, "config-*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            int maxBackups = _configService.Config.MaxConfigBackups;
            if (files.Count > maxBackups)
            {
                foreach (var file in files.Skip(maxBackups))
                {
                    file.Delete();
                    _appLog?.Info("ConfigBackupTrimmed", $"Deleted={file.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            _appLog?.Warning("ConfigBackupTrimFailed", ex.Message);
        }
    }

    public string? GetLatestBackupPath()
    {
        return Directory.GetFiles(_backupDir, "config-*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .FirstOrDefault()?.FullName;
    }

    public int GetBackupCount()
    {
        return Directory.GetFiles(_backupDir, "config-*.json").Length;
    }

    public bool RestoreLatest()
    {
        string? latest = GetLatestBackupPath();
        if (latest == null) return false;

        try
        {
            File.Copy(latest, _configService.ConfigPath, true);
            _configService.ReloadConfig();
            _appLog?.Info("ConfigRestored", $"From={latest}");
            return true;
        }
        catch (Exception ex)
        {
            _appLog?.Error("ConfigRestoreFailed", ex.Message);
            return false;
        }
    }

    public string ExportBundle(bool includeUsage = false)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string bundleName = $"pointypal-config-bundle-{timestamp}.zip";
        string bundlePath = Path.Combine(_backupDir, bundleName);

        if (File.Exists(bundlePath)) File.Delete(bundlePath);

        using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(_configService.ConfigPath, "config.json");
            
            if (includeUsage)
            {
                string usagePath = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "usage.json");
                if (File.Exists(usagePath))
                {
                    archive.CreateEntryFromFile(usagePath, "usage.json");
                }
            }
        }

        _appLog?.Info("ConfigBundleExported", $"Path={bundlePath}");
        return bundlePath;
    }

    public bool ImportBundle(string zipPath)
    {
        if (!File.Exists(zipPath)) return false;

        try
        {
            // 1. Backup current
            CreateBackup();

            // 2. Extract config.json
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var entry = archive.GetEntry("config.json");
                if (entry == null) throw new Exception("Bundle does not contain config.json");

                entry.ExtractToFile(_configService.ConfigPath, true);
                
                var usageEntry = archive.GetEntry("usage.json");
                if (usageEntry != null)
                {
                    string usagePath = Path.Combine(Path.GetDirectoryName(_configService.ConfigPath) ?? "", "usage.json");
                    usageEntry.ExtractToFile(usagePath, true);
                }
            }

            // 3. Reload
            _configService.ReloadConfig();
            _appLog?.Info("ConfigBundleImported", $"From={zipPath}");
            return true;
        }
        catch (Exception ex)
        {
            _appLog?.Error("ConfigBundleImportFailed", ex.Message);
            return false;
        }
    }
}
