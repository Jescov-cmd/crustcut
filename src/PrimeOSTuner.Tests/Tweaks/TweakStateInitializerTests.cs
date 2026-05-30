using FluentAssertions;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TweakStateInitializerTests : IDisposable
{
    private readonly string _historyPath = Path.Combine(Path.GetTempPath(), $"tsi-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_historyPath)) File.Delete(_historyPath);
    }

    private sealed class StubTweak : ITweak
    {
        private readonly TweakState _state;
        private readonly bool _throws;
        public StubTweak(string id, TweakState state, bool throws = false) { Id = id; _state = state; _throws = throws; }
        public string Id { get; }
        public string DisplayName => Id;
        public string Description => "";
        public bool RequiresElevation => false;
        public bool IsDestructive => false;
        public bool RequiresReboot => false;
        public Task<TweakState> ProbeAsync(CancellationToken ct = default)
            => _throws ? throw new InvalidOperationException("probe blew up") : Task.FromResult(_state);
        public Task<TweakResult> ApplyAsync(IProgress<int>? p = null, CancellationToken ct = default) => Task.FromResult(TweakResult.Success());
        public Task<TweakResult> RevertAsync(string undo, CancellationToken ct = default) => Task.FromResult(TweakResult.Success());
        public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult("");
    }

    [Fact]
    public async Task Applied_tweak_with_history_recovers_undo_data()
    {
        var history = new TweakHistory(_historyPath);
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.applied", "Applied", DateTime.UtcNow, "undo-payload", false));

        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.applied", TweakState.Applied) }, history);

        var s = states.Single();
        s.IsApplied.Should().BeTrue();
        s.UndoData.Should().Be("undo-payload");
    }

    [Fact]
    public async Task NotApplied_tweak_is_off_and_drops_stale_undo_data()
    {
        var history = new TweakHistory(_historyPath);
        // History still claims it was applied, but the live probe says it's gone now
        // (e.g. Windows reverted it). The tile must reflect the probe, not history.
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.gone", "Gone", DateTime.UtcNow, "stale", false));

        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.gone", TweakState.NotApplied) }, history);

        var s = states.Single();
        s.IsApplied.Should().BeFalse();
        s.UndoData.Should().BeNull();
    }

    [Fact]
    public async Task Applied_tweak_without_history_has_no_undo_data()
    {
        var history = new TweakHistory(_historyPath);
        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.manual", TweakState.Applied) }, history);

        var s = states.Single();
        s.IsApplied.Should().BeTrue();
        s.UndoData.Should().BeNull();
    }

    [Fact]
    public async Task Recovers_earliest_undo_not_the_poisoned_latest_one()
    {
        // Reproduces the real bug: a tweak applied repeatedly. The first apply captured
        // the pristine "off" value; later re-applies captured the already-ON value as
        // their "previous". Reverting to the latest would be a no-op (turns right back
        // on). We must recover the EARLIEST (pristine) undo.
        var history = new TweakHistory(_historyPath);
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.spam", "Spam", DateTime.UtcNow.AddMinutes(-3), "PRISTINE-off", false));
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.spam", "Spam", DateTime.UtcNow.AddMinutes(-2), "poisoned-on", false));
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.spam", "Spam", DateTime.UtcNow.AddMinutes(-1), "poisoned-on", false));

        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.spam", TweakState.Applied) }, history);

        states.Single().UndoData.Should().Be("PRISTINE-off");
    }

    [Fact]
    public async Task Apply_after_revert_starts_a_fresh_pristine_baseline()
    {
        var history = new TweakHistory(_historyPath);
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.y", "Y", DateTime.UtcNow.AddMinutes(-3), "old-pristine", false));
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.y", "Y", DateTime.UtcNow.AddMinutes(-2), null, true));   // reverted -> chain reset
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.y", "Y", DateTime.UtcNow.AddMinutes(-1), "new-pristine", false));

        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.y", TweakState.Applied) }, history);

        states.Single().UndoData.Should().Be("new-pristine");
    }

    [Fact]
    public async Task Reverted_history_entry_clears_recovered_undo()
    {
        var history = new TweakHistory(_historyPath);
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.x", "X", DateTime.UtcNow, "undo", false));
        await history.AppendAsync(new HistoryEntry(Guid.NewGuid(), "t.x", "X", DateTime.UtcNow, null, true));

        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.x", TweakState.Applied) }, history);

        states.Single().UndoData.Should().BeNull();
    }

    [Fact]
    public async Task Probe_that_throws_is_treated_as_not_applied()
    {
        var history = new TweakHistory(_historyPath);
        var states = await TweakStateInitializer.ComputeAsync(
            new[] { new StubTweak("t.boom", TweakState.Applied, throws: true) }, history);

        states.Single().IsApplied.Should().BeFalse();
    }
}
