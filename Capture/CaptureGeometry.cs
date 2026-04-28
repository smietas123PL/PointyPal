using System;
using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Capture;

/// <summary>
/// Centralized metadata about a screen capture, including all coordinate spaces and scaling factors.
/// </summary>
public class CaptureGeometry
{
    public int CaptureImageWidth { get; set; }
    public int CaptureImageHeight { get; set; }
    
    public int OriginalMonitorPixelWidth { get; set; }
    public int OriginalMonitorPixelHeight { get; set; }
    
    public Rect MonitorBoundsPhysical { get; set; }
    public Rect MonitorBoundsDip { get; set; }
    
    public double DpiScaleX { get; set; } = 1.0;
    public double DpiScaleY { get; set; } = 1.0;
    
    public double DownscaleFactorX { get; set; } = 1.0;
    public double DownscaleFactorY { get; set; } = 1.0;
    
    public Point CursorScreenPhysical { get; set; }
    public Point CursorImagePoint { get; set; }
    
    public Rect VirtualScreenBounds { get; set; }
    
    public string? MonitorDeviceName { get; set; }
    
    public DateTime CaptureTimestamp { get; set; }

    public override string ToString()
    {
        return $"Img:{CaptureImageWidth}x{CaptureImageHeight}, Mon:{OriginalMonitorPixelWidth}x{OriginalMonitorPixelHeight} @ {DpiScaleX:P0}, Downscale:{DownscaleFactorX:F3}";
    }
}
