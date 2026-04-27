using System;
using System.IO;
using FluentAssertions;
using PointyPal.Core;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class HotkeyPolicyServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigService _configService;

    public HotkeyPolicyServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PointyPalTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _configService = new ConfigService(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void DailyHotkeys_AreAllowedInNormalMode()
    {
        var policy = new HotkeyPolicyService(_configService);

        policy.IsHotkeyAllowed(PointyPalHotkeyAction.VoicePushToTalk).Should().BeTrue();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.QuickAsk).Should().BeTrue();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.CancelActiveOperation).Should().BeTrue();
    }

    [Fact]
    public void DeveloperHotkeys_AreBlockedInNormalMode()
    {
        var policy = new HotkeyPolicyService(_configService);

        policy.IsHotkeyAllowed(PointyPalHotkeyAction.FakeLocalInteraction).Should().BeFalse();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.ForceFakeProvider).Should().BeFalse();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.UiAutomationCapture).Should().BeFalse();
    }

    [Fact]
    public void DeveloperHotkeys_AreAllowedWhenDeveloperModeAndHotkeysEnabled()
    {
        _configService.Config.DeveloperModeEnabled = true;
        _configService.Config.EnableDeveloperHotkeys = true;

        var policy = new HotkeyPolicyService(_configService);

        policy.IsHotkeyAllowed(PointyPalHotkeyAction.FakeLocalInteraction).Should().BeTrue();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.ForceFakeProvider).Should().BeTrue();
        policy.IsHotkeyAllowed(PointyPalHotkeyAction.CalibrationGrid).Should().BeTrue();
    }

    [Fact]
    public void HotkeyReference_HidesDeveloperHotkeysInNormalMode()
    {
        var policy = new HotkeyPolicyService(_configService);

        var items = policy.GetVisibleHotkeyReferenceItems();

        items.Should().Contain(i => i.Description == HotkeyPolicyService.DeveloperHotkeysHiddenMessage);
        items.Should().NotContain(i => i.Key == "F12");
        items.Should().Contain(i => i.Key == "Ctrl + Space");
    }
}
