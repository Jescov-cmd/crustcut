using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crustcut.Presentation.Navigation;

namespace Crustcut.Presentation;

public partial class ShellViewModel : ObservableObject
{
    public IReadOnlyList<NavItem> Primary => NavCatalog.Primary;
    public IReadOnlyList<NavItem> Bottom  => NavCatalog.Bottom;

    [ObservableProperty] private string _activeTab = "Overview";

    public bool IsActive(string id) => string.Equals(ActiveTab, id, StringComparison.Ordinal);

    [RelayCommand]
    private void Navigate(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab)) return;
        // Ignore ids that aren't in the catalog, so a typo in markup can't blank the page.
        var known = NavCatalog.Primary.Concat(NavCatalog.Bottom).Any(i => i.Id == tab);
        if (!known) return;
        ActiveTab = tab;
    }
}
