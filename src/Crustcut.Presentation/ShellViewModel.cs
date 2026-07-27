using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crustcut.Presentation.Navigation;

namespace Crustcut.Presentation;

public partial class ShellViewModel : ObservableObject
{
    public IReadOnlyList<NavItemVm> Primary { get; }
    public IReadOnlyList<NavItemVm> Bottom { get; }

    [ObservableProperty] private string _activeTab = "Overview";

    public ShellViewModel()
    {
        Primary = NavCatalog.Primary.Select(i => new NavItemVm(i)).ToList();
        Bottom = NavCatalog.Bottom.Select(i => new NavItemVm(i)).ToList();
        SyncActiveFlags();
    }

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

    partial void OnActiveTabChanged(string value) => SyncActiveFlags();

    private void SyncActiveFlags()
    {
        foreach (var i in Primary) i.IsActive = i.Id == ActiveTab;
        foreach (var i in Bottom) i.IsActive = i.Id == ActiveTab;
    }
}
