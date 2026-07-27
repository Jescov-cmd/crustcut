using Crustcut.Presentation;
using Crustcut.Presentation.Navigation;
using FluentAssertions;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class ShellViewModelTests
{
    [Fact]
    public void Starts_on_Overview()
    {
        new ShellViewModel().ActiveTab.Should().Be("Overview");
    }

    [Fact]
    public void Navigate_changes_the_active_tab()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("Cleanup");

        vm.ActiveTab.Should().Be("Cleanup");
        vm.IsActive("Cleanup").Should().BeTrue();
        vm.IsActive("Overview").Should().BeFalse();
    }

    [Fact]
    public void Navigate_ignores_an_unknown_tab()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("NotARealTab");

        vm.ActiveTab.Should().Be("Overview");
    }

    [Fact]
    public void Every_nav_id_is_unique()
    {
        var ids = NavCatalog.Primary.Concat(NavCatalog.Bottom).Select(i => i.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Gaming_is_a_section_rather_than_the_frame()
    {
        // Repositioning guard: gaming must not be the only or dominant grouping.
        var performance = NavCatalog.Primary.Count(i => i.Group == NavCatalog.Performance);
        var gaming      = NavCatalog.Primary.Count(i => i.Group == NavCatalog.Gaming);

        performance.Should().BeGreaterThanOrEqualTo(gaming);
    }

    [Fact]
    public void Retired_gaming_first_labels_are_gone()
    {
        var labels = NavCatalog.Primary.Concat(NavCatalog.Bottom).Select(i => i.Label).ToList();

        labels.Should().NotContain("Bloatware");
        labels.Should().NotContain("Library");
        labels.Should().NotContain("Sentinel");
        labels.Should().NotContain("Dashboard");
    }
}
