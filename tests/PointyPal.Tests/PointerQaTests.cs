using Xunit;
using PointyPal.Core;
using PointyPal.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PointyPal.Tests;

public class PointerQaTests
{
    [Fact]
    public void PointerQaReport_RecommendationLogic_Correct()
    {
        var config = new AppConfig();
        
        // Low sample size
        var stats1 = new PointerQualityStats { SampleSize = 3, CorrectPercentage = 100 };
        var report1 = PointerQaReport.Create(stats1, config, "1.0.0");
        Assert.Equal(PointerQaRecommendation.NeedsCalibration, report1.Recommendation);

        // Good accuracy
        var stats2 = new PointerQualityStats { SampleSize = 10, CorrectPercentage = 90, ClosePercentage = 10, WrongPercentage = 0 };
        var report2 = PointerQaReport.Create(stats2, config, "1.0.0");
        Assert.Equal(PointerQaRecommendation.Good, report2.Recommendation);

        // Many wrong
        var stats3 = new PointerQualityStats { SampleSize = 10, CorrectPercentage = 50, ClosePercentage = 10, WrongPercentage = 40 };
        var report3 = PointerQaReport.Create(stats3, config, "1.0.0");
        Assert.Equal(PointerQaRecommendation.NeedsInvestigation, report3.Recommendation);

        // Many close
        var stats4 = new PointerQualityStats { SampleSize = 10, CorrectPercentage = 60, ClosePercentage = 30, WrongPercentage = 10 };
        var report4 = PointerQaReport.Create(stats4, config, "1.0.0");
        Assert.Equal(PointerQaRecommendation.NeedsThresholdTuning, report4.Recommendation);
    }

    [Fact]
    public void PointerQualityService_Export_RedactsSensitiveData()
    {
        var config = new AppConfig();
        var service = new PointerQualityService();
        
        // Record some feedback
        var target = new PointerTarget { Label = "Sensitive Target Name", AdjustmentReason = "Test" };
        service.RecordFeedback(3, target);
        
        string exportPath = service.ExportReport(config);
        Assert.True(File.Exists(exportPath));
        
        string json = File.ReadAllText(exportPath);
        
        // Check for sensitive strings that should NOT be there
        Assert.DoesNotContain("Sensitive Target Name", json);
        Assert.Contains("\"Rating\": 3", json);
        Assert.Contains("\"AdjustmentReason\": \"Test\"", json);
    }

    [Fact]
    public void AppConfig_PointerDefaults_AreConservative()
    {
        var config = new AppConfig();
        
        // Snapping should be OFF by default for PT012 production baseline
        Assert.False(config.PointSnappingEnabled);
        
        // Durations should be in recommended ranges
        Assert.True(config.PointerFlightDurationMs >= 350 && config.PointerFlightDurationMs <= 600);
        Assert.True(config.PointerMarkerDurationMs >= 1000 && config.PointerMarkerDurationMs <= 2000);
        
        // Prompts should be OFF in Normal Mode
        Assert.True(config.PointerFeedbackPromptDeveloperOnly);
        Assert.False(config.PointerFeedbackPromptEnabled);
    }
}
