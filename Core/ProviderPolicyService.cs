using System;
using System.Collections.Generic;
using System.Linq;
using PointyPal.Infrastructure;

namespace PointyPal.Core;

public enum ProviderRuntimeMode
{
    Normal,
    Developer,
    Safe,
    SelfTest
}

public class ProviderConfigurationValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
    public string UserMessage => IsValid ? "" : string.Join(" ", Errors);

    internal void Add(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !Errors.Contains(message))
        {
            Errors.Add(message);
        }
    }
}

public class ProviderStatusForUi
{
    public ProviderRuntimeMode Mode { get; set; }
    public string ModeLabel { get; set; } = "Normal Mode";
    public string AiProvider { get; set; } = "";
    public string TranscriptProvider { get; set; } = "";
    public string TtsProvider { get; set; } = "";
    public bool WorkerBaseUrlConfigured { get; set; }
    public bool WorkerAuthConfigured { get; set; }
    public bool AiReady { get; set; }
    public bool VoiceInputReady { get; set; }
    public bool TtsReady { get; set; }
    public bool SafeModeActive { get; set; }
    public bool DeveloperModeEnabled { get; set; }
    public string BannerMessage { get; set; } = "";
    public string SetupWarning { get; set; } = "";
}

public class ProviderPolicyService
{
    public const string FakeProvidersDeveloperOnlyMessage = "Fake/simulated providers are only available in Developer Mode.";
    public const string WorkerUnavailableFakeFallbackDisabledMessage = "Worker connection failed. Simulated fallback is disabled in Normal Mode.";
    public const string SafeModeBannerMessage = "PointyPal is in Safe Mode. Real AI features are disabled to ensure stability. Simulated responses are for recovery and diagnostics only.";
    public const string DeveloperModeBannerMessage = "Developer Mode Active - Simulated providers and advanced diagnostics are enabled.";

    private readonly ConfigService _configService;
    private readonly ProviderRuntimeMode? _runtimeModeOverride;

    public ProviderPolicyService(ConfigService configService, ProviderRuntimeMode? runtimeModeOverride = null)
    {
        _configService = configService;
        _runtimeModeOverride = runtimeModeOverride;
    }

    public ProviderRuntimeMode RuntimeMode
    {
        get
        {
            if (_runtimeModeOverride.HasValue) return _runtimeModeOverride.Value;
            if (_configService.SafeModeActive) return ProviderRuntimeMode.Safe;
            return _configService.Config.DeveloperModeEnabled
                ? ProviderRuntimeMode.Developer
                : ProviderRuntimeMode.Normal;
        }
    }

    public string GetEffectiveAiProvider()
    {
        var mode = RuntimeMode;
        var config = _configService.Config;

        if (mode == ProviderRuntimeMode.Safe || mode == ProviderRuntimeMode.SelfTest)
        {
            return "Fake";
        }

        if (mode == ProviderRuntimeMode.Developer)
        {
            string configured = NormalizeAiProvider(config.AiProvider);
            if (configured == "Fake" && !CanUseFakeProviders())
            {
                return "Claude";
            }

            return configured;
        }

        return "Claude";
    }

    public string GetEffectiveTranscriptProvider()
    {
        var mode = RuntimeMode;
        var config = _configService.Config;

        if (mode == ProviderRuntimeMode.Safe || mode == ProviderRuntimeMode.SelfTest)
        {
            return "Fake";
        }

        if (mode == ProviderRuntimeMode.Developer)
        {
            string configured = NormalizeWorkerOrFakeProvider(config.TranscriptProvider);
            if (configured == "Fake" && !CanUseFakeProviders())
            {
                return "Worker";
            }

            return configured;
        }

        return "Worker";
    }

    public string GetEffectiveTtsProvider()
    {
        var mode = RuntimeMode;
        var config = _configService.Config;

        if (!config.TtsEnabled)
        {
            return "Disabled";
        }

        if (mode == ProviderRuntimeMode.Safe || mode == ProviderRuntimeMode.SelfTest)
        {
            return "Fake";
        }

        if (mode == ProviderRuntimeMode.Developer)
        {
            string configured = NormalizeWorkerOrFakeProvider(config.TtsProvider);
            if (configured == "Fake" && !CanUseFakeProviders())
            {
                return "Worker";
            }

            return configured;
        }

        return "Worker";
    }

    public bool CanUseFakeProviders()
    {
        return RuntimeMode switch
        {
            ProviderRuntimeMode.Safe => true,
            ProviderRuntimeMode.SelfTest => true,
            ProviderRuntimeMode.Developer => _configService.Config.AllowFakeProvidersInDeveloperMode,
            _ => false
        };
    }

    public bool CanUseRealProviders()
    {
        return RuntimeMode != ProviderRuntimeMode.Safe && RuntimeMode != ProviderRuntimeMode.SelfTest;
    }

    public bool CanFallbackToFake()
    {
        var config = _configService.Config;
        if (!config.EnableProviderFallback || !config.FallbackToFakeOnWorkerFailure)
        {
            return false;
        }

        return RuntimeMode switch
        {
            ProviderRuntimeMode.Developer => CanUseFakeProviders(),
            ProviderRuntimeMode.Normal => config.AllowFakeProviderFallbackInNormalMode,
            _ => false
        };
    }

    public ProviderConfigurationValidationResult ValidateRealProviderConfiguration()
    {
        var result = new ProviderConfigurationValidationResult();
        if (!CanUseRealProviders())
        {
            return result;
        }

        bool needsWorker =
            GetEffectiveAiProvider() == "Claude" ||
            GetEffectiveTranscriptProvider() == "Worker" ||
            GetEffectiveTtsProvider() == "Worker";

        if (!needsWorker)
        {
            return result;
        }

        if (!IsWorkerBaseUrlConfigured())
        {
            result.Add(UserMessages.ErrorWorkerUrlMissing + " " + UserMessages.HintCheckConnectionSettings);
        }

        if (!IsWorkerClientKeyConfigured())
        {
            result.Add(UserMessages.ErrorWorkerKeyMissing + " " + UserMessages.HintCheckConnectionSettings);
        }

        return result;
    }

    public ProviderStatusForUi GetProviderStatusForUi()
    {
        var validation = ValidateRealProviderConfiguration();
        var mode = RuntimeMode;
        string aiProvider = GetEffectiveAiProvider();
        string transcriptProvider = GetEffectiveTranscriptProvider();
        string ttsProvider = GetEffectiveTtsProvider();

        return new ProviderStatusForUi
        {
            Mode = mode,
            ModeLabel = mode switch
            {
                ProviderRuntimeMode.Developer => "Developer Mode",
                ProviderRuntimeMode.Safe => "Safe Mode",
                ProviderRuntimeMode.SelfTest => "Self-Test Mode",
                _ => "Normal Mode"
            },
            AiProvider = aiProvider,
            TranscriptProvider = transcriptProvider,
            TtsProvider = ttsProvider,
            WorkerBaseUrlConfigured = IsWorkerBaseUrlConfigured(),
            WorkerAuthConfigured = IsWorkerClientKeyConfigured(),
            AiReady = aiProvider == "Fake" || validation.IsValid,
            VoiceInputReady = transcriptProvider == "Fake" || validation.IsValid,
            TtsReady = ttsProvider == "Disabled" || ttsProvider == "Fake" || validation.IsValid,
            SafeModeActive = mode == ProviderRuntimeMode.Safe,
            DeveloperModeEnabled = _configService.Config.DeveloperModeEnabled,
            BannerMessage = mode switch
            {
                ProviderRuntimeMode.Safe => SafeModeBannerMessage,
                ProviderRuntimeMode.Developer => DeveloperModeBannerMessage,
                _ => ""
            },
            SetupWarning = GetProviderSetupWarning()
        };
    }

    public string GetProviderSetupWarning()
    {
        if (RuntimeMode != ProviderRuntimeMode.Normal)
        {
            return "";
        }

        var config = _configService.Config;
        bool configuredFake =
            IsFake(config.AiProvider) ||
            IsFake(config.TranscriptProvider) ||
            (config.TtsEnabled && IsFake(config.TtsProvider));

        return configuredFake ? FakeProvidersDeveloperOnlyMessage : "";
    }

    public bool IsWorkerBaseUrlConfigured()
    {
        string url = _configService.Config.WorkerBaseUrl;
        return !string.IsNullOrWhiteSpace(url) &&
               !url.Contains("YOUR-WORKER", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsWorkerClientKeyConfigured()
    {
        string key = _configService.Config.WorkerClientKey;
        return !string.IsNullOrWhiteSpace(key) &&
               !key.Equals("YOUR_POINTYPAL_CLIENT_KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAiProvider(string? provider)
    {
        return IsFake(provider) ? "Fake" : "Claude";
    }

    private static string NormalizeWorkerOrFakeProvider(string? provider)
    {
        return IsFake(provider) ? "Fake" : "Worker";
    }

    private static bool IsFake(string? provider)
    {
        return provider != null && provider.Equals("Fake", StringComparison.OrdinalIgnoreCase);
    }
}
