using System;
using System.Collections.Generic;
using System.Linq;

namespace PointyPal.Core;

public enum PointerQaRecommendation
{
    Good,
    NeedsCalibration,
    NeedsThresholdTuning,
    NeedsInvestigation
}

public class PointerQaReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string AppVersion { get; set; } = "";
    
    public string MonitorSummary { get; set; } = "";
    public string DpiSummary { get; set; } = "";
    
    public int TotalAttempts { get; set; }
    public int CorrectCount { get; set; }
    public int CloseCount { get; set; }
    public int WrongCount { get; set; }
    public int UnknownCount { get; set; }
    
    public double CorrectPercent { get; set; }
    public double ClosePercent { get; set; }
    public double WrongPercent { get; set; }
    
    public bool SnappingEnabled { get; set; }
    public int SnappingThresholdPx { get; set; }
    public int MarkerDurationMs { get; set; }
    public int FlightDurationMs { get; set; }
    
    public string Notes { get; set; } = "";
    public PointerQaRecommendation Recommendation { get; set; }

    public static PointerQaReport Create(
        PointerQualityStats stats, 
        Infrastructure.AppConfig config, 
        string appVersion)
    {
        var report = new PointerQaReport
        {
            AppVersion = appVersion,
            TotalAttempts = stats.SampleSize,
            CorrectCount = (int)Math.Round(stats.SampleSize * stats.CorrectPercentage / 100.0),
            CloseCount = (int)Math.Round(stats.SampleSize * stats.ClosePercentage / 100.0),
            WrongCount = (int)Math.Round(stats.SampleSize * stats.WrongPercentage / 100.0),
            UnknownCount = stats.UnknownCount,
            
            CorrectPercent = stats.CorrectPercentage,
            ClosePercent = stats.ClosePercentage,
            WrongPercent = stats.WrongPercentage,
            
            SnappingEnabled = config.PointSnappingEnabled,
            SnappingThresholdPx = config.PointSnappingMaxDistancePx,
            MarkerDurationMs = config.PointerMarkerDurationMs,
            FlightDurationMs = config.PointerFlightDurationMs
        };

        // Recommendation Logic (Part 9)
        if (report.TotalAttempts < 5)
        {
            report.Recommendation = PointerQaRecommendation.NeedsCalibration;
        }
        else if (report.WrongPercent > 25)
        {
            report.Recommendation = PointerQaRecommendation.NeedsInvestigation;
        }
        else if (report.ClosePercent + report.WrongPercent > 35)
        {
            report.Recommendation = PointerQaRecommendation.NeedsThresholdTuning;
        }
        else
        {
            report.Recommendation = PointerQaRecommendation.Good;
        }

        return report;
    }
}
