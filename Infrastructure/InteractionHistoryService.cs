using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PointyPal.Infrastructure;

public class InteractionHistoryItem
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Mode { get; set; } = "";
    public string UserText { get; set; } = "";
    public string Provider { get; set; } = "";
    public string CleanResponse { get; set; } = "";
    public bool HadPoint { get; set; }
    public string PointLabel { get; set; } = "";
    public long DurationMs { get; set; }
    public string? Error { get; set; }
    public string? RequestId { get; set; }
}

public class InteractionHistoryService
{
    private readonly ConfigService _configService;
    private readonly string _historyPath;

    public InteractionHistoryService(ConfigService configService) 
        : this(configService, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointyPal", "history", "interactions.jsonl"))
    {
    }

    internal InteractionHistoryService(ConfigService configService, string historyPath)
    {
        _configService = configService;
        _historyPath = historyPath;
        
        string? dir = Path.GetDirectoryName(_historyPath);
        if (dir != null) Directory.CreateDirectory(dir);
    }

    public async Task AddEntryAsync(InteractionHistoryItem item)
    {
        if (!_configService.Config.SaveInteractionHistory) return;

        try
        {
            string json = JsonSerializer.Serialize(item);
            await File.AppendAllLinesAsync(_historyPath, new[] { json });
            
            // Periodically cleanup
            if (new Random().Next(0, 10) == 0) // 10% chance on write
            {
                CleanupOldEntries();
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Failed to save interaction history: {ex.Message}");
        }
    }

    public void CleanupOldEntries()
    {
        if (!File.Exists(_historyPath)) return;

        try
        {
            var config = _configService.Config;
            var lines = File.ReadAllLines(_historyPath);
            var cutoff = DateTime.UtcNow.AddDays(-config.InteractionHistoryRetentionDays);

            var validEntries = lines
                .Select(l => {
                    try { return JsonSerializer.Deserialize<InteractionHistoryItem>(l); }
                    catch { return null; }
                })
                .Where(e => e != null && e.Timestamp > cutoff)
                .OrderByDescending(e => e.Timestamp)
                .Take(config.MaxInteractionHistoryItems)
                .OrderBy(e => e.Timestamp)
                .Select(e => JsonSerializer.Serialize(e))
                .ToList();

            if (validEntries.Count < lines.Length)
            {
                File.WriteAllLines(_historyPath, validEntries);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogStatic($"Failed to cleanup interaction history: {ex.Message}");
        }
    }

    public void ClearHistory()
    {
        if (File.Exists(_historyPath))
        {
            File.Delete(_historyPath);
        }
    }

    public string GetHistoryPath() => _historyPath;

    public int GetItemCount()
    {
        if (!File.Exists(_historyPath)) return 0;
        return File.ReadLines(_historyPath).Count();
    }
}
