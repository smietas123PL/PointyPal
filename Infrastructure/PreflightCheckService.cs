using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PointyPal.Core;
using PointyPal.Voice;

namespace PointyPal.Infrastructure;

public enum PreflightStatus
{
    Pass,
    Warning,
    Fail,
    Skipped
}

public class PreflightCheckItem
{
    public string Name { get; set; } = "";
    public PreflightStatus Status { get; set; } = PreflightStatus.Skipped;
    public string Message { get; set; } = "";
    public string? FixHint { get; set; }
}

public class PreflightCheckResult
{
    public List<PreflightCheckItem> Items { get; set; } = new();
    public DateTime RunTime { get; set; }
    public TimeSpan Duration { get; set; }
    public PreflightStatus OverallStatus { get; set; }
}

public class PreflightCheckService
{
    private readonly ConfigService _configService;
    private readonly AppLogService? _appLog;
    private readonly ProviderHealthCheckService _healthService;

    public PreflightCheckService(ConfigService configService, AppLogService? appLog, ProviderHealthCheckService healthService)
    {
        _configService = configService;
        _appLog = appLog;
        _healthService = healthService;
    }

    public async Task<PreflightCheckResult> RunAllChecksAsync()
    {
        var start = DateTime.Now;
        var result = new PreflightCheckResult { RunTime = start };
        
        // 1. Config Valid
        result.Items.Add(CheckConfigValid());

        // 2. Worker Base URL
        result.Items.Add(CheckWorkerBaseUrl());

        // 3. Worker Reachability
        result.Items.Add(await CheckWorkerHealthAsync());

        // 4. AI Provider
        result.Items.Add(CheckAiProvider());

        // 5. STT Provider
        result.Items.Add(CheckSttProvider());

        // 6. TTS Provider
        result.Items.Add(CheckTtsProvider());

        // 7. ElevenLabs Voice ID
        result.Items.Add(CheckElevenLabsVoice());

        // 8. Microphone
        result.Items.Add(CheckMicrophone());

        // 9. Folders Writable
        result.Items.Add(CheckFoldersWritable());

        // 10. Startup Registration
        result.Items.Add(CheckStartupRegistration());

        result.Duration = DateTime.Now - start;
        
        if (result.Items.Any(i => i.Status == PreflightStatus.Fail))
            result.OverallStatus = PreflightStatus.Fail;
        else if (result.Items.Any(i => i.Status == PreflightStatus.Warning))
            result.OverallStatus = PreflightStatus.Warning;
        else
            result.OverallStatus = PreflightStatus.Pass;

        _appLog?.Info("PreflightCompleted", $"Status={result.OverallStatus}; Duration={result.Duration.TotalMilliseconds}ms");
        
        if (_configService.Config.SaveDebugArtifacts)
        {
            SaveReport(result);
        }

        return result;
    }

    private PreflightCheckItem CheckConfigValid()
    {
        bool isSafe = _configService.SafeModeActive;
        return new PreflightCheckItem
        {
            Name = "Configuration",
            Status = isSafe ? PreflightStatus.Warning : PreflightStatus.Pass,
            Message = isSafe ? $"{UserMessages.SafeMode} is active: {_configService.SafeModeReason}" : "Config loaded successfully.",
            FixHint = isSafe ? "Review config.json or check logs for errors." : null
        };
    }

    private PreflightCheckItem CheckWorkerBaseUrl()
    {
        var policy = new ProviderPolicyService(_configService);
        var config = _configService.Config;
        bool configuredReal =
            config.AiProvider == "Claude" ||
            config.TranscriptProvider == "Worker" ||
            (config.TtsEnabled && config.TtsProvider == "Worker");
        bool isReal = configuredReal ||
                      (policy.CanUseRealProviders() &&
                       (policy.GetEffectiveAiProvider() == "Claude" ||
                        policy.GetEffectiveTranscriptProvider() == "Worker" ||
                        policy.GetEffectiveTtsProvider() == "Worker"));

        if (isReal)
        {
            if (!policy.IsWorkerBaseUrlConfigured())
            {
                return new PreflightCheckItem
                {
                    Name = UserMessages.WorkerConnection,
                    Status = PreflightStatus.Fail,
                    Message = UserMessages.ErrorWorkerBaseUrlMissing,
                    FixHint = "Update Worker URL in settings."
                };
            }
            if (!policy.IsWorkerClientKeyConfigured())
            {
                return new PreflightCheckItem
                {
                    Name = "Worker Auth",
                    Status = PreflightStatus.Fail,
                    Message = UserMessages.ErrorWorkerClientKeyMissing,
                    FixHint = UserMessages.HintCheckWorkerKey
                };
            }
        }

        return new PreflightCheckItem
        {
            Name = UserMessages.WorkerConnection,
            Status = PreflightStatus.Pass,
            Message = "URL and Key configured."
        };
    }

    private async Task<PreflightCheckItem> CheckWorkerHealthAsync()
    {
        try
        {
            await _healthService.CheckWorkerAsync();
            bool ok = _healthService.WorkerStatus.Contains("Reachable") || _healthService.WorkerStatus.Contains("OK");
            return new PreflightCheckItem
            {
                Name = "Worker Health",
                Status = ok ? PreflightStatus.Pass : PreflightStatus.Fail,
                Message = ok ? "Worker is reachable." : UserMessages.ErrorWorkerUnreachable,
                FixHint = UserMessages.HintCheckWorkerConnection
            };
        }
        catch (Exception ex)
        {
            return new PreflightCheckItem
            {
                Name = "Worker Health",
                Status = PreflightStatus.Fail,
                Message = $"Check failed: {ex.Message}"
            };
        }
    }

    private PreflightCheckItem CheckAiProvider()
    {
        var policy = new ProviderPolicyService(_configService);
        string provider = _configService.Config.AiProvider;
        if (provider != "Fake" && provider != "Claude")
        {
            return new PreflightCheckItem
            {
                Name = "AI Provider",
                Status = PreflightStatus.Fail,
                Message = $"Unknown provider: {provider}",
                FixHint = "Set AiProvider to 'Fake' or 'Claude'."
            };
        }
        string warning = policy.GetProviderSetupWarning();
        if (!string.IsNullOrWhiteSpace(warning) && provider == "Fake")
        {
            return new PreflightCheckItem
            {
                Name = "AI Provider",
                Status = PreflightStatus.Warning,
                Message = $"{warning} Effective provider: {policy.GetEffectiveAiProvider()}."
            };
        }

        return new PreflightCheckItem
        {
            Name = "AI Provider",
            Status = PreflightStatus.Pass,
            Message = $"Provider: {policy.GetEffectiveAiProvider()}"
        };
    }

    private PreflightCheckItem CheckSttProvider()
    {
        var policy = new ProviderPolicyService(_configService);
        string provider = _configService.Config.TranscriptProvider;
        return new PreflightCheckItem
        {
            Name = "STT Provider",
            Status = (provider == "Fake" || provider == "Worker") ? PreflightStatus.Pass : PreflightStatus.Fail,
            Message = $"Provider: {policy.GetEffectiveTranscriptProvider()}"
        };
    }

    private PreflightCheckItem CheckTtsProvider()
    {
        var policy = new ProviderPolicyService(_configService);
        string provider = _configService.Config.TtsProvider;
        return new PreflightCheckItem
        {
            Name = "TTS Provider",
            Status = (provider == "Fake" || provider == "Worker") ? PreflightStatus.Pass : PreflightStatus.Fail,
            Message = $"Provider: {policy.GetEffectiveTtsProvider()}"
        };
    }

    private PreflightCheckItem CheckElevenLabsVoice()
    {
        var policy = new ProviderPolicyService(_configService);
        if (policy.GetEffectiveTtsProvider() == "Worker")
        {
            if (string.IsNullOrWhiteSpace(_configService.Config.ElevenLabsVoiceId))
            {
                return new PreflightCheckItem
                {
                    Name = "ElevenLabs Voice",
                    Status = PreflightStatus.Fail,
                    Message = "Voice ID is missing.",
                    FixHint = "Enter a valid ElevenLabs Voice ID."
                };
            }
        }
        return new PreflightCheckItem { Name = "ElevenLabs Voice", Status = PreflightStatus.Pass, Message = "OK" };
    }

    private PreflightCheckItem CheckMicrophone()
    {
        if (!_configService.Config.VoiceInputEnabled)
        {
            return new PreflightCheckItem { Name = UserMessages.VoiceInput, Status = PreflightStatus.Skipped, Message = "Voice input disabled." };
        }

        try
        {
            // Check if any capture devices exist
            int count = NAudio.Wave.WaveIn.DeviceCount;
            if (count == 0)
            {
                return new PreflightCheckItem
                {
                    Name = UserMessages.VoiceInput,
                    Status = PreflightStatus.Fail,
                    Message = UserMessages.ErrorMicUnavailable,
                    FixHint = UserMessages.HintCheckSoundSettings
                };
            }
            return new PreflightCheckItem { Name = UserMessages.VoiceInput, Status = PreflightStatus.Pass, Message = $"Found {count} device(s)." };
        }
        catch (Exception ex)
        {
            return new PreflightCheckItem { Name = UserMessages.VoiceInput, Status = PreflightStatus.Fail, Message = $"Error: {ex.Message}" };
        }
    }

    private PreflightCheckItem CheckFoldersWritable()
    {
        string appData = Path.GetDirectoryName(_configService.ConfigPath) ?? "";
        string debug = Path.Combine(appData, "debug");
        string logs = Path.Combine(appData, "logs");

        try
        {
            Directory.CreateDirectory(debug);
            Directory.CreateDirectory(logs);
            
            string testFile = Path.Combine(debug, ".write-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return new PreflightCheckItem { Name = "File System", Status = PreflightStatus.Pass, Message = "Folders are writable." };
        }
        catch (Exception ex)
        {
            return new PreflightCheckItem { Name = "File System", Status = PreflightStatus.Fail, Message = $"Write error: {ex.Message}" };
        }
    }

    private PreflightCheckItem CheckStartupRegistration()
    {
        // Simple check if it matches config (informational)
        return new PreflightCheckItem
        {
            Name = "Startup Registration",
            Status = PreflightStatus.Pass,
            Message = _configService.Config.StartWithWindows ? "Enabled in config." : "Disabled in config."
        };
    }

    private void SaveReport(PreflightCheckResult result)
    {
        try
        {
            string appData = Path.GetDirectoryName(_configService.ConfigPath) ?? "";
            string debug = Path.Combine(appData, "debug");
            Directory.CreateDirectory(debug);
            string path = Path.Combine(debug, "preflight-report.json");
            
            string json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }
}
