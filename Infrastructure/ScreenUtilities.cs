using System;
using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Infrastructure;

public static class ScreenUtilities
{
    public static Rect GetMonitorBounds(Point p)
    {
        var info = GetMonitorInfo(p);
        return info.BoundsPhysical;
    }

    public static MonitorInfo GetMonitorInfo(Point p)
    {
        var pt = new NativeMethods.POINT { X = (int)p.X, Y = (int)p.Y };
        IntPtr monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO();
            if (NativeMethods.GetMonitorInfo(monitor, info))
            {
                var bounds = new Rect(
                    info.rcMonitor.left,
                    info.rcMonitor.top,
                    info.rcMonitor.right - info.rcMonitor.left,
                    info.rcMonitor.bottom - info.rcMonitor.top);

                uint dpiX, dpiY;
                try
                {
                    NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                }
                catch
                {
                    dpiX = 96;
                    dpiY = 96;
                }

                double scaleX = dpiX / 96.0;
                double scaleY = dpiY / 96.0;

                return new MonitorInfo
                {
                    Handle = monitor,
                    BoundsPhysical = bounds,
                    BoundsDip = new Rect(bounds.X / scaleX, bounds.Y / scaleY, bounds.Width / scaleX, bounds.Height / scaleY),
                    DpiScaleX = scaleX,
                    DpiScaleY = scaleY
                };
            }
        }
        
        // Fallback
        return new MonitorInfo
        {
            BoundsPhysical = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
            BoundsDip = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
            DpiScaleX = 1.0,
            DpiScaleY = 1.0
        };
    }

    public class MonitorInfo
    {
        public IntPtr Handle { get; set; }
        public Rect BoundsPhysical { get; set; }
        public Rect BoundsDip { get; set; }
        public double DpiScaleX { get; set; }
        public double DpiScaleY { get; set; }
    }
}
