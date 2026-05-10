using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.ViewModels;

public partial class MemoryPriorityViewModel : ObservableObject
{
    private readonly PriorityRuleStore _store;
    private readonly PriorityRuleEngine _engine;
    private readonly GameRegistry _games;

    public ObservableCollection<PriorityRuleVm> Rules { get; } = new();

    [ObservableProperty] private string _activeFilter = "all"; // all | games | apps

    public MemoryPriorityViewModel(
        PriorityRuleStore store, PriorityRuleEngine engine, GameRegistry games)
    {
        _store = store;
        _engine = engine;
        _games = games;
    }

    public async Task LoadAsync()
    {
        var rules = await _store.LoadAsync();
        Rules.Clear();
        foreach (var r in rules) Rules.Add(new PriorityRuleVm(r));
        await SyncEngineAsync();
    }

    public async Task AddAsync(PriorityRule rule)
    {
        Rules.Add(new PriorityRuleVm(rule));
        await PersistAsync();
    }

    public async Task RemoveAsync(PriorityRuleVm vm)
    {
        Rules.Remove(vm);
        await PersistAsync();
    }

    public async Task UpdateRuleAsync(PriorityRuleVm vm)
    {
        await PersistAsync();
    }

    public async Task<int> ApplyRecommendedToAllGamesAsync()
    {
        var games = (await _games.GetAllAsync())
            .Where(g => !string.IsNullOrEmpty(g.InstallPath))
            .ToList();
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var game in games)
        {
            // Each game has potentially multiple EXEs; use the launch executable
            // (best-effort: the first .exe under InstallPath whose name matches the game name).
            var exePath = ResolveLaunchExe(game.InstallPath!);
            if (exePath is null) continue;
            if (existingPaths.Contains(exePath)) continue;

            var rule = new PriorityRule(
                ExePath: exePath,
                DisplayName: game.DisplayName,
                Priority: PriorityLevel.High,
                ProtectFromRamCleanup: true,
                GameBooster: true,
                IsGame: true);
            Rules.Add(new PriorityRuleVm(rule));
            added++;
        }

        if (added > 0) await PersistAsync();
        return added;
    }

    private static string? ResolveLaunchExe(string installPath)
    {
        try
        {
            // installPath may be a direct EXE path (e.g. from Steam scanner) or a folder.
            if (File.Exists(installPath) &&
                string.Equals(Path.GetExtension(installPath), ".exe", StringComparison.OrdinalIgnoreCase))
                return installPath;

            // Pick the largest .exe in the root install folder. Most games' launcher
            // is the largest binary by far.
            var dir = new DirectoryInfo(installPath);
            if (!dir.Exists) return null;
            return dir.EnumerateFiles("*.exe")
                .OrderByDescending(f => f.Length)
                .FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAsync()
    {
        var rules = Rules.Select(vm => vm.ToRule()).ToList();
        await _store.SaveAsync(rules);
        await SyncEngineAsync();
    }

    private async Task SyncEngineAsync()
    {
        await _engine.ReloadAsync(Rules.Select(vm => vm.ToRule()));
    }
}
