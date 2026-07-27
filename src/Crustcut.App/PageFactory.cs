using Avalonia.Controls;
using Crustcut.App.Views;
using Crustcut.Presentation;
using Crustcut.Presentation.Navigation;

namespace Crustcut.App;

/// <summary>
/// Maps a nav id to the control shown in the shell's page host. Tabs that have not been
/// ported yet resolve to a labelled placeholder rather than a blank pane, so navigation is
/// honest about what exists.
/// </summary>
public sealed class PageFactory
{
    private readonly OverviewViewModel? _overview;

    public PageFactory(OverviewViewModel? overview) => _overview = overview;

    public Control Create(string tabId) => tabId switch
    {
        "Overview" => new OverviewView { DataContext = _overview },
        _ => new PlaceholderView(LabelFor(tabId)),
    };

    private static string LabelFor(string tabId) =>
        NavCatalog.Primary.Concat(NavCatalog.Bottom)
            .FirstOrDefault(i => i.Id == tabId)?.Label ?? tabId;
}
