using FluentAssertions;
using PrimeOSTuner.Core.Fans;
using Xunit;

namespace PrimeOSTuner.Tests.Fans;

public class FanTuningStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"fan-tuning-{Guid.NewGuid():N}.json");

    public void Dispose() { try { File.Delete(_path); } catch { } }

    [Fact]
    public void Uncalibrated_fans_get_the_conservative_default_not_one_machines_number()
    {
        var store = new FanTuningStore(_path);

        store.HasCalibration.Should().BeFalse();
        store.MinDutyFor("Some Unknown Fan").Should().Be(FanPolicy.UncalibratedMinDutyPercent);
        store.RpmScaleFor("Some Unknown Fan").Should().Be(1.0);
    }

    [Fact]
    public void Calibration_round_trips_across_instances()
    {
        new FanTuningStore(_path).Set(new FanTuning("CPU Fan", 34, 2.0));

        var reloaded = new FanTuningStore(_path);
        reloaded.HasCalibration.Should().BeTrue();
        reloaded.MinDutyFor("CPU Fan").Should().Be(34);
        reloaded.RpmScaleFor("CPU Fan").Should().Be(2.0);
        // Fan names come from hardware and casing varies between reads.
        reloaded.MinDutyFor("cpu fan").Should().Be(34);
    }

    [Fact]
    public void Never_returns_a_floor_below_the_hard_minimum()
    {
        var store = new FanTuningStore(_path);
        store.Set(new FanTuning("Reckless Fan", 2));   // absurd calibration result

        store.MinDutyFor("Reckless Fan").Should().Be(FanPolicy.HardMinDutyPercent);
    }

    [Fact]
    public void Rpm_scale_can_be_set_before_any_calibration_exists()
    {
        var store = new FanTuningStore(_path);
        store.SetRpmScale("System Fan #1", 0.5);

        store.RpmScaleFor("System Fan #1").Should().Be(0.5);
        // ...and doesn't invent a reckless duty floor as a side effect.
        store.MinDutyFor("System Fan #1").Should().Be(FanPolicy.UncalibratedMinDutyPercent);
    }

    [Fact]
    public void Corrupt_file_degrades_to_uncalibrated_instead_of_throwing()
    {
        File.WriteAllText(_path, "{ this is not json");

        var store = new FanTuningStore(_path);
        store.HasCalibration.Should().BeFalse();
        store.MinDutyFor("CPU Fan").Should().Be(FanPolicy.UncalibratedMinDutyPercent);
    }
}
