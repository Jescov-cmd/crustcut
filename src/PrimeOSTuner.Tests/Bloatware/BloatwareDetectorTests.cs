using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Bloatware;
using Xunit;

namespace PrimeOSTuner.Tests.Bloatware;

public class BloatwareDetectorTests
{
    private static IReadOnlyList<BloatwareCatalogEntry> Catalog() => new[]
    {
        new BloatwareCatalogEntry("Microsoft.SkypeApp", "Skype", "preinstalled", SafetyTier.Safe, null),
        new BloatwareCatalogEntry("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "gaming", SafetyTier.Risky, "warning"),
        new BloatwareCatalogEntry("Microsoft.WindowsStore", "Microsoft Store", "system", SafetyTier.Blocked, "required"),
    };

    [Fact]
    public async Task DetectAsync_returns_only_items_present_in_both_catalog_and_installed_list()
    {
        var appx = new Mock<IAppxClient>();
        appx.Setup(a => a.ListInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstalledAppx>
            {
                new("Microsoft.SkypeApp", "Microsoft.SkypeApp_15.0_x64__kzf8qxf38zg5c", "C:\\foo"),
                new("Microsoft.UnrelatedThing", "Microsoft.UnrelatedThing_1.0", "C:\\bar"),
            });

        var detector = new BloatwareDetector(appx.Object, Catalog());
        var found = await detector.DetectAsync();

        found.Should().HaveCount(1);
        found[0].Entry.AppxName.Should().Be("Microsoft.SkypeApp");
        found[0].Status.Should().Be(BloatwareStatus.Installed);
        found[0].PackageFullName.Should().StartWith("Microsoft.SkypeApp_");
    }

    [Fact]
    public async Task DetectAsync_sorts_results_by_tier_then_name()
    {
        var appx = new Mock<IAppxClient>();
        appx.Setup(a => a.ListInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstalledAppx>
            {
                new("Microsoft.WindowsStore", "MS.Store_1", "x"),
                new("Microsoft.XboxGamingOverlay", "Xbox_1", "x"),
                new("Microsoft.SkypeApp", "Skype_1", "x"),
            });

        var detector = new BloatwareDetector(appx.Object, Catalog());
        var found = await detector.DetectAsync();

        found.Select(i => i.Entry.Tier).Should().ContainInOrder(SafetyTier.Safe, SafetyTier.Risky, SafetyTier.Blocked);
    }

    [Fact]
    public async Task DetectAsync_returns_empty_when_no_catalog_entries_match_installed()
    {
        var appx = new Mock<IAppxClient>();
        appx.Setup(a => a.ListInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstalledAppx>
            {
                new("SomeOther.Thing", "SomeOther.Thing_1.0", null),
            });

        var detector = new BloatwareDetector(appx.Object, Catalog());
        (await detector.DetectAsync()).Should().BeEmpty();
    }
}
