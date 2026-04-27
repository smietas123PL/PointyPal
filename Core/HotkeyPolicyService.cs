using System.Collections.Generic;
using PointyPal.Infrastructure;

namespace PointyPal.Core;

public enum PointyPalHotkeyAction
{
    VoicePushToTalk,
    QuickAsk,
    CancelActiveOperation,
    CompactDiagnostics,
    FakePointerFlight,
    FakeMappedPoint,
    CenterMappingTest,
    FakeLocalInteraction,
    FakeCenterInteraction,
    FakeNoneInteraction,
    ForceClaudeProvider,
    ForceFakeProvider,
    UiAutomationCapture,
    CalibrationGrid,
    PointRating,
    RuntimeTtsToggle
}

public class HotkeyReferenceItem
{
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public bool DeveloperOnly { get; set; }
    public string Category { get; set; } = "Daily Hotkeys";
}

public class HotkeyPolicyService
{
    public const string DeveloperHotkeysHiddenMessage = "Developer hotkeys are hidden. Enable Developer Mode to view and use them.";
    public const string DeveloperHotkeysDisabledMessage = "Developer hotkeys are disabled.";

    private readonly ConfigService _configService;

    public HotkeyPolicyService(ConfigService configService)
    {
        _configService = configService;
    }

    public bool DeveloperHotkeysEnabled =>
        _configService.Config.DeveloperModeEnabled &&
        _configService.Config.EnableDeveloperHotkeys;

    public bool IsHotkeyAllowed(PointyPalHotkeyAction action)
    {
        if (IsDailyHotkey(action))
        {
            return true;
        }

        if (action == PointyPalHotkeyAction.CompactDiagnostics)
        {
            return true;
        }

        return IsDeveloperHotkey(action) && DeveloperHotkeysEnabled;
    }

    public bool IsDailyHotkey(PointyPalHotkeyAction action)
    {
        return action is
            PointyPalHotkeyAction.VoicePushToTalk or
            PointyPalHotkeyAction.QuickAsk or
            PointyPalHotkeyAction.CancelActiveOperation;
    }

    public bool IsDeveloperHotkey(PointyPalHotkeyAction action)
    {
        return action is
            PointyPalHotkeyAction.FakePointerFlight or
            PointyPalHotkeyAction.FakeMappedPoint or
            PointyPalHotkeyAction.CenterMappingTest or
            PointyPalHotkeyAction.FakeLocalInteraction or
            PointyPalHotkeyAction.FakeCenterInteraction or
            PointyPalHotkeyAction.FakeNoneInteraction or
            PointyPalHotkeyAction.ForceClaudeProvider or
            PointyPalHotkeyAction.ForceFakeProvider or
            PointyPalHotkeyAction.UiAutomationCapture or
            PointyPalHotkeyAction.CalibrationGrid or
            PointyPalHotkeyAction.PointRating or
            PointyPalHotkeyAction.RuntimeTtsToggle;
    }

    public IReadOnlyList<HotkeyReferenceItem> GetVisibleHotkeyReferenceItems()
    {
        var items = new List<HotkeyReferenceItem>
        {
            new() { Key = "Right Ctrl (Hold)", Description = "Voice Command (Push-to-Talk)", Category = "Daily Hotkeys" },
            new() { Key = "Right Ctrl (Release)", Description = "Process Voice Command", Category = "Daily Hotkeys" },
            new() { Key = "Ctrl + Space", Description = "Open Quick Ask", Category = "Daily Hotkeys" },
            new() { Key = "Escape", Description = "Cancel Active Operation", Category = "Daily Hotkeys" },
            new() { Key = "F9", Description = "Show Status Overlay", Category = "Daily Hotkeys" },
            new() { Key = "Ctrl + Alt + 1/2/3", Description = "Rate last point (1=Bad, 2=Neutral, 3=Good)", Category = "Daily Hotkeys" }
        };

        if (!_configService.Config.DeveloperModeEnabled)
        {
            items.Add(new HotkeyReferenceItem
            {
                Key = "Developer Hotkeys",
                Description = DeveloperHotkeysHiddenMessage,
                Category = "Developer Hotkeys",
                DeveloperOnly = true
            });
            return items;
        }

        items.AddRange(new[]
        {
            new HotkeyReferenceItem { Key = "F8", Description = "Fake pointer flight", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "F10", Description = "Fake mapped point", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "F11", Description = "Center mapping test", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "F12", Description = "Fake local interaction", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Shift + F12", Description = "Fake center interaction", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Ctrl + F12", Description = "Fake none interaction", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Alt + F12", Description = "Force Claude provider", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Ctrl + Alt + F12", Description = "Force fake provider", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Ctrl + F9", Description = "UI Automation capture", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Ctrl + Shift + F9", Description = "Calibration grid", DeveloperOnly = true, Category = "Developer Hotkeys" },
            new HotkeyReferenceItem { Key = "Ctrl + Shift + F12", Description = "Runtime TTS toggle", DeveloperOnly = true, Category = "Developer Hotkeys" }
        });

        return items;
    }
}
