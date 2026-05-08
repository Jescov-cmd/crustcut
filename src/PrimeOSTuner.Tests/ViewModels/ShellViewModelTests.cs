using FluentAssertions;
using PrimeOSTuner.UI.ViewModels;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class ShellViewModelTests
{
    [Fact]
    public void SelectedTabIndex_starts_at_zero_for_default_dashboard_tab()
    {
        var vm = new ShellViewModel();
        vm.SelectedTabIndex.Should().Be(0);
    }

    [Theory]
    [InlineData("Dashboard", 0)]
    [InlineData("Optimize", 1)]
    [InlineData("GameBoost", 2)]
    [InlineData("GameLibrary", 3)]
    [InlineData("CustomMode", 4)]
    [InlineData("History", 5)]
    public void SelectedTabIndex_tracks_ActiveTab(string tab, int expectedIndex)
    {
        var vm = new ShellViewModel();
        vm.NavigateCommand.Execute(tab);
        vm.SelectedTabIndex.Should().Be(expectedIndex);
    }

    [Fact]
    public void SelectedTabIndex_returns_zero_for_unknown_tab()
    {
        var vm = new ShellViewModel();
        vm.NavigateCommand.Execute("Unknown");
        vm.SelectedTabIndex.Should().Be(0);
    }
}
