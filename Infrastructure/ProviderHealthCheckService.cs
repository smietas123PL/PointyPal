using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using PointyPal.Core;

namespace PointyPal.Infrastructure;

public class ProviderHealthCheckService
{
    private readonly ConfigService _configService;
    private readonly HttpClient _httpClient;
    private readonly AppLogService? _appLog;

    public string WorkerStatus { get; private set; } = "Unknown";
    public string AiStatus { get; private set; } = "Unknown";
    public string TranscriptStatus { get; private set; } = "Unknown";
    public string TtsStatus { get; private set; } = "Unknown";
    public DateTime? LastCheckTime { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public ProviderHealthCheckService(ConfigService configService, AppLogService? appLog = null)
    {
        _configService = configService;
        _appLog = appLog;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task CheckAllAsync()
    {
        await CheckWorkerAsync();
        // AI, STT, and TTS status mostly depend on effective providers from the policy service.
        UpdateDependentStatuses();
    }

    public async Task CheckWorkerAsync()
    {
        var config = _configService.Config;
        if (string.IsNullOrWhiteSpace(config.WorkerBaseUrl))
        {
            WorkerStatus = UserMessages.StatusSetupRequired;
            LastErrorMessage = UserMessages.ErrorWorkerUrlMissing;
            LastCheckTime = DateTime.Now;
            _appLog?.Warning("WorkerHealthCheck", "Status=NotConfigured");
            return;
        }

        try
        {
            string url = config.WorkerBaseUrl.TrimEnd('/') + "/health";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                WorkerStatus = "Ready";
                LastErrorMessage = null;
                _appLog?.Info("WorkerHealthCheck", "Status=Ready");
            }
            else
            {
                WorkerStatus = $"Error ({response.StatusCode})";
                LastErrorMessage = $"Worker returned status code: {response.StatusCode}";
                _appLog?.Warning("WorkerHealthCheck", $"Status={response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WorkerStatus = UserMessages.StatusUnreachable;
            LastErrorMessage = ex.Message;
            _appLog?.Warning("WorkerHealthCheck", $"Status=Unreachable; Error={ex.Message}");
        }

        LastCheckTime = DateTime.Now;
        UpdateDependentStatuses();
    }

    public async Task<bool> TestTtsAsync()
    {
        var config = _configService.Config;
        var policy = new ProviderPolicyService(_configService);
        string effectiveProvider = policy.GetEffectiveTtsProvider();
        if (effectiveProvider == "Disabled") return true;
        if (effectiveProvider == "Fake") return policy.CanUseFakeProviders();
        if (string.IsNullOrWhiteSpace(config.WorkerBaseUrl)) return false;

        try
        {
            var payload = new
            {
                text = "Test PointyPal.",
                voiceId = config.ElevenLabsVoiceId,
                modelId = config.ElevenLabsModelId,
                outputFormat = config.ElevenLabsOutputFormat
            };

            string url = config.WorkerBaseUrl.TrimEnd('/') + "/tts";
            var response = await _httpClient.PostAsync(url, 
                new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                TtsStatus = "Ready";
                _appLog?.Info("TtsHealthCheck", "Status=Ready");
                return true;
            }
            else
            {
                TtsStatus = UserMessages.StatusError;
                LastErrorMessage = $"TTS test failed: {response.StatusCode}";
                _appLog?.Warning("TtsHealthCheck", $"Status={response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            TtsStatus = UserMessages.StatusError;
            LastErrorMessage = ex.Message;
            _appLog?.Warning("TtsHealthCheck", $"Status=Error; Error={ex.Message}");
            return false;
        }
    }

    private void UpdateDependentStatuses()
    {
        var policy = new ProviderPolicyService(_configService);
        var validation = policy.ValidateRealProviderConfiguration();

        AiStatus = FormatProviderStatus(policy.GetEffectiveAiProvider(), validation);
        TranscriptStatus = FormatProviderStatus(policy.GetEffectiveTranscriptProvider(), validation);
        TtsStatus = FormatProviderStatus(policy.GetEffectiveTtsProvider(), validation);
    }

    private string FormatProviderStatus(string provider, ProviderConfigurationValidationResult validation)
    {
        if (provider == "Disabled") return "Disabled";
        if (provider == "Fake") return "Fake (Diagnostics Only)";
        if (!validation.IsValid) return UserMessages.StatusSetupRequired;
        return WorkerStatus;
    }
}
