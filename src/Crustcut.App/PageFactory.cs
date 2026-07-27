using Avalonia.Controls;
using Crustcut.App.Views;
using Crustcut.Presentation.Navigation;

namespace Crustcut.App;

/// <summary>
/// Maps a nav id to the control shown in the shell's page host. Tabs that have not been
/// ported yet resolve to a labelled placeholder rather than a blank pane, so navigation is
/// honest about what exists.
/// </summary>
public sealed class PageFactory
{
    private readonly Composition? _app;
    private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);

    public PageFactory(Composition? app) => _app = app;

    public Control Create(string tabId) => tabId switch
    {
        "Overview" => new OverviewView { DataContext = _app?.Overview },

        // Probing every tweak is slow, so it runs on first visit rather than at startup.
        "Optimize" => Once(tabId, new OptimizeView { DataContext = _app?.Optimize },
                           () => _app?.Optimize.InitializeAppliedStatesAsync()),

        // Cleanup and Diagnosis deliberately do NOT auto-run: both are slow, and neither
        // should happen without the user asking for it.
        "Cleanup" => new CleanupView { DataContext = _app?.Cleanup },
        "Diagnosis" => new DiagnosisView { DataContext = _app?.Diagnosis },

        "Memory" => Once(tabId, new MemoryView { DataContext = _app?.Memory },
                         () => _app?.Memory.LoadAsync()),

        "History" => Once(tabId, new HistoryView { DataContext = _app?.History },
                          () => _app?.History.LoadAsync()),

        _ => new PlaceholderView(LabelFor(tabId)),
    };

    /// <summary>Runs <paramref name="load"/> the first time a tab is opened, never again.</summary>
    private Control Once(string tabId, Control view, Func<Task?> load)
    {
        if (_loaded.Add(tabId)) _ = load();
        return view;
    }

    private static string LabelFor(string tabId) =>
        NavCatalog.Primary.Concat(NavCatalog.Bottom)
            .FirstOrDefault(i => i.Id == tabId)?.Label ?? tabId;
}
