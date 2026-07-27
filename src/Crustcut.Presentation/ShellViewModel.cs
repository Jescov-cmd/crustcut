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
        Primary = WithGroupHeaders(NavCatalog.Primary);
        Bottom = WithGroupHeaders(NavCatalog.Bottom);
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

    /// <summary>Flags the first row of each group so its heading is drawn only once.</summary>
    private static IReadOnlyList<NavItemVm> WithGroupHeaders(IReadOnlyList<NavItem> items)
    {
        var result = new List<NavItemVm>(items.Count);
        string? previousGroup = null;

        foreach (var item in items)
        {
            var isFirstOfGroup = !string.IsNullOrEmpty(item.Group) && item.Group != previousGroup;
            result.Add(new NavItemVm(item) { ShowGroupHeader = isFirstOfGroup });
            previousGroup = item.Group;
        }

        return result;
    }

    private void SyncActiveFlags()
    {
        foreach (var i in Primary) i.IsActive = i.Id == ActiveTab;
        foreach (var i in Bottom) i.IsActive = i.Id == ActiveTab;
    }
}
