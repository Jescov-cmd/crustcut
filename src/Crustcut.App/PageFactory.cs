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
    private readonly CleanupViewModel? _cleanup;
    private readonly MemoryPriorityViewModel? _memory;
    private bool _optimizeProbed;
    private bool _memoryLoaded;

    public PageFactory(
        OverviewViewModel? overview,
        OptimizeViewModel? optimize,
        CleanupViewModel? cleanup,
        MemoryPriorityViewModel? memory)
    {
        _overview = overview;
        _optimize = optimize;
        _cleanup = cleanup;
        _memory = memory;
    }

    public Control Create(string tabId) => tabId switch
    {
        "Overview" => new OverviewView { DataContext = _overview },
        "Optimize" => CreateOptimize(),
        // Cleanup deliberately does NOT auto-scan: enumerating packages is slow, and nothing
        // here should happen without the user asking for it.
        "Cleanup" => new CleanupView { DataContext = _cleanup },
        "Memory" => CreateMemory(),
        _ => new PlaceholderView(LabelFor(tabId)),
    };

    private Control CreateMemory()
    {
        var view = new MemoryView { DataContext = _memory };
        if (_memory is not null && !_memoryLoaded)
        {
            _memoryLoaded = true;
            _ = _memory.LoadAsync();
        }
        return view;
    }

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
