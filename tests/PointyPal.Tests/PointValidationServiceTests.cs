using System.Drawing;
using System.Windows;
using FluentAssertions;
using PointyPal.AI;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using Xunit;
using Point = System.Windows.Point;

namespace PointyPal.Tests;

public class PointValidationServiceTests
{
    private readonly ConfigService _configService;
    private readonly PointValidationService _service;

    public PointValidationServiceTests()
    {
        _configService = new ConfigService();
        _service = new PointValidationService(_configService);
    }

    private CaptureResult CreateFakeCapture(int imageW, int imageH, double monitorX = 0, double monitorY = 0)
    {
        var geometry = new CaptureGeometry
        {
            MonitorBoundsPhysical = new Rect(monitorX, monitorY, 1920, 1080),
            MonitorBoundsDip = new Rect(monitorX, monitorY, 1920, 1080),
            DpiScaleX = 1.0,
            DpiScaleY = 1.0,
            DownscaleFactorX = (double)imageW / 1920,
            DownscaleFactorY = (double)imageH / 1080,
            CaptureImageWidth = imageW,
            CaptureImageHeight = imageH,
            VirtualScreenBounds = new Rect(-5000, -5000, 10000, 10000)
        };

        return new CaptureResult
        {
            Image = new Bitmap(imageW, imageH),
            Geometry = geometry
        };
    }

    [Fact]
    public void ProcessPoint_ValidPoint_ReturnsIsValidTrue()
    {
        var tag = new PointTag { HasPoint = true, X = 100, Y = 100, Label = "test" };
        var capture = CreateFakeCapture(1920, 1080);
        var mappedPoint = new Point(100, 100);

        var (attempt, validation) = _service.ProcessPoint(tag, capture, null, mappedPoint, "user", "provider", "raw", "clean");

        validation.IsValid.Should().BeTrue();
        validation.InImageBounds.Should().BeTrue();
        attempt.WasPointClamped.Should().BeFalse();
    }

    [Fact]
    public void ProcessPoint_OutOfBoundsPoint_IsClamped()
    {
        var tag = new PointTag { HasPoint = true, X = 2000, Y = 1100, Label = "out" };
        var capture = CreateFakeCapture(1920, 1080);
        var mappedPoint = new Point(2000, 1100);

        var (attempt, validation) = _service.ProcessPoint(tag, capture, null, mappedPoint, "user", "provider", "raw", "clean");

        validation.InImageBounds.Should().BeFalse();
        validation.WasClamped.Should().BeTrue();
        attempt.WasPointClamped.Should().BeTrue();
    }

    [Fact]
    public void ProcessPoint_SnappingDisabled_DoesNotAdjustPoint()
    {
        _configService.Config.PointSnappingEnabled = false;
        var tag = new PointTag { HasPoint = true, X = 100, Y = 100, Label = "button" };
        var capture = CreateFakeCapture(1920, 1080);
        var mappedPoint = new Point(100, 100);
        
        var uiContext = new UiAutomationContext
        {
            IsAvailable = true,
            NearbyElements = new System.Collections.Generic.List<UiElementInfo>
            {
                new UiElementInfo { Name = "RealButton", ControlType = "ControlType.Button", BoundingRectangle = new Rect(105, 105, 50, 50) }
            }
        };

        var (attempt, _) = _service.ProcessPoint(tag, capture, uiContext, mappedPoint, "user", "provider", "raw", "clean");

        attempt.FinalPointScreenX.Should().Be(100);
        attempt.FinalPointScreenY.Should().Be(100);
    }

    [Fact]
    public void ProcessPoint_SnappingEnabled_AdjustsToNearestElement()
    {
        _configService.Config.PointSnappingEnabled = true;
        _configService.Config.PointSnappingMaxDistancePx = 100; // Increased to be safe
        
        var tag = new PointTag { HasPoint = true, X = 100, Y = 100, Label = "button" };
        var capture = CreateFakeCapture(1920, 1080);
        var mappedPoint = new Point(100, 100);
        
        // Element center is at (130, 130). Distance from (100, 100) is ~42.4.
        var uiContext = new UiAutomationContext
        {
            IsAvailable = true,
            NearbyElements = new System.Collections.Generic.List<UiElementInfo>
            {
                new UiElementInfo { Name = "RealButton", ControlType = "ControlType.Button", BoundingRectangle = new Rect(105, 105, 50, 50) }
            }
        };

        var (attempt, _) = _service.ProcessPoint(tag, capture, uiContext, mappedPoint, "user", "provider", "raw", "clean");

        attempt.FinalPointScreenX.Should().Be(130);
        attempt.FinalPointScreenY.Should().Be(130);
        attempt.AdjustmentReason.Should().Be("SnappedToNearestButton");
    }
}
