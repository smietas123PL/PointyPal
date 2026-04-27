using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PointyPal.Infrastructure;

public enum RcReadinessStatus
{
    Ready,
    ReadyWithWarnings,
    NotReady
}

public class RcReadinessResult
{
    public RcReadinessStatus Status { get; set; }
    public int Score { get; set; }
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class RcReadinessService
{
    private readonly ConfigService _configService;
    private readonly PreflightCheckService _preflight;
    private readonly SelfTestReportService _selfTest;
    private readonly ProviderHealthCheckService _health;
    private readonly ResilienceMonitorService _resilience;
    private readonly AppLogService? _appLog;

    public RcReadinessService(
        ConfigService configService,
        PreflightCheckService preflight,
        SelfTestReportService selfTest,
        ProviderHealthCheckService health,
        ResilienceMonitorService resilience,
        AppLogService? appLog = null)
    {
        _configService = configService;
        _preflight = preflight;
        _selfTest = selfTest;
        _health = health;
        _resilience = resilience;
        _appLog = appLog;
    }

    public async Task<RcReadinessResult> RunReadinessCheckAsync()
    {
        var result = new RcReadinessResult { Score = 100 };

        // 1. Preflight
        var preflightResults = await _preflight.RunAllChecksAsync();
        if (preflightResults.Items.Any(r => r.Status == PreflightStatus.Fail))
        {
            result.BlockingIssues.Add("Preflight check found failed items.");
            result.Score -= 40;
        }
        else if (preflightResults.Items.Any(r => r.Status == PreflightStatus.Warning))
        {
            result.Warnings.Add("Preflight check found warnings.");
            result.Score -= 10;
        }

        // 2. Self-Test
        var latestSelfTest = _selfTest.GetLatestReport();
        if (latestSelfTest == null)
        {
            result.Warnings.Add("No self-test report found. Run self-test before release.");
            result.Score -= 20;
        }
        else if (latestSelfTest.FailedScenarios > 0)
        {
            result.BlockingIssues.Add($"Self-test failed: {latestSelfTest.FailedScenarios} scenarios failed.");
            result.Score -= 30;
        }

        // 3. Worker Health
        if (_health.WorkerStatus != "Healthy")
        {
            result.Warnings.Add($"Worker status is {_health.WorkerStatus}. Verify worker connectivity.");
            result.Score -= 15;
        }

        // 4. Resilience
        var resSnapshot = _resilience.GetCurrentSnapshot();
        if (resSnapshot.Status != ResilienceStatus.Healthy)
        {
            result.Warnings.Add($"Resilience status is {resSnapshot.Status}. Recent provider failures detected.");
            result.Score -= 10;
        }
        
        if (resSnapshot.LastResourceWarningAt.HasValue && (DateTime.Now - resSnapshot.LastResourceWarningAt.Value).TotalHours < 1)
        {
            result.Warnings.Add($"Recent resource warning: {resSnapshot.LastResourceWarningMessage}");
            result.Score -= 5;
        }

        // 5. Provider Fallback
        if (_resilience.FallbackActive)
        {
            result.BlockingIssues.Add("Provider fallback is currently active. Real providers are failing.");
            result.Score -= 30;
        }

        // 6. Safe Mode
        if (_configService.SafeModeActive)
        {
            result.BlockingIssues.Add("Application is in Safe Mode. Cannot validate RC in Safe Mode.");
            result.Score -= 50;
        }

        // Calculate Status
        result.Score = Math.Max(0, result.Score);
        if (result.BlockingIssues.Count > 0 || result.Score < 50)
        {
            result.Status = RcReadinessStatus.NotReady;
            result.RecommendedActions.Add("Resolve all blocking issues before proceeding.");
        }
        else if (result.Warnings.Count > 0 || result.Score < 90)
        {
            result.Status = RcReadinessStatus.ReadyWithWarnings;
            result.RecommendedActions.Add("Review warnings and address them if possible.");
        }
        else
        {
            result.Status = RcReadinessStatus.Ready;
            result.RecommendedActions.Add("Ready for Release Candidate validation.");
        }

        if (_configService.Config.SaveDebugArtifacts)
        {
            SaveReport(result);
        }

        return result;
    }

    private void SaveReport(RcReadinessResult result)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            Directory.CreateDirectory(debugDir);
            string filePath = Path.Combine(debugDir, "rc-readiness-report.json");
            
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _appLog?.Warning("RcReadiness", $"Failed to save readiness report: {ex.Message}");
        }
    }
}
