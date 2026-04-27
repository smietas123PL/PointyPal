using System;

namespace PointyPal.UI;

public enum SetupWizardStep
{
    Welcome,
    Privacy,
    WorkerConnection,
    VoiceInput,
    VoiceOutput,
    Hotkeys,
    RealFlowTest,
    Complete
}

public class SetupWizardState
{
    public bool PrivacySafeDefaultsApplied { get; set; }
    public bool WorkerReachable { get; set; }
    public bool WorkerAuthValid { get; set; }
    public bool MicrophoneDetected { get; set; }
    public bool TtsTested { get; set; }
    public bool RealFlowTestPassed { get; set; }
    
    // Temporary storage for settings being configured
    public string WorkerBaseUrl { get; set; } = "";
    public string WorkerClientKey { get; set; } = "";
    public bool VoiceEnabled { get; set; }
    public bool TtsEnabled { get; set; }
}

public class SetupWizardResult
{
    public bool Completed { get; set; }
    public bool Skipped { get; set; }
}
