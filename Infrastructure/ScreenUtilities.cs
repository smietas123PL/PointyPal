using System;
using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Infrastructure;

public static class ScreenUtilities
{
    public static Rect GetMonitorBounds(Point p)
    {
        var pt = new NativeMethods.POINT { X = (int)p.X, Y = (int)p.Y };
        IntPtr monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMethods.MONITORINFO();
            if (NativeMethods.GetMonitorInfo(monitor, info))
            {
                return new Rect(
                    info.rcMonitor.left,
                    info.rcMonitor.top,
                    info.rcMonitor.right - info.rcMonitor.left,
                    info.rcMonitor.bottom - info.rcMonitor.top);
            }
        }
        
        // Fallback
        return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }
}
