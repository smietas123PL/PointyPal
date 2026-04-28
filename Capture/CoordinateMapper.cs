using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Capture;

public class CoordinateMappingResult
{
    public Point InputPoint { get; set; }
    public Point OutputPoint { get; set; }
    public string InputSpace { get; set; } = "";
    public string OutputSpace { get; set; } = "";
    public bool WasClamped { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public Point Offset { get; set; }
}

public class CoordinateMapper
{
    public Point MapImagePointToScreenPoint(Point imagePoint, CaptureResult capture)
    {
        return ImageToScreenPhysical(imagePoint, capture.Geometry).OutputPoint;
    }

    public CoordinateMappingResult ImageToScreenPhysical(Point imagePoint, CaptureGeometry geometry)
    {
        bool wasClamped = false;
        Point clamped = ClampImagePoint(imagePoint, geometry);
        if (clamped != imagePoint) wasClamped = true;

        double physicalX = geometry.MonitorBoundsPhysical.X + (clamped.X / geometry.DownscaleFactorX);
        double physicalY = geometry.MonitorBoundsPhysical.Y + (clamped.Y / geometry.DownscaleFactorY);

        return new CoordinateMappingResult
        {
            InputPoint = imagePoint,
            OutputPoint = new Point(physicalX, physicalY),
            InputSpace = "Image",
            OutputSpace = "ScreenPhysical",
            WasClamped = wasClamped,
            ScaleX = 1.0 / geometry.DownscaleFactorX,
            ScaleY = 1.0 / geometry.DownscaleFactorY,
            Offset = new Point(geometry.MonitorBoundsPhysical.X, geometry.MonitorBoundsPhysical.Y)
        };
    }

    public CoordinateMappingResult ScreenPhysicalToImage(Point screenPoint, CaptureGeometry geometry)
    {
        bool wasClamped = false;
        Point clamped = ClampScreenPoint(screenPoint, geometry);
        if (clamped != screenPoint) wasClamped = true;

        double imageX = (clamped.X - geometry.MonitorBoundsPhysical.X) * geometry.DownscaleFactorX;
        double imageY = (clamped.Y - geometry.MonitorBoundsPhysical.Y) * geometry.DownscaleFactorY;

        return new CoordinateMappingResult
        {
            InputPoint = screenPoint,
            OutputPoint = new Point(imageX, imageY),
            InputSpace = "ScreenPhysical",
            OutputSpace = "Image",
            WasClamped = wasClamped,
            ScaleX = geometry.DownscaleFactorX,
            ScaleY = geometry.DownscaleFactorY,
            Offset = new Point(-geometry.MonitorBoundsPhysical.X, -geometry.MonitorBoundsPhysical.Y)
        };
    }

    public CoordinateMappingResult ScreenPhysicalToOverlayDip(Point screenPoint, CaptureGeometry geometry)
    {
        double dipX = screenPoint.X / geometry.DpiScaleX;
        double dipY = screenPoint.Y / geometry.DpiScaleY;

        return new CoordinateMappingResult
        {
            InputPoint = screenPoint,
            OutputPoint = new Point(dipX, dipY),
            InputSpace = "ScreenPhysical",
            OutputSpace = "OverlayDip",
            ScaleX = 1.0 / geometry.DpiScaleX,
            ScaleY = 1.0 / geometry.DpiScaleY
        };
    }

    public CoordinateMappingResult OverlayDipToScreenPhysical(Point dipPoint, CaptureGeometry geometry)
    {
        double physicalX = dipPoint.X * geometry.DpiScaleX;
        double physicalY = dipPoint.Y * geometry.DpiScaleY;

        return new CoordinateMappingResult
        {
            InputPoint = dipPoint,
            OutputPoint = new Point(physicalX, physicalY),
            InputSpace = "OverlayDip",
            OutputSpace = "ScreenPhysical",
            ScaleX = geometry.DpiScaleX,
            ScaleY = geometry.DpiScaleY
        };
    }

    public Point ClampImagePoint(Point imagePoint, CaptureGeometry geometry)
    {
        double x = Math.Max(0, Math.Min(imagePoint.X, geometry.CaptureImageWidth - 1));
        double y = Math.Max(0, Math.Min(imagePoint.Y, geometry.CaptureImageHeight - 1));
        return new Point(x, y);
    }

    public Point ClampScreenPoint(Point screenPoint, CaptureGeometry geometry)
    {
        double x = Math.Max(geometry.MonitorBoundsPhysical.Left, Math.Min(screenPoint.X, geometry.MonitorBoundsPhysical.Right - 1));
        double y = Math.Max(geometry.MonitorBoundsPhysical.Top, Math.Min(screenPoint.Y, geometry.MonitorBoundsPhysical.Bottom - 1));
        return new Point(x, y);
    }
}
