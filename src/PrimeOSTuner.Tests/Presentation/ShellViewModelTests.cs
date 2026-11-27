using Crustcut.Presentation;
using Crustcut.Presentation.Navigation;
using FluentAssertions;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class ShellViewModelTests
{
    [Fact]
    public void Starts_on_Home()
    {
        new ShellViewModel().ActiveTab.Should().Be("Home");
    }

    [Fact]
    public void The_rail_has_four_pages()
    {
        // Simplicity guard: the thirteen-page rail is what made people hunt.
        NavCatalog.Primary.Select(i => i.Id).Should().BeSubsetOf(new[] { "Home", "Boost", "Games", "Settings" });
        NavCatalog.Primary.Count.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void Navigate_changes_the_active_tab()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("Games");

        vm.ActiveTab.Should().Be("Games");
        vm.IsActive("Games").Should().BeTrue();
        vm.IsActive("Home").Should().BeFalse();
    }

    [Fact]
    public void Navigate_ignores_an_unknown_tab()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("NotARealTab");

        vm.ActiveTab.Should().Be("Home");
    }

    [Fact]
    public void A_sub_page_keeps_its_parent_highlighted_and_offers_a_way_back()
    {
        var vm = new ShellViewModel();

        vm.NavigateCommand.Execute("WhatsNew");   // exists on every platform

        vm.ActiveTab.Should().Be("WhatsNew");
        vm.IsSubPage.Should().BeTrue();
        vm.BackLabel.Should().Be("Back to Settings");
        vm.Primary.Single(i => i.Id == "Settings").IsActive.Should().BeTrue();

        vm.BackCommand.Execute(null);

        vm.ActiveTab.Should().Be("Settings");
        vm.IsSubPage.Should().BeFalse();
    }

    [Fact]
    public void Exactly_one_rail_item_is_ever_active()
    {
        var vm = new ShellViewModel();

        foreach (var id in NavCatalog.SubPages.Select(p => p.Id).Concat(NavCatalog.Primary.Select(i => i.Id)))
        {
            vm.NavigateCommand.Execute(id);
            vm.Primary.Count(i => i.IsActive).Should().Be(1, $"after opening {id}");
        }
    }

    [Fact]
    public void Every_page_id_is_unique_and_every_parent_is_in_the_rail()
    {
        var ids = NavCatalog.Primary.Select(i => i.Id).Concat(NavCatalog.SubPages.Select(p => p.Id)).ToList();
        ids.Should().OnlyHaveUniqueItems();

        var rail = NavCatalog.Primary.Select(i => i.Id).ToHashSet();
        if (OperatingSystem.IsWindows())
            NavCatalog.SubPages.Should().OnlyContain(p => rail.Contains(p.Parent));
    }

    [Fact]
    public void Retired_gaming_first_labels_are_gone()
    {
        var labels = NavCatalog.Primary.Select(i => i.Label)
            .Concat(NavCatalog.SubPages.Select(p => p.Label)).ToList();

        labels.Should().NotContain("Bloatware");
        labels.Should().NotContain("Library");
        labels.Should().NotContain("Sentinel");
        labels.Should().NotContain("Dashboard");
    }
}
