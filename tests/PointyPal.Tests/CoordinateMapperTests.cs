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

    private CaptureResult CreateFakeCapture(int originalW, int originalH, int imageW, int imageH, double monitorX = 0, double monitorY = 0)
    {
        return new CaptureResult
        {
            OriginalWidth = originalW,
            OriginalHeight = originalH,
            Image = new Bitmap(imageW, imageH),
            MonitorBounds = new Rect(monitorX, monitorY, originalW, originalH)
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
    public void MapImagePointToScreenPoint_NegativeCoordinates_MapsCorrectly()
    {
        var capture = CreateFakeCapture(1920, 1080, 1920, 1080, -1920, 0); // Monitor to the left
        var imagePoint = new Point(100, 100);

        var screenPoint = _mapper.MapImagePointToScreenPoint(imagePoint, capture);

        screenPoint.X.Should().Be(-1820);
        screenPoint.Y.Should().Be(100);
    }

    [Fact]
    public void RoundTrip_MapsBackToOriginal()
    {
        var capture = CreateFakeCapture(2560, 1440, 1000, 600, 100, 100);
        var originalScreenPoint = new Point(500, 500);

        var imagePoint = _mapper.MapScreenPointToImagePoint(originalScreenPoint, capture);
        var resultScreenPoint = _mapper.MapImagePointToScreenPoint(imagePoint, capture);

        resultScreenPoint.X.Should().BeInRange(499.9, 500.1);
        resultScreenPoint.Y.Should().BeInRange(499.9, 500.1);
    }
}
