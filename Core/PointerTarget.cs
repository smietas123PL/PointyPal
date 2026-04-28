using System.Windows;
using PointyPal.Capture;

namespace PointyPal.Core;

public enum PointSource
{
    ClaudePoint,
    UiAutomationSnap,
    Calibration,
    Replay,
    Test,
    Unknown
}

public enum PointConfidence
{
    High,
    Medium,
    Low,
    Unknown
}

public class PointerTarget
{
    public Point OriginalImagePoint { get; set; }
    public Point ClampedImagePoint { get; set; }
    public Point MappedScreenPhysicalPoint { get; set; }
    public Point FinalScreenPhysicalPoint { get; set; }
    public Point FinalOverlayDipPoint { get; set; }
    
    public string Label { get; set; } = string.Empty;
    public PointSource Source { get; set; } = PointSource.Unknown;
    public PointConfidence Confidence { get; set; } = PointConfidence.Unknown;
    
    public string? AdjustmentReason { get; set; }
    public bool WasClamped { get; set; }
    public bool WasSnapped { get; set; }
    
    public string? NearestUiElementName { get; set; }
    public string? NearestUiElementType { get; set; }
    public double? DistanceToNearestUiElement { get; set; }
    
    public string? MonitorDeviceName { get; set; }
    public string? CaptureGeometrySummary { get; set; }

    public override string ToString()
    {
        return $"Target: {Label} @ ({FinalScreenPhysicalPoint.X:F0}, {FinalScreenPhysicalPoint.Y:F0}) - {Source} ({Confidence})";
    }
}
