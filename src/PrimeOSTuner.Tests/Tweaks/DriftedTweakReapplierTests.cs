using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class DriftedTweakReapplierTests
{
    private sealed class FakeTweak : ITweak
    {
        private TweakState _state;
        private readonly bool _throwsOnProbe;
        public int ApplyCalls { get; private set; }

        public FakeTweak(string id, TweakState state, bool throwsOnProbe = false, bool applySucceeds = true)
        {
            Id = id; _state = state; _throwsOnProbe = throwsOnProbe; _applySucceeds = applySucceeds;
        }

        private readonly bool _applySucceeds;
        public string Id { get; }
        public string DisplayName => Id;
        public string Description => "";
        public bool RequiresElevation => false;
        public bool IsDestructive => false;
        public bool RequiresReboot => false;

        public Task<TweakState> ProbeAsync(CancellationToken ct = default)
            => _throwsOnProbe ? throw new InvalidOperationException("probe boom") : Task.FromResult(_state);

        public Task<TweakResult> ApplyAsync(IProgress<int>? p = null, CancellationToken ct = default)
        {
            ApplyCalls++;
            if (_applySucceeds) { _state = TweakState.Applied; return Task.FromResult(TweakResult.Success()); }
            return Task.FromResult(TweakResult.Failure("nope"));
        }

        public Task<TweakResult> RevertAsync(string undo, CancellationToken ct = default) => Task.FromResult(TweakResult.Success());
        public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult("");
    }

    [Fact]
    public async Task Reapplies_only_drifted_tweaks_and_skips_already_applied()
    {
        var drifted = new FakeTweak("a.drifted", TweakState.NotApplied);
        var onAlready = new FakeTweak("b.on", TweakState.Applied);

        var result = await DriftedTweakReapplier.ReapplyAsync(
            new ITweak[] { drifted, onAlready },
            new[] { "a.drifted", "b.on" });

        result.Reapplied.Should().Be(1);
        result.AlreadyApplied.Should().Be(1);
        drifted.ApplyCalls.Should().Be(1, "the drifted tweak must be re-applied");
        onAlready.ApplyCalls.Should().Be(0, "an already-applied tweak must not be touched");
    }

    [Fact]
    public async Task Ignores_enforced_ids_that_are_not_registered_tweaks()
    {
        var t = new FakeTweak("known", TweakState.NotApplied);

        var result = await DriftedTweakReapplier.ReapplyAsync(
            new ITweak[] { t },
            new[] { "known", "ghost.removed-from-app" });

        result.Reapplied.Should().Be(1);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task A_probe_that_throws_is_counted_as_failed_not_fatal()
    {
        var boom = new FakeTweak("x.boom", TweakState.NotApplied, throwsOnProbe: true);
        var ok = new FakeTweak("y.ok", TweakState.NotApplied);

        var result = await DriftedTweakReapplier.ReapplyAsync(
            new ITweak[] { boom, ok },
            new[] { "x.boom", "y.ok" });

        result.Failed.Should().Be(1);
        result.Reapplied.Should().Be(1, "one bad tweak must not stop the others");
    }

    [Fact]
    public async Task Apply_failure_is_counted()
    {
        var failing = new FakeTweak("z.fail", TweakState.NotApplied, applySucceeds: false);

        var result = await DriftedTweakReapplier.ReapplyAsync(new ITweak[] { failing }, new[] { "z.fail" });

        result.Reapplied.Should().Be(0);
        result.Failed.Should().Be(1);
    }
}
