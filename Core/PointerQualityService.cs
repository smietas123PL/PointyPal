using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PointyPal.AI;
using PointyPal.Infrastructure;

namespace PointyPal.Core;

public class PointerQualityStats
{
    public double CorrectPercentage { get; set; }
    public double ClosePercentage { get; set; }
    public double WrongPercentage { get; set; }
    public int UnknownCount { get; set; }
    public int SampleSize { get; set; }
    public double OverallScore { get; set; }
}

public class FeedbackEntry
{
    public DateTime Timestamp { get; set; }
    public int Rating { get; set; } // 3=Correct, 2=Close, 1=Wrong
    public string? AdjustmentReason { get; set; }
    public bool WasSnapped { get; set; }
    public bool WasClamped { get; set; }
}

public class PointerQualityService
{
    private readonly string _storagePath;
    private List<FeedbackEntry> _history = new();

    public PointerQualityService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dataDir = Path.Combine(appData, "PointyPal", "data");
        Directory.CreateDirectory(dataDir);
        _storagePath = Path.Combine(dataDir, "pointer-feedback.json");
        LoadHistory();
    }

    public void RecordFeedback(int rating, PointerTarget target)
    {
        var entry = new FeedbackEntry
        {
            Timestamp = DateTime.Now,
            Rating = rating,
            AdjustmentReason = target.AdjustmentReason,
            WasSnapped = target.WasSnapped,
            WasClamped = target.WasClamped
        };
        
        _history.Add(entry);
        
        // Keep only last 100 entries
        if (_history.Count > 100) _history.RemoveAt(0);
        
        SaveHistory();
    }

    public PointerQualityStats GetStats()
    {
        if (_history.Count == 0)
        {
            return new PointerQualityStats { SampleSize = 0 };
        }

        int total = _history.Count;
        int correct = _history.Count(e => e.Rating == 3);
        int close = _history.Count(e => e.Rating == 2);
        int wrong = _history.Count(e => e.Rating == 1);

        double score = ((correct * 1.0) + (close * 0.5)) / total;

        return new PointerQualityStats
        {
            SampleSize = total,
            CorrectPercentage = (double)correct / total * 100,
            ClosePercentage = (double)close / total * 100,
            WrongPercentage = (double)wrong / total * 100,
            OverallScore = score * 100,
            UnknownCount = 0 // In this implementation, we don't have 'Unknown' yet
        };
    }

    public PointerQaReport GenerateReport(AppConfig config)
    {
        var stats = GetStats();
        var report = PointerQaReport.Create(stats, config, AppInfo.Version);
        
        // Add monitor info
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            report.MonitorSummary = $"{screens.Length} monitors";
            report.DpiSummary = string.Join(", ", screens.Select(s => $"{s.Bounds.Width}x{s.Bounds.Height}"));
        }
        catch { report.MonitorSummary = "Unknown"; }

        return report;
    }

    public string ExportReport(AppConfig config)
    {
        var report = GenerateReport(config);
        
        // Redacted history for export
        var redactedHistory = _history.Select(e => new {
            e.Timestamp,
            e.Rating,
            e.WasSnapped,
            e.WasClamped,
            e.AdjustmentReason
        }).ToList();

        var exportData = new
        {
            Report = report,
            History = redactedHistory,
            Config = new {
                config.PointSnappingEnabled,
                config.PointSnappingMaxDistancePx,
                config.PointSnappingPreferButtons,
                config.PointerMarkerDurationMs,
                config.PointerFlightDurationMs,
                config.PointerReturnDurationMs,
                config.PointerTargetOffsetPx
            }
        };

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string debugDir = Path.Combine(appData, "PointyPal", "debug");
        Directory.CreateDirectory(debugDir);
        string exportPath = Path.Combine(debugDir, "pointer-qa-report.json");

        string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(exportPath, json);

        return exportPath;
    }

    private void LoadHistory()
    {
        if (File.Exists(_storagePath))
        {
            try
            {
                string json = File.ReadAllText(_storagePath);
                _history = JsonSerializer.Deserialize<List<FeedbackEntry>>(json) ?? new List<FeedbackEntry>();
            }
            catch { _history = new List<FeedbackEntry>(); }
        }
    }

    private void SaveHistory()
    {
        try
        {
            string json = JsonSerializer.Serialize(_history);
            File.WriteAllText(_storagePath, json);
        }
        catch { }
    }
}
