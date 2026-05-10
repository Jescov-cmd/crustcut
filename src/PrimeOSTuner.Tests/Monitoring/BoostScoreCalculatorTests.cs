using FluentAssertions;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Monitoring;

public class BoostScoreCalculatorTests
{
    [Fact]
    public async Task All_tweaks_applied_scores_one_hundred()
    {
        var tweaks = new ITweak[]
        {
            new FakeTweak("a", TweakState.Applied, false),
            new FakeTweak("b", TweakState.Applied, false),
            new FakeTweak("c", TweakState.Applied, false),
        };

        var r = await BoostScoreCalculator.ComputeAsync(tweaks);
        r.Score.Should().Be(100);
        r.Applied.Should().Be(3);
        r.Total.Should().Be(3);
        r.Tier.Should().Be("EXCELLENT");
    }

    [Fact]
    public async Task No_tweaks_applied_scores_zero()
    {
        var tweaks = new ITweak[]
        {
            new FakeTweak("a", TweakState.NotApplied, false),
            new FakeTweak("b", TweakState.NotApplied, false),
        };

        var r = await BoostScoreCalculator.ComputeAsync(tweaks);
        r.Score.Should().Be(0);
        r.Applied.Should().Be(0);
        r.Total.Should().Be(2);
    }

    [Fact]
    public async Task PartiallyApplied_counts_as_half()
    {
        var tweaks = new ITweak[]
        {
            new FakeTweak("a", TweakState.Applied, false),
            new FakeTweak("b", TweakState.PartiallyApplied, false),
        };

        var r = await BoostScoreCalculator.ComputeAsync(tweaks);
        r.Score.Should().Be(75); // (1.0 + 0.5) / 2 = 0.75
    }

    [Fact]
    public async Task Destructive_tweaks_are_excluded_from_score()
    {
        var tweaks = new ITweak[]
        {
            new FakeTweak("a", TweakState.Applied, false),
            new FakeTweak("b", TweakState.NotApplied, isDestructive: true),
        };

        var r = await BoostScoreCalculator.ComputeAsync(tweaks);
        r.Score.Should().Be(100);
        r.Total.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_state_is_excluded_from_denominator()
    {
        var tweaks = new ITweak[]
        {
            new FakeTweak("a", TweakState.Applied, false),
            new FakeTweak("b", TweakState.Unknown, false),
        };

        var r = await BoostScoreCalculator.ComputeAsync(tweaks);
        r.Score.Should().Be(100); // unknown doesn't punish
        r.Total.Should().Be(1);
    }

    [Theory]
    [InlineData(95, "EXCELLENT")]
    [InlineData(80, "GREAT")]
    [InlineData(60, "GOOD")]
    [InlineData(40, "FAIR")]
    [InlineData(10, "POOR")]
    public void TierFor_buckets_correctly(int score, string expected)
        => BoostScoreCalculator.TierFor(score).Should().Be(expected);

    private sealed class FakeTweak : ITweak
    {
        private readonly TweakState _state;
        public FakeTweak(string id, TweakState state, bool isDestructive)
        { Id = id; _state = state; IsDestructive = isDestructive; }
        public string Id { get; }
        public string DisplayName => Id;
        public string Description => "";
        public bool RequiresElevation => false;
        public bool IsDestructive { get; }
        public Task<TweakState> ProbeAsync(CancellationToken ct = default) => Task.FromResult(_state);
        public Task<TweakResult> ApplyAsync(IProgress<int>? p = null, CancellationToken ct = default) => Task.FromResult(TweakResult.Success());
        public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default) => Task.FromResult(TweakResult.Success());
        public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult("");
    }
}
