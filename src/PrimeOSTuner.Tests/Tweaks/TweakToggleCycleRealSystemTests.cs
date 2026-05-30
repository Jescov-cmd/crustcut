using FluentAssertions;
using Microsoft.Win32;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// Full real-registry toggle cycle for every HKCU optimizer (no elevation needed):
/// force an OFF baseline → Apply → probe MUST report Applied → Revert → probe MUST report
/// NOT Applied. This is the direct, automated proof of the user's complaint: "I turn it
/// off and it comes back on." Each test snapshots the affected values up front and restores
/// them in Dispose, so the machine is left exactly as found. HKLM tweaks use the identical
/// RegistryTweak/RegistryClient code path — only the hive differs — so proving it here
/// proves the logic for the elevated ones too.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RealSystemRegistry")]
public sealed class TweakToggleCycleRealSystemTests : IDisposable
{
    // Every (subkey, value) any tested tweak touches, so we can snapshot/restore exactly.
    private static readonly (string Sub, string Name)[] Touched =
    {
        (@"Software\Microsoft\GameBar", "AllowAutoGameMode"),
        (@"Software\Microsoft\GameBar", "AutoGameModeEnabled"),
        (@"Control Panel\Mouse", "MouseSpeed"),
        (@"Control Panel\Mouse", "MouseThreshold1"),
        (@"Control Panel\Mouse", "MouseThreshold2"),
        (@"Control Panel\Desktop", "MenuShowDelay"),
        (@"Control Panel\Desktop", "HungAppTimeout"),
        (@"Control Panel\Desktop", "WaitToKillAppTimeout"),
        (@"Control Panel\Desktop", "AutoEndTasks"),
        (@"Control Panel\Accessibility\StickyKeys", "Flags"),
        (@"Control Panel\Accessibility\Keyboard Response", "Flags"),
        (@"Control Panel\Accessibility\ToggleKeys", "Flags"),
        (@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"),
        (@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations"),
        (@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect"),
        (@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow"),
        (@"Control Panel\Desktop\WindowMetrics", "MinAnimate"),
    };

    private readonly List<(string Sub, string Name, object? Value, RegistryValueKind Kind)> _saved = new();

    public TweakToggleCycleRealSystemTests()
    {
        foreach (var (sub, name) in Touched)
        {
            using var key = Registry.CurrentUser.OpenSubKey(sub, writable: false);
            var v = key?.GetValue(name);
            var kind = v is null ? RegistryValueKind.Unknown : key!.GetValueKind(name);
            _saved.Add((sub, name, v, kind));
        }
    }

    public void Dispose()
    {
        foreach (var (sub, name, value, kind) in _saved)
        {
            using var key = Registry.CurrentUser.CreateSubKey(sub, writable: true);
            if (key is null) continue;
            if (value is null) key.DeleteValue(name, throwOnMissingValue: false);
            else key.SetValue(name, value, kind);
        }
    }

    private static void DeleteBaseline()
    {
        foreach (var (sub, name) in Touched)
        {
            using var key = Registry.CurrentUser.OpenSubKey(sub, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    private static async Task AssertToggleCycle(ITweak tweak)
    {
        DeleteBaseline();

        // OFF baseline.
        (await tweak.ProbeAsync()).Should().NotBe(TweakState.Applied, $"{tweak.Id} should start off after baseline");

        // Apply → ON.
        var apply = await tweak.ApplyAsync();
        apply.Succeeded.Should().BeTrue($"{tweak.Id} apply should succeed");
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied, $"{tweak.Id} should read Applied after apply");

        // Revert → OFF (this is the bit that was bouncing back on).
        var revert = await tweak.RevertAsync(apply.UndoData!);
        revert.Succeeded.Should().BeTrue($"{tweak.Id} revert should succeed");
        (await tweak.ProbeAsync()).Should().NotBe(TweakState.Applied, $"{tweak.Id} should read NOT Applied after revert");
    }

    [Fact] public Task GameMode_toggle_cycle() => AssertToggleCycle(new GameModeTweak(new RegistryClient()));
    [Fact] public Task MouseAccel_toggle_cycle() => AssertToggleCycle(new MouseAccelTweak(new RegistryClient()));
    [Fact] public Task SnappyUi_toggle_cycle() => AssertToggleCycle(new SnappyUiTweak(new RegistryClient()));
    [Fact] public Task StickyKeys_toggle_cycle() => AssertToggleCycle(new StickyKeysTweak(new RegistryClient()));
    [Fact] public Task VisualEffects_toggle_cycle() => AssertToggleCycle(new VisualEffectsTweak(new RegistryClient()));

    [Theory]
    [InlineData("core.startup-delay")]
    [InlineData("core.game-dvr-disable")]
    [InlineData("core.fullscreen-optimizations")]
    [InlineData("core.advertising-id")]
    [InlineData("core.feedback-diagnostics")]
    [InlineData("core.typing-personalization")]
    public async Task Catalog_hkcu_tweak_toggle_cycle(string id)
    {
        var def = RegistryTweakCatalog.LoadFromFile(RegistryTweakCatalog.DefaultPath())
            .Single(d => d.Id == id);
        def.Hive.Should().Be("CurrentUser", "this test only covers HKCU catalog tweaks (no elevation)");

        var registry = new RegistryClient();
        // Snapshot + restore this catalog value specifically (not in the static Touched set).
        object? saved; RegistryValueKind savedKind;
        using (var k = Registry.CurrentUser.OpenSubKey(def.Key, writable: false))
        {
            saved = k?.GetValue(def.ValueName);
            savedKind = saved is null ? RegistryValueKind.Unknown : k!.GetValueKind(def.ValueName);
        }
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(def.Key, writable: true))
                k?.DeleteValue(def.ValueName, throwOnMissingValue: false);

            var tweak = new RegistryTweak(def, registry);
            (await tweak.ProbeAsync()).Should().NotBe(TweakState.Applied);
            var apply = await tweak.ApplyAsync();
            (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
            await tweak.RevertAsync(apply.UndoData!);
            (await tweak.ProbeAsync()).Should().NotBe(TweakState.Applied);
        }
        finally
        {
            using var k = Registry.CurrentUser.CreateSubKey(def.Key, writable: true);
            if (k is not null)
            {
                if (saved is null) k.DeleteValue(def.ValueName, throwOnMissingValue: false);
                else k.SetValue(def.ValueName, saved, savedKind);
            }
        }
    }
}
