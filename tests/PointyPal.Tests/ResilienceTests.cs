using System;
using System.IO;
using System.Threading.Tasks;
using PointyPal.Infrastructure;
using PointyPal.Core;
using PointyPal.AI;
using Xunit;

namespace PointyPal.Tests;

public class ResilienceTests
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly AppLogService _logService;
    private readonly AppStateManager _stateManager;
    private readonly ProviderHealthCheckService _healthService;
    private readonly InteractionTimelineService _timelineService;

    public ResilienceTests()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "PointyPalTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        string configPath = Path.Combine(testDir, "config.json");
        
        _config = new AppConfig();
        File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(_config));
        
        _configService = new ConfigService(configPath);
        _logService = new AppLogService(_config, testDir);
        _stateManager = new AppStateManager();
        _healthService = new ProviderHealthCheckService(_configService, _logService);
        _timelineService = new InteractionTimelineService(_configService);
    }

    [Fact]
    public void ConsecutiveFailures_TriggerDegradedStatus()
    {
        // Arrange
        _configService.Config.ProviderFailureThreshold = 3;
        _configService.Config.EnableProviderFallback = true;
        _configService.Config.FallbackToFakeOnWorkerFailure = true;
        _configService.Config.AllowFakeProviderFallbackInNormalMode = true;
        
        var monitor = new ResilienceMonitorService(_configService, _stateManager, _healthService, _timelineService, _logService);
        
        // Act
        monitor.RecordProviderFailure("AI", "Error 1");
        monitor.RecordProviderFailure("AI", "Error 2");
        
        // Assert
        Assert.Equal(ResilienceStatus.Healthy, monitor.CurrentStatus);
        Assert.False(monitor.FallbackActive);

        // Third failure
        monitor.RecordProviderFailure("AI", "Error 3");
        Assert.Equal(ResilienceStatus.Degraded, monitor.CurrentStatus);
        Assert.True(monitor.FallbackActive);
    }

    [Fact]
    public void Success_ResetsFailuresAndFallback()
    {
        // Arrange
        _configService.Config.ProviderFailureThreshold = 2;
        _configService.Config.EnableProviderFallback = true;
        _configService.Config.FallbackToFakeOnWorkerFailure = true;
        _configService.Config.AllowFakeProviderFallbackInNormalMode = true;
        
        var monitor = new ResilienceMonitorService(_configService, _stateManager, _healthService, _timelineService, _logService);
        
        // Act
        monitor.RecordProviderFailure("AI", "Error 1");
        monitor.RecordProviderFailure("AI", "Error 2");
        Assert.Equal(ResilienceStatus.Degraded, monitor.CurrentStatus);
        Assert.True(monitor.FallbackActive);

        monitor.RecordInteractionSuccess();
        
        // Assert
        Assert.Equal(ResilienceStatus.Healthy, monitor.CurrentStatus);
        Assert.False(monitor.FallbackActive);
        Assert.Equal(0, monitor.ConsecutiveFailures);
    }

    [Fact]
    public void ResetFailures_ClearsState()
    {
        // Arrange
        var monitor = new ResilienceMonitorService(_configService, _stateManager, _healthService, _timelineService, _logService);
        monitor.RecordProviderFailure("AI", "Error");
        
        // Act
        monitor.ResetFailures();
        
        // Assert
        Assert.Equal(0, monitor.ConsecutiveFailures);
        Assert.Equal(ResilienceStatus.Healthy, monitor.CurrentStatus);
    }

    [Fact]
    public void NormalMode_BlocksFakeFallbackByDefault()
    {
        _configService.Config.ProviderFailureThreshold = 1;
        _configService.Config.EnableProviderFallback = true;
        _configService.Config.FallbackToFakeOnWorkerFailure = true;
        _configService.Config.AllowFakeProviderFallbackInNormalMode = false;

        var monitor = new ResilienceMonitorService(_configService, _stateManager, _healthService, _timelineService, _logService);

        monitor.RecordProviderFailure("AI", "Worker unavailable");

        Assert.Equal(ResilienceStatus.Degraded, monitor.CurrentStatus);
        Assert.False(monitor.FallbackActive);
        Assert.Contains(monitor.RecentEvents, e => e.Message == ProviderPolicyService.WorkerUnavailableFakeFallbackDisabledMessage);
    }
}
