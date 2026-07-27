using Crustcut.Presentation;
using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Bloatware;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class CleanupViewModelTests
{
    private static BloatwareItem Item(string name, SafetyTier tier = SafetyTier.Safe, string? risk = null)
        => new(new BloatwareCatalogEntry(name, name, "gaming", tier, risk),
               BloatwareStatus.Installed, name + "_1.0_x64__abc", null);

    private static (CleanupViewModel Vm, Mock<IAppxClient> Appx, RecordingDialogService Dialogs) Build(
        params BloatwareItem[] detected)
    {
        var appx = new Mock<IAppxClient>();
        var detector = new BloatwareDetector(appx.Object, Array.Empty<BloatwareCatalogEntry>());

        var dialogs = new RecordingDialogService();
        var vm = new CleanupViewModel(
            detector,
            Mock.Of<IInstalledProgramsClient>(),
            Array.Empty<DesktopBloatwareCatalogEntry>(),
            new BloatwareUninstallService(appx.Object),
            new BloatwareDisableService(Mock.Of<PrimeOSTuner.Core.Tweaks.IServiceClient>()),
            dialogs);

        foreach (var i in detected) vm.Items.Add(new BloatwareItemRowVm(i));
        return (vm, appx, dialogs);
    }

    [Fact]
    public async Task Uninstall_asks_for_confirmation_before_removing_anything()
    {
        var (vm, appx, dialogs) = Build(Item("Xbox"));
        dialogs.ConfirmResult = false;   // user declines

        await vm.UninstallAsync(vm.Items[0]);

        dialogs.Confirms.Should().ContainSingle();
        appx.Verify(a => a.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        vm.Items.Should().ContainSingle("the app must still be listed when the user declines");
    }

    [Fact]
    public async Task Uninstall_proceeds_only_after_an_explicit_yes()
    {
        var (vm, appx, dialogs) = Build(Item("Xbox"));
        dialogs.ConfirmResult = true;

        await vm.UninstallAsync(vm.Items[0]);

        appx.Verify(a => a.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Blocked_apps_are_refused_without_even_asking()
    {
        var (vm, appx, dialogs) = Build(Item("Cortana", SafetyTier.Blocked));

        await vm.UninstallAsync(vm.Items[0]);

        dialogs.Confirms.Should().BeEmpty();
        dialogs.Shown.Should().ContainSingle();
        appx.Verify(a => a.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Risk_note_is_surfaced_in_the_confirmation()
    {
        var (vm, _, dialogs) = Build(Item("Store", SafetyTier.Risky, "Other apps may stop updating."));
        dialogs.ConfirmResult = false;

        await vm.UninstallAsync(vm.Items[0]);

        dialogs.Confirms[0].Message.Should().Contain("Other apps may stop updating.");
    }

    [Fact]
    public async Task Disable_needs_no_confirmation_because_it_is_reversible()
    {
        var (vm, _, dialogs) = Build(Item("Xbox"));

        await vm.DisableAsync(vm.Items[0]);

        dialogs.Confirms.Should().BeEmpty();
        vm.Items.Should().BeEmpty();
    }
}
