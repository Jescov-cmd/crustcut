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
    public void Overview_starts_flagged_active()
    {
        var vm = new ShellViewModel();

        vm.Primary.Single(i => i.Id == "Overview").IsActive.Should().BeTrue();
        vm.Primary.Where(i => i.Id != "Overview").Should().OnlyContain(i => !i.IsActive);
    }

    [Fact]
    public void Navigating_moves_the_active_flag_and_clears_the_previous_one()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("Games");

        vm.Primary.Single(i => i.Id == "Games").IsActive.Should().BeTrue();
        vm.Primary.Single(i => i.Id == "Overview").IsActive.Should().BeFalse();
    }

    [Fact]
    public void Navigating_to_a_bottom_item_clears_primary_selection()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("Settings");

        vm.Bottom.Single(i => i.Id == "Settings").IsActive.Should().BeTrue();
        vm.Primary.Should().OnlyContain(i => !i.IsActive);
    }

    [Fact]
    public void Exactly_one_item_is_ever_active()
    {
        var vm = new ShellViewModel();

        foreach (var id in new[] { "Optimize", "Sessions", "History", "Memory" })
        {
            vm.NavigateCommand.Execute(id);
            vm.Primary.Concat(vm.Bottom).Count(i => i.IsActive).Should().Be(1);
        }
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
