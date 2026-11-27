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
    private readonly HashSet<string> _loaded;
    private readonly Dictionary<string, Control> _cache = new(StringComparer.Ordinal);

    // The window (and so this factory) is rebuilt on every tray open, but a tab's first-visit
    // load — probing every tweak, scanning the game library — is still once per app run:
    // the loaded set lives on the long-lived Composition.
    public PageFactory(Composition? app)
    {
        _app = app;
        _loaded = app?.LoadedTabs ?? new HashSet<string>(StringComparer.Ordinal);
    }

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
        "Home" => new HomeView { DataContext = _app?.Home },

        // Boost reads the same probe as All settings; whichever opens first runs it once.
        "Boost" => BoostPage(),

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

        "Fans" => new FansView { DataContext = _app?.Fans },

        "History" => new HistoryView { DataContext = _app?.History },   // reloads itself on each visit

        "Games" => Once(tabId, new GamesView { DataContext = _app?.Games },
                        () => _app?.Games.LoadAsync()),

        "Sessions" => new SessionsView { DataContext = _app?.Sessions },

        "GameBoost" => new GameBoostView { DataContext = _app?.GameBoost },

        "Guides" => new GuidesView { DataContext = _app?.Guides },

        "WhatsNew" => new WhatsNewView(),

        "Settings" => new SettingsView { DataContext = _app?.Settings },

        _ => new PlaceholderView(LabelFor(tabId)),
    };

    private Control BoostPage()
    {
        var view = new BoostView { DataContext = _app?.Boost };
        if (_app?.Optimize is { } optimize && _app.Boost is { } boost)
        {
            if (_loaded.Add("Optimize"))
                _ = optimize.InitializeAppliedStatesAsync().ContinueWith(
                    _ => Avalonia.Threading.Dispatcher.UIThread.Post(boost.Sync));
            else boost.Sync();
        }
        return view;
    }

    /// <summary>Runs <paramref name="load"/> the first time a tab is opened, never again.</summary>
    private Control Once(string tabId, Control view, Func<Task?> load)
    {
        if (_loaded.Add(tabId)) _ = load();
        return view;
    }

    private static string LabelFor(string tabId) => NavCatalog.LabelFor(tabId);
}
