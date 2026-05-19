using FluentAssertions;
using PrimeOSTuner.Core.Education;
using Xunit;

namespace PrimeOSTuner.Tests.Education;

public class SystemStateProbeTests
{
    [Theory]
    [InlineData(6000u, 6000u, 34u, DetectedState.Enabled)]   // DDR5 above JEDEC ceiling
    [InlineData(4800u, 4800u, 34u, DetectedState.Disabled)]  // DDR5 at JEDEC base
    [InlineData(3600u, 3600u, 26u, DetectedState.Enabled)]   // DDR4 above JEDEC ceiling
    [InlineData(2400u, 2400u, 26u, DetectedState.Disabled)]  // DDR4 at JEDEC base
    public void ClassifyMemoryProfile_compares_running_speed_to_the_jedec_ceiling(
        uint configured, uint rated, uint ddrType, DetectedState expected)
    {
        SystemStateProbe.ClassifyMemoryProfile(configured, rated, ddrType).Should().Be(expected);
    }

    [Fact]
    public void ClassifyMemoryProfile_is_disabled_when_running_below_the_rated_speed()
    {
        // Rated 6000 but running 4800 — the profile is not applied.
        SystemStateProbe.ClassifyMemoryProfile(4800, 6000, 34).Should().Be(DetectedState.Disabled);
    }

    [Fact]
    public void ClassifyMemoryProfile_is_unknown_when_the_running_speed_is_missing()
    {
        SystemStateProbe.ClassifyMemoryProfile(0, 0, 34).Should().Be(DetectedState.Unknown);
    }

    [Fact]
    public void ClassifyMemoryProfile_is_unknown_for_an_unrecognized_memory_type()
    {
        SystemStateProbe.ClassifyMemoryProfile(3200, 3200, 0).Should().Be(DetectedState.Unknown);
    }
}
