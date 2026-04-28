using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PointyPal.Infrastructure;
using PointyPal.Core;

namespace PointyPal.Input;

public class PushToTalkService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;
    private readonly AppStateManager _stateManager;
    private readonly Voice.MicrophoneCaptureService _micService;
    private readonly Voice.ITranscriptProvider _transcriptProvider;
    private readonly Voice.ITranscriptProvider _fakeTranscriptProvider;
    private readonly Voice.ITranscriptProvider _workerTranscriptProvider;
    private readonly ConfigService _configService;
    private readonly InteractionCoordinator _coordinator;
    private readonly UsageTracker _usageTracker;
    private readonly DebugLogger _debugLogger;
    private readonly InteractionTimelineService _timelineService;
    private readonly ResilienceMonitorService? _resilienceMonitor;
    private readonly ProviderPolicyService _providerPolicy;
    private readonly HotkeyPolicyService _hotkeyPolicy;
    private CancellationTokenSource? _voiceCts;
    private InteractionTimelineStep? _recordingStep;
    private bool _isDisposed;
    private DateTime _recordingStartTime;

    private const int VK_F8 = 0x77;
    private const int VK_F9 = 0x78;
    private const int VK_F10 = 0x79;
    private const int VK_F11 = 0x7A;
    private const int VK_F12 = 0x7B;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_1 = 0x31;
    private const int VK_2 = 0x32;
    private const int VK_3 = 0x33;
    private const int VK_SPACE = 0x20;

    private const int VK_MENU = 0x12;

    public Voice.MicrophoneCaptureService MicService => _micService;
    public event EventHandler? DiagnosticsToggled;
    public event EventHandler? TestF10Requested;
    public event EventHandler? TestF11Requested;
    public event EventHandler? UiCaptureRequested;
    public event EventHandler? CalibrationToggled;
    public event Action<int>? RatingSubmitted;
    public event Action<string, ProviderOverride>? InteractionRequested;
    public event EventHandler? QuickAskRequested;
    
    public void RequestCalibrationToggle() => CalibrationToggled?.Invoke(this, EventArgs.Empty);
    public void RequestDiagnosticsToggle() => DiagnosticsToggled?.Invoke(this, EventArgs.Empty);

    public PushToTalkService(
        AppStateManager stateManager, 
        Voice.MicrophoneCaptureService micService,
        Voice.ITranscriptProvider transcriptProvider,
        ConfigService configService,
        InteractionCoordinator coordinator,
        UsageTracker usageTracker,
        DebugLogger debugLogger,
        InteractionTimelineService timelineService,
        ResilienceMonitorService? resilienceMonitor = null,
        ProviderPolicyService? providerPolicy = null,
        HotkeyPolicyService? hotkeyPolicy = null,
        Voice.ITranscriptProvider? fakeTranscriptProvider = null,
        Voice.ITranscriptProvider? workerTranscriptProvider = null)
    {
        _stateManager = stateManager;
        _micService = micService;
        _transcriptProvider = transcriptProvider;
        _configService = configService;
        _coordinator = coordinator;
        _usageTracker = usageTracker;
        _debugLogger = debugLogger;
        _timelineService = timelineService;
        _resilienceMonitor = resilienceMonitor;
        _providerPolicy = providerPolicy ?? new ProviderPolicyService(configService);
        _hotkeyPolicy = hotkeyPolicy ?? new HotkeyPolicyService(configService);
        _fakeTranscriptProvider = fakeTranscriptProvider ?? new Voice.FakeTranscriptProvider(configService.Config.FakeTranscriptText);
        _workerTranscriptProvider = workerTranscriptProvider ?? transcriptProvider;
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule!)
        {
            return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc,
                NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool isKeyDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)NativeMethods.WM_KEYUP || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP;

            if (vkCode == NativeMethods.VK_RCONTROL)
            {
                var config = _configService.Config;
                if (isKeyDown)
                {
                    if (!config.VoiceInputEnabled)
                    {
                        _coordinator.ShowStatusMessage("Voice input is disabled.");
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }

                    bool canStart = _stateManager.CurrentState == CompanionState.FollowingCursor || 
                                     _stateManager.IsInFlightState();
                                     
                    bool alreadyBusy = _stateManager.CurrentState == CompanionState.Processing || 
                                       _stateManager.CurrentState == CompanionState.Speaking;

                    if (alreadyBusy && config.NewInteractionBehavior == "CancelPrevious")
                    {
                        _coordinator.CancelCurrentInteraction("Right Ctrl restart");
                        canStart = true;
                    }

                    if (canStart && !_micService.IsRecording)
                    {
                        if (_usageTracker.CurrentUsage.InteractionsCount >= config.DailyInteractionLimit)
                        {
                             _coordinator.ShowStatusMessage("Daily interaction limit reached.");
                             return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                        }

                        if (_micService.IsMicrophoneAvailable())
                        {
                            _voiceCts?.Cancel();
                            _voiceCts = new CancellationTokenSource();
                            _stateManager.SetState(CompanionState.Listening, "Right Ctrl pressed");
                            bool saveToDisk = config.SaveDebugArtifacts && config.SaveRecordings;
                            _micService.StartRecording(saveToDisk);
                            _recordingStartTime = DateTime.Now;
                            _timelineService.StartTimeline(InteractionSource.Voice, config.VoiceInteractionMode, _providerPolicy.GetEffectiveTranscriptProvider());
                            _recordingStep = _timelineService.StartStep(InteractionTimelineStepNames.PushToTalkRecording);
                        }
                        else
                        {
                            _resilienceMonitor?.RecordEvent("VoiceInput", "Error", "No microphone detected when trying to record.");
                            _stateManager.SetState(CompanionState.Error, "No microphone");
                            _coordinator.ShowStatusMessage("No microphone detected.");
                            Task.Delay(2000).ContinueWith(_ => _stateManager.SetState(CompanionState.FollowingCursor, "Error recovery"));
                        }
                    }
                }
                else if (isKeyUp)
                {
                    if (_stateManager.CurrentState == CompanionState.Listening)
                    {
                        bool isShiftHeld = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                        bool forceFakeVoice = isShiftHeld && _hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.ForceFakeProvider);
                        var audioWriteStep = _timelineService.StartStep(InteractionTimelineStepNames.AudioFileWrite);
                        _micService.StopRecording();
                        _timelineService.CompleteStep(_recordingStep, new Dictionary<string, string>
                        {
                            ["DurationMs"] = Math.Round(_micService.LastDurationMs).ToString()
                        });
                        _recordingStep = null;
                        _timelineService.CompleteStep(audioWriteStep, GetAudioMetadata());
                        _ = HandleVoiceRecordingCompleteAsync(forceFakeVoice);
                    }
                }
            }
            else if (vkCode == VK_F8 && isKeyDown)
            {
                if (!_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.FakePointerFlight))
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (_stateManager.CurrentState == CompanionState.FollowingCursor)
                {
                    _stateManager.SetState(CompanionState.FlyingToTarget, "F8 Fake Target");
                }
            }
            else if (vkCode == VK_F9 && isKeyDown)
            {
                bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                if (isCtrl && isShift)
                {
                    if (_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.CalibrationGrid))
                    {
                        CalibrationToggled?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (isCtrl)
                {
                    if (_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.UiAutomationCapture))
                    {
                        UiCaptureRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    DiagnosticsToggled?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (vkCode >= VK_1 && vkCode <= VK_3 && isKeyDown)
            {
                bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool isAlt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                if (isCtrl && isAlt)
                {
                    if (!_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.PointRating))
                    {
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }

                    int rating = vkCode - VK_1 + 1;
                    RatingSubmitted?.Invoke(rating);
                }
            }
            else if (vkCode == VK_F10 && isKeyDown)
            {
                if (!_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.FakeMappedPoint))
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (_stateManager.CurrentState == CompanionState.FollowingCursor)
                {
                    TestF10Requested?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (vkCode == VK_F11 && isKeyDown)
            {
                if (!_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.CenterMappingTest))
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (_stateManager.CurrentState == CompanionState.FollowingCursor)
                {
                    TestF11Requested?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (vkCode == VK_F12 && isKeyDown)
            {
                bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                bool isAlt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

                if (isCtrl && isShift && !isAlt)
                {
                    if (!_hotkeyPolicy.IsHotkeyAllowed(PointyPalHotkeyAction.RuntimeTtsToggle))
                    {
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }

                    // Ctrl+Shift+F12: Toggle TTS
                    _configService.Config.TtsEnabled = !_configService.Config.TtsEnabled;
                    _configService.SaveConfig(_configService.Config);
                    _coordinator.CancelCurrentInteraction(); // Stop any active interaction/audio
                    _stateManager.SetState(CompanionState.FollowingCursor, $"TTS {(_configService.Config.TtsEnabled ? "Enabled" : "Disabled")}");
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (_stateManager.CurrentState == CompanionState.FollowingCursor)
                {
                    var hotkeyAction = GetF12HotkeyAction(isShift, isCtrl, isAlt);
                    if (!_hotkeyPolicy.IsHotkeyAllowed(hotkeyAction))
                    {
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }

                    ProviderOverride providerOverride = ProviderOverride.None;
                    if (isAlt && isCtrl) providerOverride = ProviderOverride.ForceFake;
                    else if (isAlt) providerOverride = ProviderOverride.ForceClaude;

                    string mockInput = "default point";
                    if (isShift) mockInput = "center";
                    if (isCtrl && !isAlt) mockInput = "none";
                    
                    InteractionRequested?.Invoke(mockInput, providerOverride);
                }
            }
            else if (vkCode == VK_SPACE && isKeyDown)
            {
                bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                if (isCtrl)
                {
                    QuickAskRequested?.Invoke(this, EventArgs.Empty);
                    return (IntPtr)1; // Consume key
                }
            }
            else if (vkCode == VK_ESCAPE && isKeyDown)
            {
                if (_micService.IsRecording)
                {
                    _timelineService.FailStep(_recordingStep, "Escape pressed");
                    _recordingStep = null;
                    _micService.CancelRecording();
                }
                _voiceCts?.Cancel();
                _ = _timelineService.CompleteTimelineAsync(wasCancelled: true, errorMessage: "Escape pressed");
                _coordinator.CancelCurrentInteraction("Escape pressed");
                _stateManager.CancelToFollowingCursor("Escape pressed");
            }
        }
        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static PointyPalHotkeyAction GetF12HotkeyAction(bool isShift, bool isCtrl, bool isAlt)
    {
        if (isAlt && isCtrl) return PointyPalHotkeyAction.ForceFakeProvider;
        if (isAlt) return PointyPalHotkeyAction.ForceClaudeProvider;
        if (isCtrl) return PointyPalHotkeyAction.FakeNoneInteraction;
        if (isShift) return PointyPalHotkeyAction.FakeCenterInteraction;
        return PointyPalHotkeyAction.FakeLocalInteraction;
    }

    private Voice.ITranscriptProvider ResolveTranscriptProvider(bool forceFake)
    {
        if (forceFake && _providerPolicy.CanUseFakeProviders())
        {
            return _fakeTranscriptProvider;
        }

        return _providerPolicy.GetEffectiveTranscriptProvider() == "Fake"
            ? _fakeTranscriptProvider
            : _workerTranscriptProvider;
    }

    private async Task HandleVoiceRecordingCompleteAsync(bool forceFake = false)
    {
        double durationMs = (DateTime.Now - _recordingStartTime).TotalMilliseconds;
        var config = _configService.Config;

        if (durationMs < config.MinRecordingMs)
        {
            _micService.CancelRecording();
            _stateManager.SetState(CompanionState.FollowingCursor, "Recording too short");
            await _timelineService.CompleteTimelineAsync(wasCancelled: true, errorMessage: "Recording too short");
            return;
        }

        try
        {
            // Check STT limit
            double durationSec = durationMs / 1000.0;
            if (_usageTracker.CurrentUsage.SttSeconds + durationSec > config.DailySttSecondsLimit)
            {
                _micService.CancelRecording();
                _stateManager.SetState(CompanionState.Error, "STT Limit");
                await _timelineService.CompleteTimelineAsync(errorMessage: "STT limit reached");
                _coordinator.ShowStatusMessage("Daily transcription limit reached.");
                await Task.Delay(3000);
                _stateManager.SetState(CompanionState.FollowingCursor, "STT Limit reached");
                return;
            }
            _usageTracker.AddSttSeconds(durationSec);

            _stateManager.SetState(CompanionState.Processing, "Transcribing voice");

            Voice.ITranscriptProvider provider = ResolveTranscriptProvider(forceFake);
            string effectiveProviderName = forceFake && _providerPolicy.CanUseFakeProviders()
                ? "Fake"
                : _providerPolicy.GetEffectiveTranscriptProvider();
            _timelineService.SetProviderName(effectiveProviderName);
            
            _voiceCts?.Cancel();
            _voiceCts = new CancellationTokenSource();
            var token = _voiceCts.Token;

            var transcriptionStep = _timelineService.StartStep(
                InteractionTimelineStepNames.TranscriptionRequest,
                new Dictionary<string, string>
                {
                    ["Provider"] = effectiveProviderName
                });

            Voice.TranscriptResult result;
            try
            {
                result = await provider.GetTranscriptAsync(
                    new Voice.TranscriptRequest { AudioFilePath = _micService.LastFilePath ?? "" },
                    token);

                var metadata = new Dictionary<string, string>
                {
                    ["Provider"] = result.ProviderName,
                    ["TranscriptLength"] = result.Text.Length.ToString(),
                    ["ProviderDurationMs"] = Math.Round(result.DurationMs).ToString()
                };

                if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _timelineService.CompleteStep(transcriptionStep, metadata);
                }
                else
                {
                    _timelineService.FailStep(transcriptionStep, result.ErrorMessage, metadata);
                }
            }
            catch (Exception ex)
            {
                _timelineService.FailStep(transcriptionStep, ex.Message);
                throw;
            }

            _debugLogger.SaveDebugJson("latest-transcript-request.json", new {
                AudioPath = _micService.LastFilePath,
                DurationMs = durationMs,
                Timestamp = DateTime.Now
            });
            _debugLogger.SaveDebugJson("latest-transcript-response.json", result);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                _resilienceMonitor?.RecordProviderFailure("STT", result.ErrorMessage);
                _usageTracker.IncrementErrors();
                _stateManager.SetState(CompanionState.Error, result.ErrorMessage);
                await _timelineService.CompleteTimelineAsync(errorMessage: result.ErrorMessage);
                _coordinator.ShowStatusMessage(result.ErrorMessage);
                await Task.Delay(3000);
                _stateManager.SetState(CompanionState.FollowingCursor, "Transcription error");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                await _timelineService.CompleteTimelineAsync(errorMessage: "Empty transcript");
                _coordinator.ShowStatusMessage("I did not hear a question.");
                await Task.Delay(2000);
                _stateManager.SetState(CompanionState.FollowingCursor, "Empty transcript");
                return;
            }

            // Trigger the coordinator with the transcript
            await _coordinator.RunVoiceInteractionAsync(result, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await _timelineService.CompleteTimelineAsync(wasCancelled: true, errorMessage: "Voice transcription cancelled");
            _stateManager.SetState(CompanionState.FollowingCursor, "Voice transcription cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Voice interaction failed: {ex.Message}");
            await _timelineService.CompleteTimelineAsync(errorMessage: ex.Message);
            _stateManager.SetState(CompanionState.Error, $"Voice Error: {ex.Message}");
            _coordinator.ShowStatusMessage("Voice processing failed.");
            await Task.Delay(2000);
            _stateManager.SetState(CompanionState.FollowingCursor, "Recovered from voice error");
        }
    }

    private Dictionary<string, string> GetAudioMetadata()
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            string? path = _micService.LastFilePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                metadata["AudioBytes"] = new FileInfo(path).Length.ToString();
            }
        }
        catch
        {
            // Timeline metadata is diagnostic-only.
        }

        return metadata;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _voiceCts?.Cancel();
            _voiceCts?.Dispose();
            if (_hookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _isDisposed = true;
        }
    }
}
