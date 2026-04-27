using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using PointyPal.Capture;
using Point = System.Windows.Point;

namespace PointyPal.AI;

public class PromptPayloadBuilder
{
    public async Task<string> BuildAndSavePayloadAsync(
        string userText, 
        CaptureResult capture, 
        Rect monitorBounds, 
        Point screenCursorPos,
        UiAutomationContext? uiContext = null,
        Core.InteractionMode mode = Core.InteractionMode.Assist)
    {
        var payload = new
        {
            UserText = userText,
            InteractionMode = mode.ToString(),
            PromptProfileInstructions = PromptProfileBuilder.BuildModeInstructions(mode),
            ScreenshotPath = capture.ImagePath,
            ScreenshotWidth = capture.Image.Width,
            ScreenshotHeight = capture.Image.Height,
            MonitorBounds = new { X = monitorBounds.X, Y = monitorBounds.Y, Width = monitorBounds.Width, Height = monitorBounds.Height },
            CursorScreenPosition = new { X = screenCursorPos.X, Y = screenCursorPos.Y },
            CursorImagePosition = new { X = capture.CursorImagePosition.X, Y = capture.CursorImagePosition.Y },
            Timestamp = DateTime.UtcNow.ToString("o"),
            UiAutomationContext = uiContext,
            Instructions = ClaudePromptBuilder.BuildInstructions(mode)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string debugDir = Path.Combine(appData, "PointyPal", "debug");
        Directory.CreateDirectory(debugDir);
        
        string payloadPath = Path.Combine(debugDir, "latest-prompt-payload.json");
        await File.WriteAllTextAsync(payloadPath, json);

        if (uiContext != null)
        {
            string uiContextPath = Path.Combine(debugDir, "latest-ui-context.json");
            var uiJson = JsonSerializer.Serialize(uiContext, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(uiContextPath, uiJson);
        }
        
        return payloadPath;
    }
}
