using System;
using System.Windows;
using PointyPal.Capture;
using PointyPal.Core;

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
    
    // Core pointer data
    public PointerTarget Target { get; set; } = new();

    // Redundant but kept for diagnostic compatibility if needed, 
    // but better to use Target properties.
    public double? ParsedPointImageX => Target.OriginalImagePoint.X;
    public double? ParsedPointImageY => Target.OriginalImagePoint.Y;
    public string? ParsedPointLabel => Target.Label;
    
    public double? MappedScreenX => Target.MappedScreenPhysicalPoint.X;
    public double? MappedScreenY => Target.MappedScreenPhysicalPoint.Y;
    
    public bool WasPointClamped => Target.WasClamped;
    
    public string? UiElementUnderMappedPoint { get; set; }
    public string? NearestUiElement => Target.NearestUiElementName;
    public double? DistanceToNearestUiElement => Target.DistanceToNearestUiElement;
    
    public double FinalPointScreenX => Target.FinalScreenPhysicalPoint.X;
    public double FinalPointScreenY => Target.FinalScreenPhysicalPoint.Y;
    public string? AdjustmentReason => Target.AdjustmentReason;
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
