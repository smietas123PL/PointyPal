using System;
using System.Collections.Generic;

namespace PointyPal.Core;

public enum SelfTestMode
{
    Quick,
    Full,
    LatencySelfTest
}

public class SelfTestScenarioResult
{
    public string ScenarioName { get; set; } = "";
    public bool Passed { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string AssertionsSummary { get; set; } = "";
}

public class SelfTestResult
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public bool Passed { get; set; }
    public int TotalScenarios { get; set; }
    public int PassedScenarios { get; set; }
    public int FailedScenarios { get; set; }
    public List<SelfTestScenarioResult> ScenarioResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public SelfTestMode Mode { get; set; }
}
