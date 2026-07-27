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
    private readonly OptimizeViewModel? _optimize;
    private bool _optimizeProbed;

    public PageFactory(OverviewViewModel? overview, OptimizeViewModel? optimize)
    {
        _overview = overview;
        _optimize = optimize;
    }

    public Control Create(string tabId) => tabId switch
    {
        "Overview" => new OverviewView { DataContext = _overview },
        "Optimize" => CreateOptimize(),
        _ => new PlaceholderView(LabelFor(tabId)),
    };

    private Control CreateOptimize()
    {
        var view = new OptimizeView { DataContext = _optimize };
        // Probing every tweak is slow, so do it once on first visit rather than at startup.
        if (_optimize is not null && !_optimizeProbed)
        {
            _optimizeProbed = true;
            _ = _optimize.InitializeAppliedStatesAsync();
        }
        return view;
    }

    private static string LabelFor(string tabId) =>
        NavCatalog.Primary.Concat(NavCatalog.Bottom)
            .FirstOrDefault(i => i.Id == tabId)?.Label ?? tabId;
}
