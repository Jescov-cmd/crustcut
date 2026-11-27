using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Crustcut.Presentation;

/// <summary>One line of Home's "needs a look" list. <paramref name="Action"/> is a page id to
/// open, or "optimize" to run the recommended settings in one go.</summary>
public sealed record HomeItemVm(string Title, string Detail, string Action);

/// <summary>
/// Home: one sentence about the PC, four numbers, one button, and a short list of things
/// worth doing — instead of four pages (Overview, Diagnosis, Cleanup, Memory) the user had
/// to visit and interpret. It composes the existing view models rather than re-sampling:
/// the Overview VM already streams CPU/RAM, Fans owns the CPU temperature, Memory knows
/// who is using it. The list is rebuilt from facts each refresh, so an item disappears by
/// itself once it's dealt with.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly OverviewViewModel _overview;
    private readonly FansViewModel _fans;
    private readonly MemoryPriorityViewModel? _memory;
    private readonly System.Timers.Timer _timer = new(3000) { AutoReset = true };
    private readonly IUiDispatcher _ui;
    private int _tick;

    [ObservableProperty] private string _headline = "Your PC is ready";
    [ObservableProperty] private string _subline = "Everything is set up.";
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string _tempLabel = "CPU temp";
    [ObservableProperty] private string _tempText = "—";
    [ObservableProperty] private string _lastGameLabel = "Last game";
    [ObservableProperty] private string _lastGameText = "—";
    [ObservableProperty] private string _freeStatus = "";
    [ObservableProperty] private bool _isFreeing;
    [ObservableProperty] private bool _hasItems;

    public ObservableCollection<HomeItemVm> Items { get; } = new();
    public bool CanFreeMemory => _memory is not null;
    public bool CanOptimize => _overview.HasTweakSurface;

    public HomeViewModel(OverviewViewModel overview, FansViewModel fans,
        MemoryPriorityViewModel? memory, IUiDispatcher ui)
    {
        _overview = overview;
        _fans = fans;
        _memory = memory;
        _ui = ui;
        _timer.Elapsed += (_, _) => _ui.Post(() => _ = RefreshAsync());
    }

    /// <summary>Home is (or stops being) on screen. Nothing samples while it isn't.</summary>
    public void SetViewing(bool viewing)
    {
        _overview.SetViewing(viewing);
        _fans.SetViewing(viewing);
        if (viewing)
        {
            _tick = 0;
            _ = RefreshAsync();
            _timer.Start();
        }
        else _timer.Stop();
    }

    public async Task RefreshAsync()
    {
        _fans.Refresh();
        CpuText = $"{_overview.CpuPercent:F0}%";
        RamText = $"{_overview.RamPercent:F0}%";
        if (_fans.IsSupported && _fans.TempText != "—")
        {
            TempLabel = "CPU temp";
            TempText = _fans.TempText.Replace(" °C", "°");
        }
        else
        {
            TempLabel = "Graphics";
            TempText = $"{_overview.GpuPercent:F0}%";
        }

        var last = _overview.RecentSessions.FirstOrDefault(s => !s.IsUnreliable);
        LastGameLabel = last is null ? "Last game" : last.GameName;
        LastGameText = last is null ? "—" : $"{last.AvgFpsValue} fps";

        // Who's using the memory is a process scan — every 5th tick (15 s), and only when
        // memory is actually tight enough to mention.
        if (_memory is not null && _overview.RamPercent >= 80 && _tick % 5 == 0)
        {
            try { await _memory.RefreshTopMemoryAsync(); } catch { }
        }
        _tick++;

        RebuildItems(last);
    }

    private void RebuildItems(FrameSessionVm? last)
    {
        var items = new List<HomeItemVm>();

        if (_overview.RamPercent >= 80)
        {
            var top = _memory?.TopMemory.FirstOrDefault(r =>
                !r.Name.Equals("Crustcut", StringComparison.OrdinalIgnoreCase) &&
                !r.Name.Equals("System", StringComparison.OrdinalIgnoreCase));
            items.Add(new HomeItemVm(
                $"Memory is at {_overview.RamPercent:F0}%",
                top is null
                    ? "Games stutter when memory runs out. See what's using it."
                    : $"{top.Name} is using {top.UsageMb}. Closing it frees the most.",
                "Memory"));
        }

        if (_fans.IsSupported && _fans.ConflictSuspected)
            items.Add(new HomeItemVm("Another program is fighting the fans",
                "Turn off fan control in your lighting or vendor software.", "Fans"));

        if (_overview.HasTweakSurface && _overview.RecommendedOff > 0)
            items.Add(new HomeItemVm(
                _overview.RecommendedOff == 1 ? "1 recommended setting is off" : $"{_overview.RecommendedOff} recommended settings are off",
                "Turn them on in one go. Every change can be undone in History.",
                "optimize"));

        if (last is { HasVerdict: true })
            items.Add(new HomeItemVm($"{last.GameName} stuttered last time", last.Verdict, "Sessions"));

        // Replace only when something changed, so the list doesn't flicker every 3 s.
        if (!items.SequenceEqual(Items))
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);
        }
        HasItems = Items.Count > 0;

        Headline = Items.Count switch
        {
            0 => "Your PC is ready",
            1 => "One thing needs a look",
            var n => $"{n} things need a look",
        };
        Subline = _overview.ActiveGameName is { Length: > 0 } game
            ? $"Playing {game}."
            : Items.Count == 0 ? "Everything is set up." : "Nothing urgent. Fix them when you like.";
    }

    public async Task FreeMemoryAsync()
    {
        if (_memory is null || IsFreeing) return;
        IsFreeing = true;
        FreeStatus = "Freeing memory…";
        try { FreeStatus = await _memory.DeepCleanAsync(); }
        finally { IsFreeing = false; }
        await RefreshAsync();
    }

    public async Task OptimizeAsync()
    {
        FreeStatus = "Turning on recommended settings…";
        await _overview.RunOneClickAsync();
        FreeStatus = _overview.OneClickStatus;
        await RefreshAsync();
    }
}
