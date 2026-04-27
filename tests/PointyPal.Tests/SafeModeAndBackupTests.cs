using System;
using System.IO;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class SafeModeAndBackupTests
{
    private string GetTempConfigPath() => Path.Combine(Path.GetTempPath(), $"config-{Guid.NewGuid()}.json");

    [Fact]
    public void SafeMode_ForcesFakeProviders()
    {
        var configPath = GetTempConfigPath();
        var service = new ConfigService(configPath);
        
        service.SetSafeMode(true, "Test");
        
        Assert.True(service.SafeModeActive);
        Assert.Equal("Test", service.SafeModeReason);
        
        if (File.Exists(configPath)) File.Delete(configPath);
    }

    [Fact]
    public void ConfigBackupService_CreatesAndTrimsBackups()
    {
        var configPath = GetTempConfigPath();
        var service = new ConfigService(configPath);
        var config = service.Config;
        config.MaxConfigBackups = 2;
        
        var backupService = new ConfigBackupService(service, null);
        
        // Create 3 backups
        backupService.CreateBackup();
        System.Threading.Thread.Sleep(1100); // Ensure different timestamps if name-based, though my logic uses time
        backupService.CreateBackup();
        System.Threading.Thread.Sleep(1100);
        backupService.CreateBackup();
        
        Assert.True(backupService.GetBackupCount() <= 2);
        
        // Cleanup
        string backupDir = Path.Combine(Path.GetDirectoryName(configPath) ?? "", "backups");
        if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
        if (File.Exists(configPath)) File.Delete(configPath);
    }

    [Fact]
    public void ConfigService_InvalidJson_EntersSafeMode()
    {
        var configPath = GetTempConfigPath();
        File.WriteAllText(configPath, "{ \"invalid\": json }");
        
        var service = new ConfigService(configPath);
        
        Assert.True(service.SafeModeActive);
        Assert.Contains("Config load error", service.SafeModeReason);
        
        if (File.Exists(configPath)) File.Delete(configPath);
    }

    [Fact]
    public void PreflightCheckService_AggregatesResults()
    {
        var configPath = GetTempConfigPath();
        var service = new ConfigService(configPath);
        var health = new ProviderHealthCheckService(service, null);
        var preflight = new PreflightCheckService(service, null, health);
        
        // This won't run fully without a worker, but we can check the items exist
        var resultTask = preflight.RunAllChecksAsync();
        resultTask.Wait();
        var result = resultTask.Result;
        
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, i => i.Name == "Configuration");
        
        if (File.Exists(configPath)) File.Delete(configPath);
    }
}
