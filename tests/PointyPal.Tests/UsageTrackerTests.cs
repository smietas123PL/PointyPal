using System.IO;
using System.Text.Json;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class UsageTrackerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly UsageTracker _tracker;

    public UsageTrackerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        _tracker = new UsageTracker(_tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public void IncrementInteractions_IncrementsCorrectly()
    {
        int initial = _tracker.CurrentUsage.InteractionsCount;
        _tracker.IncrementInteractions();
        _tracker.CurrentUsage.InteractionsCount.Should().Be(initial + 1);
    }

    [Fact]
    public void SaveUsage_PersistsToFile()
    {
        _tracker.IncrementClaudeRequests();
        
        // Create a new tracker instance with same path
        var secondTracker = new UsageTracker(_tempPath);
        secondTracker.CurrentUsage.ClaudeRequestsCount.Should().Be(1);
    }

    [Fact]
    public void EnsureToday_ResetsOnNewDay()
    {
        // Mock a usage from yesterday
        var yesterdayUsage = new DailyUsage
        {
            Date = DateTime.Today.AddDays(-1),
            InteractionsCount = 10
        };
        string json = JsonSerializer.Serialize(yesterdayUsage);
        File.WriteAllText(_tempPath, json);

        // Tracker should detect it's not today and reset
        var newTracker = new UsageTracker(_tempPath);
        newTracker.CurrentUsage.Date.Date.Should().Be(DateTime.Today);
        newTracker.CurrentUsage.InteractionsCount.Should().Be(0);
    }
}
