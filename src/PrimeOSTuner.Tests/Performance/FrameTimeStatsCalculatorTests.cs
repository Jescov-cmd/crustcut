using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameTimeStatsCalculatorTests
{
    [Fact]
    public void Compute_returns_60fps_when_every_frame_is_exactly_16_67ms()
    {
        var samples = Enumerable.Repeat(1000.0 / 60.0, 1000).ToList();

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.AvgFps.Should().BeApproximately(60.0, 0.01);
        stats.OnePctLowFps.Should().BeApproximately(60.0, 0.01);
        stats.SampleCount.Should().Be(1000);
    }

    [Fact]
    public void Compute_one_percent_low_reflects_the_worst_one_percent_of_frames()
    {
        // 990 frames at 60fps (16.67ms) + 10 frames at 30fps (33.33ms).
        // The 99th percentile sits in the 30fps tail, so 1% low FPS ≈ 30.
        var samples = Enumerable.Repeat(16.67, 990)
            .Concat(Enumerable.Repeat(33.33, 10))
            .ToList();

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.AvgFps.Should().BeApproximately(60.0, 1.0);   // average ~60
        stats.OnePctLowFps.Should().BeLessThan(35);          // 1% low is in the slow tail
        stats.OnePctLowFps.Should().BeGreaterThan(28);
    }

    [Fact]
    public void Compute_returns_zero_stats_for_an_empty_sample_set()
    {
        var stats = FrameTimeStatsCalculator.Compute(new List<double>());

        stats.SampleCount.Should().Be(0);
        stats.AvgFps.Should().Be(0);
        stats.OnePctLowFps.Should().Be(0);
        stats.MaxFrameTimeMs.Should().Be(0);
    }

    [Fact]
    public void Compute_handles_a_single_sample_without_throwing()
    {
        var stats = FrameTimeStatsCalculator.Compute(new List<double> { 16.67 });

        stats.SampleCount.Should().Be(1);
        stats.AvgFps.Should().BeApproximately(60.0, 0.1);
        stats.MaxFrameTimeMs.Should().BeApproximately(16.67, 0.001);
    }

    [Fact]
    public void Compute_ignores_zero_and_negative_samples()
    {
        // PresentMon's first row often has msBetweenPresents = 0; some malformed rows
        // could also produce negatives. Both should be filtered out.
        var samples = new List<double> { 0.0, -5.0, 16.67, 16.67, 16.67 };

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.SampleCount.Should().Be(3);   // only the three positive samples
        stats.AvgFps.Should().BeApproximately(60.0, 0.1);
    }

    [Fact]
    public void Compute_sets_MaxFrameTimeMs_to_the_largest_sample()
    {
        var samples = new List<double> { 16.67, 16.67, 200.0, 16.67 };

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.MaxFrameTimeMs.Should().BeApproximately(200.0, 0.001);
    }
}
