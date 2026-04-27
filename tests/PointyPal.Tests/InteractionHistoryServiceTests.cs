using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class InteractionHistoryServiceTests : IDisposable
{
    private readonly string _tempPath;
    private readonly ConfigService _configService;
    private readonly InteractionHistoryService _service;

    public InteractionHistoryServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jsonl");
        _configService = new ConfigService();
        _service = new InteractionHistoryService(_configService, _tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Fact]
    public async Task AddEntryAsync_WritesValidJsonLine()
    {
        var item = new InteractionHistoryItem { Mode = "Test", UserText = "Hello" };
        await _service.AddEntryAsync(item);

        File.Exists(_tempPath).Should().BeTrue();
        var lines = File.ReadAllLines(_tempPath);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("\"Mode\":\"Test\"");
        lines[0].Should().Contain("\"UserText\":\"Hello\"");
    }

    [Fact]
    public async Task AddEntryAsync_RespectsSaveHistoryFlag()
    {
        _configService.Config.SaveInteractionHistory = false;
        var item = new InteractionHistoryItem { Mode = "Test" };
        await _service.AddEntryAsync(item);

        File.Exists(_tempPath).Should().BeFalse();
    }

    [Fact]
    public void CleanupOldEntries_RemovesOldItems()
    {
        _configService.Config.InteractionHistoryRetentionDays = 1;
        _configService.Config.MaxInteractionHistoryItems = 100;

        var oldItem = new InteractionHistoryItem { Timestamp = DateTime.UtcNow.AddDays(-2), Mode = "Old" };
        var newItem = new InteractionHistoryItem { Timestamp = DateTime.UtcNow, Mode = "New" };

        File.WriteAllLines(_tempPath, new[] { 
            JsonSerializer.Serialize(oldItem), 
            JsonSerializer.Serialize(newItem) 
        });

        _service.CleanupOldEntries();

        var lines = File.ReadAllLines(_tempPath);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("\"Mode\":\"New\"");
    }

    [Fact]
    public void CleanupOldEntries_EnforcesMaxItems()
    {
        _configService.Config.InteractionHistoryRetentionDays = 30;
        _configService.Config.MaxInteractionHistoryItems = 2;

        var items = new[] {
            new InteractionHistoryItem { Timestamp = DateTime.UtcNow.AddMinutes(-3), Mode = "1" },
            new InteractionHistoryItem { Timestamp = DateTime.UtcNow.AddMinutes(-2), Mode = "2" },
            new InteractionHistoryItem { Timestamp = DateTime.UtcNow.AddMinutes(-1), Mode = "3" }
        };

        File.WriteAllLines(_tempPath, items.Select(i => JsonSerializer.Serialize(i)));

        _service.CleanupOldEntries();

        var lines = File.ReadAllLines(_tempPath);
        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.Contains("\"Mode\":\"3\""));
        lines.Should().Contain(l => l.Contains("\"Mode\":\"2\""));
    }
}
