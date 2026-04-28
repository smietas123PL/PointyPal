using System.Drawing;
using System.Windows;
using FluentAssertions;
using PointyPal.Capture;
using Xunit;
using Point = System.Windows.Point;

namespace PointyPal.Tests;

public class CoordinateMapperTests
{
    private readonly CoordinateMapper _mapper = new();

    private CaptureResult CreateFakeCapture(int physicalW, int physicalH, int imageW, int imageH, double monitorX = 0, double monitorY = 0)
    {
        var geometry = new CaptureGeometry
        {
            MonitorBoundsPhysical = new Rect(monitorX, monitorY, physicalW, physicalH),
            MonitorBoundsDip = new Rect(monitorX, monitorY, physicalW, physicalH), // Assume 1:1 for simplicity
            DpiScaleX = 1.0,
            DpiScaleY = 1.0,
            DownscaleFactorX = (double)imageW / physicalW,
            DownscaleFactorY = (double)imageH / physicalH,
            CaptureImageWidth = imageW,
            CaptureImageHeight = imageH,
            VirtualScreenBounds = new Rect(-5000, -5000, 10000, 10000)
        };

        return new CaptureResult
        {
            OriginalWidth = physicalW,
            OriginalHeight = physicalH,
            Image = new Bitmap(imageW, imageH),
            Geometry = geometry
        };
    }

    [Fact]
    public void MapImagePointToScreenPoint_Center_MapsCorrectly()
    {
        var capture = CreateFakeCapture(1920, 1080, 960, 540);
        var imagePoint = new Point(480, 270); // Center of 960x540

        var screenPoint = _mapper.MapImagePointToScreenPoint(imagePoint, capture);

        screenPoint.X.Should().Be(960);
        screenPoint.Y.Should().Be(540);
    }

    [Fact]
    public void MapImagePointToScreenPoint_NonPrimaryMonitor_MapsCorrectly()
    {
        var capture = CreateFakeCapture(1920, 1080, 1920, 1080, 1920, 0); // Second monitor to the right
        var imagePoint = new Point(100, 100);

        var screenPoint = _mapper.MapImagePointToScreenPoint(imagePoint, capture);

        screenPoint.X.Should().Be(2020);
        screenPoint.Y.Should().Be(100);
    }

    [Fact]
    public void RoundTrip_MapsBackToOriginal()
    {
        var capture = CreateFakeCapture(2560, 1440, 1280, 720, 100, 100);
        var originalScreenPoint = new Point(500, 500);

        var imageResult = _mapper.ScreenPhysicalToImage(originalScreenPoint, capture.Geometry);
        var resultScreenPoint = _mapper.MapImagePointToScreenPoint(imageResult.OutputPoint, capture);

        resultScreenPoint.X.Should().BeInRange(499.9, 500.1);
        resultScreenPoint.Y.Should().BeInRange(499.9, 500.1);
    }

    [Fact]
    public void ScreenPhysicalToOverlayDip_HighDpi_MapsCorrectly()
    {
        var geometry = new CaptureGeometry
        {
            DpiScaleX = 1.5,
            DpiScaleY = 1.5
        };
        var screenPoint = new Point(150, 300);

        var result = _mapper.ScreenPhysicalToOverlayDip(screenPoint, geometry);

        result.OutputPoint.X.Should().Be(100);
        result.OutputPoint.Y.Should().Be(200);
    }
}
