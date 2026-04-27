using System;
using System.Collections.Generic;
using System.Windows;

namespace PointyPal.Capture;

public class UiAutomationContext
{
    public bool IsAvailable { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ActiveWindowTitle { get; set; }
    public string? ActiveProcessName { get; set; }
    public UiElementInfo? FocusedElement { get; set; }
    public UiElementInfo? ElementUnderCursor { get; set; }
    public List<UiElementInfo> NearbyElements { get; set; } = new();
    public DateTime CapturedAt { get; set; }
    public double CollectionDurationMs { get; set; }
}

public class UiElementInfo
{
    public string? Name { get; set; }
    public string? AutomationId { get; set; }
    public string? ClassName { get; set; }
    public string? ControlType { get; set; }
    public string? LocalizedControlType { get; set; }
    public Rect BoundingRectangle { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public bool HasKeyboardFocus { get; set; }
    public int? ProcessId { get; set; }
    
    // Helper for sorting
    public double DistanceFromCursor { get; set; }
}
