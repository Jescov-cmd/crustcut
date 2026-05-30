using FluentAssertions;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

/// <summary>
/// Verifies the Optimize-tab load logic (TweakStateInitializer) against the REAL system.
/// This is the autonomous proxy for the user's reboot test: because registry tweaks
/// persist across reboot, what the initializer reports NOW is what the tab will show on
/// the next launch. The fix being proven here is "the tab reflects real applied state"
/// — the thing that was completely missing before (every toggle always read off).
/// </summary>
[Trait("Category", "Integration")]
[Collection("RealSystemRegistry")]
public class OptimizeTabStateRealSystemTests : IDisposable
{
    private readonly string _historyPath = Path.Combine(Path.GetTempPath(), $"otsr-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_historyPath)) File.Delete(_historyPath);
    }

    [Fact]
    public async Task Initializer_reflects_real_probe_state_for_every_registry_tweak()
    {
        var registry = new RegistryClient();
        // Registry-backed tweaks read HKCU/HKLM directly (no elevation needed to READ),
        // so their probes return a definite state on any machine.
        var tweaks = new ITweak[]
        {
            new GameModeTweak(registry),
            new HwGpuSchedulingTweak(registry),
            new MouseAccelTweak(registry),
            new SnappyUiTweak(registry),
            new VisualEffectsTweak(registry),
        };
        var history = new TweakHistory(_historyPath);

        var states = await TweakStateInitializer.ComputeAsync(tweaks, history);

        // The initializer must faithfully mirror each tweak's live probe result. Before
        // the fix the tab ignored this entirely and showed everything off.
        foreach (var tweak in tweaks)
        {
            var probe = await tweak.ProbeAsync();
            var state = states.Single(s => s.TweakId == tweak.Id);
            state.IsApplied.Should().Be(probe == TweakState.Applied,
                $"{tweak.Id} tile should match its real probe state");
        }
    }

    [Fact]
    public async Task Applied_tweak_recovers_undo_so_it_can_be_toggled_off_after_reboot()
    {
        // Simulate: a previous session applied Game Mode (history has its undo data),
        // then a reboot. On reload the initializer should both detect it AND hand back
        // the undo data so the user can turn it off again.
        var registry = new RegistryClient();
        var gameMode = new GameModeTweak(registry);

        // Snapshot + apply for real (HKCU, no admin), then restore at the end.
        var applyResult = await gameMode.ApplyAsync();
        try
        {
            var history = new TweakHistory(_historyPath);
            await history.AppendAsync(new HistoryEntry(
                Guid.NewGuid(), gameMode.Id, gameMode.DisplayName, DateTime.UtcNow, applyResult.UndoData, false));

            var states = await TweakStateInitializer.ComputeAsync(new ITweak[] { gameMode }, history);

            var s = states.Single();
            s.IsApplied.Should().BeTrue();
            s.UndoData.Should().NotBeNull("the tab must recover undo data so an already-applied tweak is revertible");
        }
        finally
        {
            if (applyResult.UndoData is not null) await gameMode.RevertAsync(applyResult.UndoData);
        }
    }
}
