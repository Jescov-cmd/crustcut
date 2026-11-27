using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crustcut.Presentation.Navigation;

namespace Crustcut.Presentation;

public partial class ShellViewModel : ObservableObject
{
    public IReadOnlyList<NavItemVm> Primary { get; }
    public IReadOnlyList<NavItemVm> Bottom { get; }

    [ObservableProperty] private string _activeTab = "Home";

    /// <summary>True when the open page is reached from inside a rail page (shows the back bar).</summary>
    public bool IsSubPage => NavCatalog.SubPageFor(ActiveTab) is not null;

    /// <summary>"Back to Boost" — where the back bar goes.</summary>
    public string BackLabel => NavCatalog.SubPageFor(ActiveTab) is { } p
        ? $"Back to {NavCatalog.LabelFor(p.Parent)}" : "";

    [RelayCommand]
    private void Back()
    {
        if (NavCatalog.SubPageFor(ActiveTab) is { } p) ActiveTab = p.Parent;
    }

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
        if (!NavCatalog.IsKnown(tab)) return;
        ActiveTab = tab;
    }

    partial void OnActiveTabChanged(string value)
    {
        SyncActiveFlags();
        OnPropertyChanged(nameof(IsSubPage));
        OnPropertyChanged(nameof(BackLabel));
    }

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
        // A sub-page keeps its parent highlighted in the rail: you're still "in" Boost.
        var railId = NavCatalog.SubPageFor(ActiveTab)?.Parent ?? ActiveTab;
        foreach (var i in Primary) i.IsActive = i.Id == railId;
        foreach (var i in Bottom) i.IsActive = i.Id == railId;
    }
}
