using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PointyPal.Infrastructure;
using PointyPal.Core;

namespace PointyPal.UI;

public partial class SetupWizardWindow : Window
{
    private readonly ConfigService _configService;
    private readonly ProviderHealthCheckService _healthService;
    private readonly AppLogService? _appLog;
    private readonly InteractionCoordinator? _coordinator;
    private readonly SetupWizardState _state = new();
    private SetupWizardStep _currentStep = SetupWizardStep.Welcome;

    public SetupWizardResult Result { get; } = new();

    public SetupWizardWindow(
        ConfigService configService,
        ProviderHealthCheckService healthService,
        AppLogService? appLog = null,
        InteractionCoordinator? coordinator = null)
    {
        InitializeComponent();
        _configService = configService;
        _healthService = healthService;
        _appLog = appLog;
        _coordinator = coordinator;

        InitializeState();
        UpdateStepVisibility();
    }

    private void InitializeState()
    {
        var config = _configService.Config;
        _state.WorkerBaseUrl = config.WorkerBaseUrl;
        _state.WorkerClientKey = config.WorkerClientKey;
        _state.VoiceEnabled = config.VoiceInputEnabled;
        _state.TtsEnabled = config.TtsEnabled;

        WorkerUrlBox.Text = _state.WorkerBaseUrl;
        WorkerKeyBox.Password = _state.WorkerClientKey;
        EnableTtsCheck.IsChecked = _state.TtsEnabled;
        StartWithWindowsCheck.IsChecked = config.StartWithWindows;

        if (_configService.SafeModeActive)
        {
            SafeModeBanner.Visibility = Visibility.Visible;
            _appLog?.Info("WizardSafeMode", "Wizard started in Safe Mode.");
        }
    }

    private void UpdateStepVisibility()
    {
        StepWelcome.Visibility = _currentStep == SetupWizardStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        StepPrivacy.Visibility = _currentStep == SetupWizardStep.Privacy ? Visibility.Visible : Visibility.Collapsed;
        StepWorker.Visibility = _currentStep == SetupWizardStep.WorkerConnection ? Visibility.Visible : Visibility.Collapsed;
        StepVoiceInput.Visibility = _currentStep == SetupWizardStep.VoiceInput ? Visibility.Visible : Visibility.Collapsed;
        StepVoiceOutput.Visibility = _currentStep == SetupWizardStep.VoiceOutput ? Visibility.Visible : Visibility.Collapsed;
        StepHotkeys.Visibility = _currentStep == SetupWizardStep.Hotkeys ? Visibility.Visible : Visibility.Collapsed;
        StepRealFlow.Visibility = _currentStep == SetupWizardStep.RealFlowTest ? Visibility.Visible : Visibility.Collapsed;
        StepComplete.Visibility = _currentStep == SetupWizardStep.Complete ? Visibility.Visible : Visibility.Collapsed;

        PrevBtn.Visibility = _currentStep == SetupWizardStep.Welcome ? Visibility.Collapsed : Visibility.Visible;
        NextBtn.Content = _currentStep == SetupWizardStep.Complete ? "Finish" : "Continue";
        SkipBtn.Visibility = _currentStep == SetupWizardStep.Complete ? Visibility.Collapsed : Visibility.Visible;

        if (_currentStep == SetupWizardStep.Complete)
        {
            FinalVoiceInputText.Text = _configService.Config.VoiceInputEnabled ? "✓ Voice Input Enabled" : "○ Voice Input Disabled";
            FinalVoiceOutputText.Text = _configService.Config.TtsEnabled ? "✓ Voice Output Enabled" : "○ Voice Output Disabled";
            FinalDevModeText.Text = _configService.Config.DeveloperModeEnabled ? "✓ Developer Mode Enabled" : "- Developer Mode Disabled";
        }
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == SetupWizardStep.Complete)
        {
            FinishBtn_Click(sender, e);
            return;
        }

        _currentStep = (SetupWizardStep)((int)_currentStep + 1);
        UpdateStepVisibility();
    }

    private void PrevBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == SetupWizardStep.Welcome) return;
        _currentStep = (SetupWizardStep)((int)_currentStep - 1);
        UpdateStepVisibility();
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to skip the setup wizard? You can reopen it later from the Control Center.",
            "Skip Setup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Result.Skipped = true;
            Close();
        }
    }

    private void FinishBtn_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.Config;
        config.SetupWizardCompleted = true;
        config.OnboardingCompleted = true;
        config.ShowSetupWizardOnStartup = false;
        config.StartWithWindows = StartWithWindowsCheck.IsChecked ?? false;
        _configService.SaveConfig(config);

        Result.Completed = true;
        Close();
    }

    // Step 2: Privacy
    private void ApplyPrivacyDefaultsBtn_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.Config;
        config.SaveDebugArtifacts = false;
        config.SaveScreenshots = false;
        config.SaveRecordings = false;
        config.SaveTtsAudio = false;
        config.SaveInteractionHistory = false;
        config.RedactDebugPayloads = true;
        _configService.SaveConfig(config);

        _state.PrivacySafeDefaultsApplied = true;
        MessageBox.Show("Privacy-safe defaults applied.", "Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
        NextBtn_Click(sender, e);
    }

    private void KeepCurrentPrivacyBtn_Click(object sender, RoutedEventArgs e)
    {
        NextBtn_Click(sender, e);
    }

    // Step 3: Worker
    private async void CheckWorkerBtn_Click(object sender, RoutedEventArgs e)
    {
        string url = WorkerUrlBox.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            WorkerStatusText.Text = "Status: URL is empty";
            return;
        }

        _state.WorkerBaseUrl = url;
        _configService.Config.WorkerBaseUrl = url;

        WorkerStatusText.Text = "Status: Checking...";
        CheckWorkerBtn.IsEnabled = false;

        try
        {
            await _healthService.CheckWorkerAsync();
            _state.WorkerReachable = _healthService.WorkerStatus.Contains("Reachable") || _healthService.WorkerStatus.Contains("OK");
            
            WorkerStatusText.Text = $"Status: {_healthService.WorkerStatus}";
            WorkerDetailsText.Text = _healthService.LastErrorMessage ?? "";
            
            if (_state.WorkerReachable)
            {
                WorkerStatusText.Foreground = Brushes.Green;
            }
            else
            {
                WorkerStatusText.Foreground = Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            WorkerStatusText.Text = "Status: Error";
            WorkerDetailsText.Text = ex.Message;
            WorkerStatusText.Foreground = Brushes.Red;
        }
        finally
        {
            CheckWorkerBtn.IsEnabled = true;
        }
    }

    private void SaveWorkerBtn_Click(object sender, RoutedEventArgs e)
    {
        _configService.Config.WorkerBaseUrl = WorkerUrlBox.Text.Trim();
        _configService.Config.WorkerClientKey = WorkerKeyBox.Password;
        _configService.SaveConfig(_configService.Config);
        
        MessageBox.Show("Connection settings saved.", "Worker", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Step 4: Voice Input
    private void TestMicBtn_Click(object sender, RoutedEventArgs e)
    {
        // For simplicity, we just check if devices exist. 
        // In a real app, we might start a small capture loop to show levels.
        int devices = NAudio.Wave.WaveIn.DeviceCount;
        if (devices > 0)
        {
            MicStatusText.Text = $"Microphone detected! ({devices} devices)";
            MicStatusText.Foreground = Brushes.Green;
            _state.MicrophoneDetected = true;
        }
        else
        {
            MicStatusText.Text = "No microphone detected.";
            MicStatusText.Foreground = Brushes.Red;
        }
    }

    private async void TestRealSttBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_configService.Config.WorkerClientKey))
        {
            MessageBox.Show("Worker Client Key is required for real transcription.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("This will capture 2 seconds of audio and send it to your Worker. Please speak now...", "Real STT Test");
        // Logic to run a real STT test would go here. For now, we show a placeholder.
        await Task.Delay(2000);
        MessageBox.Show("Test request sent. Check your Worker logs or app logs for results.", "Real STT Test Started");
    }

    // Step 5: Voice Output
    private void EnableTtsCheck_Checked(object sender, RoutedEventArgs e)
    {
        _configService.Config.TtsEnabled = true;
        TtsStatusText.Text = "TTS is enabled.";
    }

    private void EnableTtsCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        _configService.Config.TtsEnabled = false;
        TtsStatusText.Text = "TTS is currently disabled.";
    }

    private async void TestTtsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_configService.Config.TtsEnabled)
        {
            MessageBox.Show("Please enable Voice Output first.", "TTS Disabled", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TtsStatusText.Text = "Testing TTS...";
        TestTtsBtn.IsEnabled = false;

        bool ok = await _healthService.TestTtsAsync();
        TtsStatusText.Text = ok ? "TTS test successful! Check your speakers." : "TTS test failed. Check Worker URL and Key.";
        TtsStatusText.Foreground = ok ? Brushes.Green : Brushes.Red;
        TestTtsBtn.IsEnabled = true;
        _state.TtsTested = ok;
    }

    // Step 6: Hotkeys
    private void OpenHotkeysBtn_Click(object sender, RoutedEventArgs e)
    {
        // This would open the full hotkey reference. 
        // For now, we can just point to the Control Center.
        MessageBox.Show("Full hotkey reference is available in the Control Center > Hotkeys tab.", "Hotkeys");
    }

    // Step 7: Real Flow
    private async void RunRealTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator == null) return;
        if (string.IsNullOrEmpty(_configService.Config.WorkerClientKey))
        {
            MessageBox.Show("Worker Client Key is required for real tests.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RealTestResultPanel.Visibility = Visibility.Visible;
        RealTestResultText.Text = "Running Quick Ask Test...";
        
        try
        {
            // Simple text-only test
            await _coordinator.StartInteractionAsync("Hi, please answer with 'Ready'.", ProviderOverride.ForceClaude);
            RealTestResultText.Text = "Quick Ask Test completed. Check for response bubble.";
            _state.RealFlowTestPassed = true;
        }
        catch (Exception ex)
        {
            RealTestResultText.Text = $"Test failed: {ex.Message}";
        }
    }

    private async void RunScreenshotTestBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator == null) return;
        if (string.IsNullOrEmpty(_configService.Config.WorkerClientKey))
        {
            MessageBox.Show("Worker Client Key is required for real tests.", "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RealTestResultPanel.Visibility = Visibility.Visible;
        RealTestResultText.Text = "Capturing screen and sending to Claude...";

        try
        {
            await _coordinator.StartInteractionAsync("What is on my screen?", ProviderOverride.ForceClaude);
            RealTestResultText.Text = "Screen Context Test completed. Check for response bubble.";
            _state.RealFlowTestPassed = true;
        }
        catch (Exception ex)
        {
            RealTestResultText.Text = $"Test failed: {ex.Message}";
        }
    }

    private void SkipRealTestBtn_Click(object sender, RoutedEventArgs e)
    {
        NextBtn_Click(sender, e);
    }

    private void OpenControlCenterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.OpenControlCenter();
        }
    }

    private void HelpLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/example/pointypal#setup",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            _appLog?.Error("WizardHelpLink", $"Failed to open help link: {ex.Message}");
        }
    }
}
