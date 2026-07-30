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
    private readonly Dictionary<string, Control> _cache = new(StringComparer.Ordinal);

    public PageFactory(Composition? app) => _app = app;

    /// <summary>
    /// Views are built ONCE and reused on every later visit. Rebuilding per navigation
    /// re-instantiated every control template on the UI thread — on Memory that meant
    /// dozens of ComboBox rows realized from scratch per click, which is exactly the
    /// "tab freezes when opened" the user kept hitting.
    /// </summary>
    public Control Create(string tabId)
    {
        if (_cache.TryGetValue(tabId, out var cached)) return cached;
        var view = Build(tabId);
        _cache[tabId] = view;
        return view;
    }

    private Control Build(string tabId) => tabId switch
    {
        "Overview" => new OverviewView { DataContext = _app?.Overview },

        // Probing every tweak is slow, so it runs on first visit rather than at startup.
        "Optimize" => Once(tabId, new OptimizeView { DataContext = _app?.Optimize },
                           () => _app?.Optimize?.InitializeAppliedStatesAsync()),

        // Cleanup and Diagnosis deliberately do NOT auto-run: both are slow, and neither
        // should happen without the user asking for it.
        "Cleanup" => new CleanupView { DataContext = _app?.Cleanup },
        "Diagnosis" => new DiagnosisView { DataContext = _app?.Diagnosis },

        "Memory" => Once(tabId, new MemoryView { DataContext = _app?.Memory },
                         () => _app?.Memory?.LoadAsync()),

        "History" => Once(tabId, new HistoryView { DataContext = _app?.History },
                          () => _app?.History?.LoadAsync()),

        "Games" => Once(tabId, new GamesView { DataContext = _app?.Games },
                        () => _app?.Games.LoadAsync()),

        "Sessions" => new SessionsView { DataContext = _app?.Sessions },

        "GameBoost" => new GameBoostView { DataContext = _app?.GameBoost },

        "Guides" => new GuidesView { DataContext = _app?.Guides },

        "Settings" => new SettingsView { DataContext = _app?.Settings },

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
