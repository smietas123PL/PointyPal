using System;
using System.Collections.Generic;

namespace PointyPal.Infrastructure;

public enum ResilienceStatus
{
    Healthy,
    Degraded,
    Offline,
    Recovering,
    SafeModeRecommended
}

public class ResilienceEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "Info"; // "Info", "Warning", "Error", "Critical"
    public string Message { get; set; } = "";
    public string? SuggestedAction { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class ResilienceSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ResilienceStatus Status { get; set; }
    public double ProcessWorkingSetMb { get; set; }
    public double PrivateMemoryMb { get; set; }
    public double CpuUsagePercent { get; set; }
    public int GdiObjectCount { get; set; }
    public int UserObjectCount { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
    public TimeSpan AppUptime { get; set; }
    public int InteractionCountSinceStart { get; set; }
    public int ConsecutiveProviderFailures { get; set; }
    public bool MicrophoneAvailable { get; set; }
    public int DisplayCount { get; set; }
    public DateTime? LastResourceWarningAt { get; set; }
    public string? LastResourceWarningMessage { get; set; }
}

public class SoakTestReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationMinutes { get; set; }
    public int TotalIterations { get; set; }
    public int PassedIterations { get; set; }
    public int FailedIterations { get; set; }
    public double AverageInteractionDurationMs { get; set; }
    public double P95InteractionDurationMs { get; set; }
    public double MaxMemoryMb { get; set; }
    public double MemoryDeltaMb { get; set; }
    public int MaxHandles { get; set; }
    public int MaxThreads { get; set; }
    public int FallbackActivations { get; set; }
    public int CancellationFailures { get; set; }
    public List<string> Errors { get; set; } = new();
}
