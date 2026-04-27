using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Capture;

public class CoordinateMapper
{
    public Point MapImagePointToScreenPoint(Point imagePoint, CaptureResult capture)
    {
        double scaleX = (double)capture.OriginalWidth / capture.Image.Width;
        double scaleY = (double)capture.OriginalHeight / capture.Image.Height;

        double screenX = capture.MonitorBounds.X + (imagePoint.X * scaleX);
        double screenY = capture.MonitorBounds.Y + (imagePoint.Y * scaleY);

        return new Point(screenX, screenY);
    }

    public Point MapScreenPointToImagePoint(Point screenPoint, CaptureResult capture)
    {
        double scaleX = (double)capture.Image.Width / capture.OriginalWidth;
        double scaleY = (double)capture.Image.Height / capture.OriginalHeight;

        double imageX = (screenPoint.X - capture.MonitorBounds.X) * scaleX;
        double imageY = (screenPoint.Y - capture.MonitorBounds.Y) * scaleY;

        return new Point(imageX, imageY);
    }
}
