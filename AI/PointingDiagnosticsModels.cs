using System;
using System.Windows;
using PointyPal.Capture;

namespace PointyPal.AI;

public class PointingAttempt
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string UserText { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    
    public int ScreenshotWidth { get; set; }
    public int ScreenshotHeight { get; set; }
    public Rect MonitorBounds { get; set; }
    
    public string RawAiResponse { get; set; } = string.Empty;
    public string CleanResponse { get; set; } = string.Empty;
    
    // Image space coordinates from AI
    public double? ParsedPointImageX { get; set; }
    public double? ParsedPointImageY { get; set; }
    public string? ParsedPointLabel { get; set; }
    
    // Initial mapped screen coordinates (before snapping)
    public double? MappedScreenX { get; set; }
    public double? MappedScreenY { get; set; }
    
    public bool WasPointClamped { get; set; }
    
    // UI Automation context data
    public string? UiElementUnderMappedPoint { get; set; }
    public string? NearestUiElement { get; set; }
    public double? DistanceToNearestUiElement { get; set; }
    
    // Final coordinates used for flight
    public double FinalPointScreenX { get; set; }
    public double FinalPointScreenY { get; set; }
    public string? AdjustmentReason { get; set; }
}

public class PointValidationResult
{
    public bool IsValid { get; set; }
    public bool HasPoint { get; set; }
    public bool InImageBounds { get; set; }
    public bool InMonitorBounds { get; set; }
    public bool InVirtualDesktopBounds { get; set; }
    public bool WasClamped { get; set; }
    public string? WarningMessage { get; set; }
}

public class PointingDiagnostics
{
    public PointingAttempt? LastAttempt { get; set; }
    public PointValidationResult? LastValidation { get; set; }
}
