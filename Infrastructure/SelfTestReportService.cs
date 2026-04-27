using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PointyPal.Core;

namespace PointyPal.Infrastructure;

public class SelfTestReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public SelfTestResult? LastResult { get; private set; }
    public string ReportPath { get; }
    public string LastReportPath { get; private set; } = "";
    public bool IsRunning { get; private set; }
    public SelfTestMode? LastMode { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler? StateChanged;

    public SelfTestReportService(ConfigService configService, AppLogService? appLog = null)
        : this(
            configService,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PointyPal",
                "debug",
                "self-test-report.json"),
            appLog)
    {
    }

    internal SelfTestReportService(ConfigService configService, string reportPath, AppLogService? appLog = null)
    {
        _configService = configService;
        _appLog = appLog;
        ReportPath = reportPath;
    }

    public async Task<SelfTestResult> RunAndSaveAsync(
        InteractionSimulationHarness harness,
        SelfTestMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!await _runGate.WaitAsync(0, cancellationToken))
        {
            return BuildFailedResult(mode, "Self-test is already running.");
        }

        try
        {
            MarkRunning(mode);

            SelfTestResult result = mode == SelfTestMode.Quick
                ? await harness.RunQuickAsync(cancellationToken)
                : await harness.RunFullAsync(cancellationToken);

            await SaveLatestAsync(result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = BuildFailedResult(mode, "Self-test cancelled.");
            await SaveLatestAsync(result, CancellationToken.None);
            return result;
        }
        catch (Exception ex)
        {
            var result = BuildFailedResult(mode, ex.Message);
            await SaveLatestAsync(result, CancellationToken.None);
            return result;
        }
        finally
        {
            if (IsRunning)
            {
                IsRunning = false;
                OnStateChanged();
            }

            _runGate.Release();
        }
    }

    public async Task SaveLatestAsync(SelfTestResult result, CancellationToken cancellationToken = default)
    {
        LastResult = result;
        LastMode = result.Mode;
        LastError = result.ErrorMessage ?? result.ScenarioResults.FirstOrDefault(s => !s.Passed)?.ErrorMessage;
        IsRunning = false;
        LastReportPath = "";
        _appLog?.Info(
            "SelfTestResult",
            $"Mode={result.Mode}; Passed={result.Passed}; Total={result.TotalScenarios}; Failed={result.FailedScenarios}; DurationMs={result.DurationMs}");

        if (!_configService.Config.SaveDebugArtifacts)
        {
            OnStateChanged();
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(result, JsonOptions);
            await File.WriteAllTextAsync(ReportPath, json, cancellationToken);
            LastReportPath = ReportPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastError = $"Failed to write self-test report: {ex.Message}";
            _appLog?.Warning("SelfTestReportWriteFailed", $"Error={ex.Message}");
        }

        OnStateChanged();
    }

    private static SelfTestResult BuildFailedResult(SelfTestMode mode, string errorMessage)
    {
        var now = DateTime.Now;
        return new SelfTestResult
        {
            StartedAt = now,
            CompletedAt = now,
            DurationMs = 0,
            Mode = mode,
            Passed = false,
            TotalScenarios = 0,
            PassedScenarios = 0,
            FailedScenarios = 0,
            ErrorMessage = errorMessage
        };
    }

    private void MarkRunning(SelfTestMode mode)
    {
        IsRunning = true;
        LastMode = mode;
        LastError = null;
        OnStateChanged();
    }

    public SelfTestResult? GetLatestReport()
    {
        if (LastResult != null) return LastResult;

        try
        {
            if (File.Exists(ReportPath))
            {
                string json = File.ReadAllText(ReportPath);
                return JsonSerializer.Deserialize<SelfTestResult>(json, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _appLog?.Warning("SelfTestReport", $"Failed to load latest report: {ex.Message}");
        }

        return null;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
