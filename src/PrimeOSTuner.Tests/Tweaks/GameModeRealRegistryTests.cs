using FluentAssertions;
using Microsoft.Win32;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// End-to-end proof that the GameMode DWORD fix works against the REAL Windows registry
/// (HKCU, so no elevation needed). Captures the current values up front and restores them
/// exactly at the end, so it leaves the machine as it found it. This is the test that
/// proves the "reverts on reboot" bug is actually fixed on a real system, not just mocks.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RealSystemRegistry")]
public class GameModeRealRegistryTests : IDisposable
{
    private const string SubKey = @"Software\Microsoft\GameBar";
    private static readonly string[] Names = { "AllowAutoGameMode", "AutoGameModeEnabled" };
    private readonly (string Name, object? Value, RegistryValueKind Kind)[] _saved;

    public GameModeRealRegistryTests()
    {
        // Snapshot the real values so we can restore them in Dispose.
        using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: false);
        _saved = Names.Select(n =>
        {
            var v = key?.GetValue(n);
            var kind = v is null ? RegistryValueKind.Unknown : key!.GetValueKind(n);
            return (n, v, kind);
        }).ToArray();
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.CreateSubKey(SubKey, writable: true);
        if (key is null) return;
        foreach (var (name, value, kind) in _saved)
        {
            if (value is null) key.DeleteValue(name, throwOnMissingValue: false);
            else key.SetValue(name, value, kind);
        }
    }

    [Fact]
    public async Task Apply_then_Probe_reports_Applied_and_writes_real_DWORDs()
    {
        var tweak = new GameModeTweak(new RegistryClient());

        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();

        // The probe must now report Applied — this is what lights up the tile after a reboot.
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);

        // And the values must be written as REG_DWORD = 1 (the bug wrote REG_SZ "1",
        // which Windows reset to DWORD 0 on every boot).
        using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: false)!;
        foreach (var name in Names)
        {
            key.GetValueKind(name).Should().Be(RegistryValueKind.DWord);
            ((int)key.GetValue(name)!).Should().Be(1);
        }
    }

    [Fact]
    public async Task Apply_then_Revert_restores_previous_state()
    {
        // Force a known "off" baseline so the test is deterministic regardless of whether
        // Game Mode happens to be on for this user right now. Dispose() restores the real
        // values afterward, so we still leave the machine exactly as we found it.
        using (var seed = Registry.CurrentUser.CreateSubKey(SubKey, writable: true)!)
        {
            seed.SetValue("AllowAutoGameMode", 0, RegistryValueKind.DWord);
            seed.SetValue("AutoGameModeEnabled", 0, RegistryValueKind.DWord);
        }

        var tweak = new GameModeTweak(new RegistryClient());

        var applied = await tweak.ApplyAsync();
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);

        var revert = await tweak.RevertAsync(applied.UndoData!);
        revert.Succeeded.Should().BeTrue();

        // After revert the probe must return to the baseline (DWORD 0) — i.e. not Applied.
        (await tweak.ProbeAsync()).Should().NotBe(TweakState.Applied);
    }
}
