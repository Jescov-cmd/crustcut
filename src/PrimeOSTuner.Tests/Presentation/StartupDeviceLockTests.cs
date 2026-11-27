using Crustcut.Presentation;
using FluentAssertions;
using PrimeOSTuner.Core.Platform;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class StartupDeviceLockTests
{
    [Theory]
    [InlineData("LGHUB", "\"C:\\Program Files\\LGHUB\\system_tray\\lghub_system_tray.exe\" --minimized")]
    [InlineData("SignalRgb", "\"C:\\Users\\x\\AppData\\Local\\VortxEngine\\SignalRgbLauncher.exe\" --silent")]
    [InlineData("Stream Deck", "\"C:\\Program Files\\Elgato\\StreamDeck\\StreamDeck.exe\" --runinbk")]
    [InlineData("RtkAudUService", "\"C:\\Windows\\System32\\DriverStore\\FileRepository\\x\\RtkAudUService64.exe\" -background")]
    public void Device_software_is_locked_on_at_startup(string name, string command)
    {
        var row = new CleanupViewModel.StartupRowVm(new StartupApp(name, command, StartupSource.CurrentUser, true));

        row.IsDevice.Should().BeTrue();
        row.CanToggle.Should().BeFalse("switching it off would leave the device dead after the next boot");
    }

    [Fact]
    public void Ordinary_startup_apps_can_be_switched_off()
    {
        var row = new CleanupViewModel.StartupRowVm(new StartupApp("OneDrive",
            "\"C:\\Program Files\\Microsoft OneDrive\\OneDrive.exe\" /background", StartupSource.CurrentUser, true));

        row.IsDevice.Should().BeFalse();
        row.CanToggle.Should().BeTrue();
    }

    [Fact]
    public void A_device_app_that_is_already_off_can_be_turned_back_on()
    {
        var row = new CleanupViewModel.StartupRowVm(new StartupApp("LGHUB",
            "C:\\Program Files\\LGHUB\\lghub.exe", StartupSource.CurrentUser, false));

        row.CanToggle.Should().BeTrue();
    }
}
