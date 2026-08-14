using FluentAssertions;
using PrimeOSTuner.Core.History;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class OneClickOptimizerOptInTests
{
    private sealed class FakeTweak : ITweak, IOptInTweak
    {
        public FakeTweak(string id, bool optIn) { Id = id; OptIn = optIn; }
        public string Id { get; }
        public bool OptIn { get; }
        public bool Applied { get; private set; }
        public string DisplayName => Id;
        public string Description => "";
        public bool RequiresElevation => false;
        public bool IsDestructive => false;
        public bool RequiresReboot => false;
        public Task<TweakState> ProbeAsync(CancellationToken ct = default)
            => Task.FromResult(TweakState.NotApplied);
        public Task<TweakResult> ApplyAsync(IProgress<int>? p = null, CancellationToken ct = default)
        {
            Applied = true;
            return Task.FromResult(TweakResult.Success());
        }
        public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
            => Task.FromResult(TweakResult.Success());
        public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult("");
    }

    [Fact]
    public async Task One_click_never_applies_opt_in_tweaks()
    {
        // MPO disable helps some machines and paints black rectangles on others; a bundle
        // must not make that bet for the user.
        var ordinary = new FakeTweak("core.ordinary", optIn: false);
        var machineDependent = new FakeTweak("game.mpo-disable", optIn: true);
        var historyPath = Path.Combine(Path.GetTempPath(), $"hist-{Guid.NewGuid():N}.json");

        try
        {
            var optimizer = new OneClickOptimizer(
                new ITweak[] { ordinary, machineDependent }, new TweakHistory(historyPath));
            await optimizer.RunAsync();

            ordinary.Applied.Should().BeTrue();
            machineDependent.Applied.Should().BeFalse();
        }
        finally { try { File.Delete(historyPath); } catch { } }
    }

    /// <summary>
    /// The specific tweaks that have already burned users must STAY opt-in. Each earned
    /// its place here by shipping in a bundle and visibly breaking machines: MPO disable
    /// and hardware GPU scheduling paint black boxes on some GPU combinations, the
    /// visual-effects tweak restyles the desktop, and Quiet CPU trades speed for silence.
    /// If someone removes IOptInTweak from one of these during a refactor, this test is
    /// what stops the regression from shipping.
    /// </summary>
    [Fact]
    public void Tweaks_that_burned_users_stay_opt_in()
    {
        var registry = new Moq.Mock<PrimeOSTuner.Win.IRegistryClient>().Object;
        var power = new Moq.Mock<PrimeOSTuner.Win.IPowerPlanClient>().Object;

        new HwGpuSchedulingTweak(registry).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
        new VisualEffectsTweak(registry).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
        new CpuBoostLimitTweak(power).Should().BeAssignableTo<IOptInTweak>()
            .Which.OptIn.Should().BeTrue();
    }
}
