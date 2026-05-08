using FluentAssertions;
using PrimeOSTuner.Core.Monitoring;
using Xunit;

namespace PrimeOSTuner.Tests.Monitoring;

public class BoostScoreCalculatorTests
{
    [Fact]
    public void Score_for_a_pristine_pc_is_one_hundred()
    {
        var inputs = new BoostScoreInputs(
            JunkBytes: 0,
            HighPerformancePower: true,
            VisualEffectsOptimized: true,
            MouseAccelDisabled: true,
            TelemetryDisabled: true,
            BloatwareCount: 0);

        BoostScoreCalculator.Compute(inputs).Should().Be(100);
    }

    [Fact]
    public void Score_for_a_fresh_windows_is_low()
    {
        var inputs = new BoostScoreInputs(
            JunkBytes: 5L * 1024 * 1024 * 1024, // 5 GB
            HighPerformancePower: false,
            VisualEffectsOptimized: false,
            MouseAccelDisabled: false,
            TelemetryDisabled: false,
            BloatwareCount: 12);

        BoostScoreCalculator.Compute(inputs).Should().BeLessThan(40);
    }

    [Fact]
    public void Score_is_clamped_to_zero_one_hundred()
    {
        var ridiculous = new BoostScoreInputs(
            JunkBytes: long.MaxValue,
            HighPerformancePower: false,
            VisualEffectsOptimized: false,
            MouseAccelDisabled: false,
            TelemetryDisabled: false,
            BloatwareCount: 1000);

        var s = BoostScoreCalculator.Compute(ridiculous);
        s.Should().BeInRange(0, 100);
    }
}
