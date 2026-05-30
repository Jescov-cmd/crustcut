using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Settings;
using Xunit;

namespace PrimeOSTuner.Tests.Settings;

public class AppSettingsOverlayTests
{
    [Fact]
    public void Overlay_settings_round_trip_through_the_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AppSettingsStore(path);
            var s = store.Load();
            s.OverlayEnabled = true;
            s.OverlayX = 1200;
            s.OverlayY = 40;
            s.OverlayScale = 1.3;
            s.OverlayShowVram = false;
            s.OverlayShowNet = true;
            s.OverlayOnlyInGame = false;
            store.Save(s);

            var reloaded = store.Load();
            reloaded.OverlayEnabled.Should().BeTrue();
            reloaded.OverlayX.Should().Be(1200);
            reloaded.OverlayY.Should().Be(40);
            reloaded.OverlayScale.Should().Be(1.3);
            reloaded.OverlayShowVram.Should().BeFalse();
            reloaded.OverlayShowNet.Should().BeTrue();
            reloaded.OverlayOnlyInGame.Should().BeFalse();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Overlay_defaults_are_sensible()
    {
        var s = new AppSettings();
        s.OverlayEnabled.Should().BeFalse();        // off by default
        s.OverlayOnlyInGame.Should().BeTrue();      // not intrusive on the desktop
        s.OverlayShowCpu.Should().BeTrue();
        s.OverlayScale.Should().Be(1.0);
    }
}
