using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace PointyPal.Infrastructure;

public class DailyUsage
{
    public DateTime Date { get; set; }
    public int InteractionsCount { get; set; }
    public int ClaudeRequestsCount { get; set; }
    public double SttSeconds { get; set; }
    public int TtsCharacters { get; set; }
    public int TtsRequests { get; set; }
    public int ErrorsCount { get; set; }
}

public class UsageTracker
{
    private readonly string _usagePath;
    private DailyUsage _currentUsage;

    public DailyUsage CurrentUsage => _currentUsage;

    public UsageTracker() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "usage.json"))
    {
    }

    internal UsageTracker(string usagePath)
    {
        _usagePath = usagePath;
        string? dir = Path.GetDirectoryName(_usagePath);
        if (dir != null) Directory.CreateDirectory(dir);
        
        _currentUsage = LoadUsage();
    }

    private DailyUsage LoadUsage()
    {
        if (File.Exists(_usagePath))
        {
            try
            {
                string json = File.ReadAllText(_usagePath);
                var usage = JsonSerializer.Deserialize<DailyUsage>(json);
                if (usage != null && usage.Date.Date == DateTime.Today)
                {
                    return usage;
                }
            }
            catch
            {
                // Fallback to new
            }
        }

        var newUsage = new DailyUsage { Date = DateTime.Today };
        SaveUsage(newUsage);
        return newUsage;
    }

    public void SaveUsage(DailyUsage usage)
    {
        try
        {
            _currentUsage = usage;
            string json = JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_usagePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save usage: {ex.Message}");
        }
    }

    public void IncrementInteractions()
    {
        EnsureToday();
        _currentUsage.InteractionsCount++;
        SaveUsage(_currentUsage);
    }

    public void IncrementClaudeRequests()
    {
        EnsureToday();
        _currentUsage.ClaudeRequestsCount++;
        SaveUsage(_currentUsage);
    }

    public void AddSttSeconds(double seconds)
    {
        EnsureToday();
        _currentUsage.SttSeconds += seconds;
        SaveUsage(_currentUsage);
    }

    public void AddTtsCharacters(int chars)
    {
        EnsureToday();
        _currentUsage.TtsCharacters += chars;
        _currentUsage.TtsRequests++;
        SaveUsage(_currentUsage);
    }

    public void IncrementErrors()
    {
        EnsureToday();
        _currentUsage.ErrorsCount++;
        SaveUsage(_currentUsage);
    }

    public void ResetDailyUsage()
    {
        _currentUsage = new DailyUsage { Date = DateTime.Today };
        SaveUsage(_currentUsage);
    }

    private void EnsureToday()
    {
        if (_currentUsage.Date.Date != DateTime.Today)
        {
            ResetDailyUsage();
        }
    }
}
