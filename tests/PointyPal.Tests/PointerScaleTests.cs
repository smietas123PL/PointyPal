using Xunit;
using PointyPal.Infrastructure;
using System;

namespace PointyPal.Tests;

public class PointerScaleTests
{
    [Fact]
    public void DefaultPointerScale_IsWithinReasonableRange()
    {
        var config = new AppConfig();
        Assert.True(config.PointerVisualSizeDip >= 18 && config.PointerVisualSizeDip <= 36);
        Assert.Equal(22, config.PointerVisualSizeDip);
    }

    [Fact]
    public void PointerVisualSize_ConfigValuesExist()
    {
        var config = new AppConfig 
        { 
            PointerVisualSizeDip = 100,
            PointerVisualMaxSizeDip = 36,
            PointerVisualMinSizeDip = 18
        };
        
        Assert.Equal(100, config.PointerVisualSizeDip);
        Assert.Equal(36, config.PointerVisualMaxSizeDip);
        Assert.Equal(18, config.PointerVisualMinSizeDip);
    }

    [Fact]
    public void ClampingLogic_WorksAsExpected()
    {
        // Testing the logic that will be used in the UI
        double size = 100;
        double min = 18;
        double max = 36;
        double clamped = Math.Clamp(size, min, max);
        
        Assert.Equal(36, clamped);
    }

    [Fact]
    public void GlowScale_DefaultIsReasonable()
    {
        var config = new AppConfig();
        Assert.Equal(1.10, config.PointerVisualGlowScale);
    }

    [Fact]
    public void StatusSlotScale_DefaultIsReasonable()
    {
        var config = new AppConfig();
        Assert.Equal(1.15, config.PointerStatusSlotScale);
    }
}
