using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PointyPal.Infrastructure;
using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Capture;

public class ScreenCaptureService
{
    public CaptureResult CaptureCurrentCursorMonitor(int maxWidth = 1280, int jpegQuality = 70, bool saveToDisk = true)
    {
        NativeMethods.POINT cursorPt;
        if (!NativeMethods.GetCursorPos(out cursorPt))
        {
            throw new InvalidOperationException("Could not get cursor position.");
        }

        var cursorScreenPoint = new Point(cursorPt.X, cursorPt.Y);
        var monitorInfo = ScreenUtilities.GetMonitorInfo(cursorScreenPoint);
        var monitorBounds = monitorInfo.BoundsPhysical;

        int originalWidth = (int)monitorBounds.Width;
        int originalHeight = (int)monitorBounds.Height;

        using var bitmap = new Bitmap(originalWidth, originalHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.CopyFromScreen(
            (int)monitorBounds.X, (int)monitorBounds.Y, 
            0, 0, 
            bitmap.Size, 
            CopyPixelOperation.SourceCopy);

        int targetWidth = originalWidth;
        int targetHeight = originalHeight;

        if (originalWidth > maxWidth)
        {
            targetWidth = maxWidth;
            targetHeight = (int)((double)originalHeight / originalWidth * maxWidth);
        }

        var scaledBitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using var scaledGraphics = Graphics.FromImage(scaledBitmap);
        scaledGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        scaledGraphics.SmoothingMode = SmoothingMode.HighQuality;
        scaledGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        scaledGraphics.DrawImage(bitmap, new Rectangle(0, 0, targetWidth, targetHeight));

        double downscaleFactorX = (double)targetWidth / originalWidth;
        double downscaleFactorY = (double)targetHeight / originalHeight;

        double cursorImageX = (cursorPt.X - monitorBounds.X) * downscaleFactorX;
        double cursorImageY = (cursorPt.Y - monitorBounds.Y) * downscaleFactorY;

        string imagePath = saveToDisk ? SaveDebugImage(scaledBitmap, jpegQuality) : string.Empty;

        var virtualBounds = new System.Windows.Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var geometry = new CaptureGeometry
        {
            CaptureImageWidth = targetWidth,
            CaptureImageHeight = targetHeight,
            OriginalMonitorPixelWidth = originalWidth,
            OriginalMonitorPixelHeight = originalHeight,
            MonitorBoundsPhysical = monitorBounds,
            MonitorBoundsDip = monitorInfo.BoundsDip,
            DpiScaleX = monitorInfo.DpiScaleX,
            DpiScaleY = monitorInfo.DpiScaleY,
            DownscaleFactorX = downscaleFactorX,
            DownscaleFactorY = downscaleFactorY,
            CursorScreenPhysical = cursorScreenPoint,
            CursorImagePoint = new System.Windows.Point(cursorImageX, cursorImageY),
            VirtualScreenBounds = virtualBounds,
            CaptureTimestamp = DateTime.Now
        };

        return new CaptureResult
        {
            Image = scaledBitmap,
            OriginalWidth = originalWidth,
            OriginalHeight = originalHeight,
            MonitorBounds = monitorBounds,
            CursorScreenPosition = cursorScreenPoint,
            CursorImagePosition = geometry.CursorImagePoint,
            CaptureTimestamp = geometry.CaptureTimestamp,
            ImagePath = imagePath,
            Geometry = geometry
        };
    }

    private string SaveDebugImage(Bitmap bitmap, int quality)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            Directory.CreateDirectory(debugDir);
            
            string filePath = Path.Combine(debugDir, "latest-capture.jpg");

            ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
            if (jpegCodec != null)
            {
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                bitmap.Save(filePath, jpegCodec, encoderParams);
            }
            else
            {
                bitmap.Save(filePath, ImageFormat.Jpeg);
            }
            return filePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save debug image: {ex.Message}");
            return string.Empty;
        }
    }
}
