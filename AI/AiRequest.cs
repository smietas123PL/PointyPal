using System;
using System.Windows;
using Point = System.Windows.Point;
using PointyPal.Capture;
using PointyPal.Core;

namespace PointyPal.AI;

public class AiRequest
{
    public string UserText { get; set; } = "";
    public string ScreenshotPath { get; set; } = "";
    public byte[]? ScreenshotBytes { get; set; }
    public string ScreenshotBase64 { get; set; } = "";
    public string ScreenshotMimeType { get; set; } = "image/jpeg";
    public int ScreenshotWidth { get; set; }
    public int ScreenshotHeight { get; set; }
    public Rect MonitorBounds { get; set; }
    public Point CursorScreenPosition { get; set; }
    public Point CursorImagePosition { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PromptInstructions { get; set; } = "";
    public string ModelOverride { get; set; } = "";
    public InteractionMode InteractionMode { get; set; } = InteractionMode.Assist;
    public UiAutomationContext? UiAutomationContext { get; set; }
}
