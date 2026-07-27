using Crustcut.Presentation;
using FluentAssertions;
using PrimeOSTuner.Core.Settings;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "crustcut-settings-" + Guid.NewGuid().ToString("N") + ".json");

    private AppSettingsStore Store() => new(_path);

    [Fact]
    public void Loads_existing_values()
    {
        Store().Save(new AppSettings { RamAutoIntervalMinutes = 7, RamAutoOptimizeOnInterval = true });

        var vm = new SettingsViewModel(Store());

        vm.RamAutoIntervalMinutes.Should().Be(7);
        vm.RamAutoOptimizeOnInterval.Should().BeTrue();
    }

    [Fact]
    public void Changing_a_value_persists_it()
    {
        var vm = new SettingsViewModel(Store()) { RamAutoIntervalMinutes = 15 };

        Store().Load().RamAutoIntervalMinutes.Should().Be(15);
    }

    [Fact]
    public void Saving_preserves_fields_this_page_does_not_show()
    {
        // The overlay persists its drag position into the same file. Replacing the whole
        // settings object instead of load-mutate-save would silently wipe it.
        Store().Save(new AppSettings { OverlayX = 421, OverlayY = 99, OverlayEnabled = true });

        var vm = new SettingsViewModel(Store()) { StartMinimized = true };

        var saved = Store().Load();
        saved.OverlayX.Should().Be(421);
        saved.OverlayY.Should().Be(99);
        saved.OverlayEnabled.Should().BeTrue();
        saved.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public void Loading_does_not_write_back()
    {
        // Constructing the page must not count as a change.
        var vm = new SettingsViewModel(Store());

        vm.Status.Should().BeEmpty();
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(10, false)]
    public void Aggressive_interval_is_flagged(int minutes, bool expected)
    {
        var vm = new SettingsViewModel(Store())
        {
            RamAutoOptimizeOnInterval = true,
            RamAutoIntervalMinutes = minutes,
        };

        vm.IntervalIsAggressive.Should().Be(expected);
    }

    [Fact]
    public void Interval_is_not_flagged_when_the_schedule_is_off()
    {
        var vm = new SettingsViewModel(Store())
        {
            RamAutoIntervalMinutes = 2,
            RamAutoOptimizeOnInterval = false,
        };

        vm.IntervalIsAggressive.Should().BeFalse();
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
