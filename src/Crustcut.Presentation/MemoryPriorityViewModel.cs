using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;

namespace Crustcut.Presentation;

public partial class MemoryPriorityViewModel : ObservableObject
{
    private readonly PriorityRuleStore _store;
    private readonly PriorityRuleEngine _engine;
    private readonly GameRegistry _games;
    private readonly IPriorityClient? _priority;

    public ObservableCollection<PriorityRuleVm> Rules { get; } = new();

    /// <summary>Levels offered in the priority dropdown. Realtime is intentionally absent.</summary>
    public static IReadOnlyList<PriorityLevel> PriorityLevels { get; } = Enum.GetValues<PriorityLevel>();

    [ObservableProperty] private string _activeFilter = "all"; // all | games | apps

    [ObservableProperty] private bool _multiSelectMode;

    public MemoryPriorityViewModel(
        PriorityRuleStore store, PriorityRuleEngine engine, GameRegistry games,
        IPriorityClient? priority = null)
    {
        _store = store;
        _engine = engine;
        _games = games;
        _priority = priority;
    }

    public async Task LoadAsync()
    {
        var rules = await _store.LoadAsync();
        Rules.Clear();
        foreach (var r in rules) Rules.Add(new PriorityRuleVm(r));

        // First launch (or after the user wiped the rules file): auto-populate with
        // detected games + currently-running user apps so the tab isn't empty.
        if (Rules.Count == 0)
        {
            await AutoPopulateAsync();
        }

        await SyncEngineAsync();
    }

    /// <summary>
    /// Seeds the rule list with detected games (from GameLibrary) + currently-running
    /// user-installed apps. All rows start with default settings â€” user customizes
    /// from there. Persists the seeded list so it's stable across launches.
    /// </summary>
    public async Task AutoPopulateAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);

        // Candidate gathering walks game install dirs and Program Files — all of it stays
        // off the UI thread; only the row adds below run on it.
        var candidates = await Task.Run(async () =>
        {
            var list = new List<PriorityRule>();

            // 1. Games from the library.
            foreach (var game in await _games.GetAllAsync())
            {
                if (string.IsNullOrEmpty(game.InstallPath)) continue;
                var exePath = ResolveLaunchExe(game.InstallPath);
                if (exePath is null) continue;
                list.Add(new PriorityRule(exePath, game.DisplayName,
                    PriorityLevel.Normal, false, false, IsGame: true));
            }

            // 2 + 3. Running, then installed-but-not-running user apps.
            foreach (var (path, name) in EnumerateRunningUserApps().Concat(EnumerateInstalledUserApps()))
                list.Add(new PriorityRule(path, name,
                    PriorityLevel.Normal, false, false, IsGame: false));

            return list;
        });

        var added = 0;
        foreach (var rule in candidates)
        {
            if (!existingPaths.Add(rule.ExePath)) continue;
            Rules.Add(new PriorityRuleVm(rule));
            added++;
        }

        if (added > 0) await PersistAsync();
    }

    /// <summary>
    /// Called every time the Memory Priority tab is opened: reloads from disk, DROPS rules
    /// whose target .exe no longer exists (uninstalled/deleted software), then re-scans for
    /// newly installed/running apps. Keeps the list honest instead of showing stale entries.
    /// </summary>
    public async Task RefreshAsync()
    {
        var stored = await _store.LoadAsync();
        Rules.Clear();
        foreach (var r in stored)
            if (TargetExists(r.ExePath)) Rules.Add(new PriorityRuleVm(r));

        if (Rules.Count == 0)
        {
            await AutoPopulateAsync();      // persists internally
        }
        else
        {
            await RescanRunningAppsAsync();  // add new apps (persists if any added)
            await PersistAsync();            // persist the pruning + resync the engine
        }
    }

    // A rule's target is considered gone if it has a path that no longer points to a file.
    private static bool TargetExists(string? exePath) =>
        !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);

    /// <summary>
    /// Re-scan everything (running + installed) and add any new user-installed apps
    /// not already in the rules list. Called from the "Re-scan apps" button.
    /// </summary>
    public async Task<int> RescanRunningAppsAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);

        // The Program Files walk touches hundreds of directories — off the UI thread, or
        // the tab freezes while it runs.
        var found = await Task.Run(() =>
            EnumerateRunningUserApps().Concat(EnumerateInstalledUserApps()).ToList());

        var added = 0;
        foreach (var (path, name) in found)
        {
            if (!existingPaths.Add(path)) continue;
            Rules.Add(new PriorityRuleVm(new PriorityRule(
                ExePath: path,
                DisplayName: name,
                Priority: PriorityLevel.Normal,
                ProtectFromRamCleanup: false,
                GameBooster: false,
                IsGame: false)));
            added++;
        }
        if (added > 0) await PersistAsync();
        return added;
    }

    /// <summary>
    /// Walks the top two levels of Program Files / Program Files (x86) / LocalAppData\Programs
    /// and picks the largest .exe in each app's folder. Heuristic but catches the vast
    /// majority of user-installed apps without needing COM or registry access.
    /// </summary>
    private static IEnumerable<(string Path, string Name)> EnumerateInstalledUserApps()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));

        // Folder names we never want to surface as "apps."
        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Common Files", "Internet Explorer", "Windows Defender",
            "Windows Defender Advanced Threat Protection", "Windows Mail",
            "Windows Media Player", "Windows NT", "Windows Photo Viewer",
            "Windows Portable Devices", "Windows Sidebar", "WindowsApps",
            "WindowsPowerShell", "ModifiableWindowsApps", "Microsoft",
            "Reference Assemblies", "MSBuild", "dotnet"
        };

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cap = 75;  // soft cap so the list doesn't bloat to hundreds

        foreach (var root in roots)
        {
            DirectoryInfo[] children;
            try { children = new DirectoryInfo(root).GetDirectories(); }
            catch { continue; }

            foreach (var dir in children)
            {
                if (seen.Count >= cap) break;
                if (excludeFolders.Contains(dir.Name)) continue;
                try
                {
                    // Pick the largest top-level .exe in this folder. Most apps have a
                    // clear "main exe" at the install root; for the rest this is best-effort.
                    var exe = dir.EnumerateFiles("*.exe", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault();
                    if (exe is null) continue;
                    if (exe.Length < 100_000) continue;  // skip tiny utility .exes
                    seen.TryAdd(exe.FullName, dir.Name);
                }
                catch { /* access denied â€” skip */ }
            }
        }
        return seen.Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Enumerates running processes that look like real user-installed apps (has a
    /// MainWindowHandle, lives under a normal install path, not a system process).
    /// </summary>
    private static IEnumerable<(string Path, string Name)> EnumerateRunningUserApps()
    {
        var systemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "dwm", "csrss", "wininit", "services", "lsass", "winlogon",
            "smss", "audiodg", "fontdrvhost", "RuntimeBroker", "sihost", "taskhostw",
            "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
            "ApplicationFrameHost", "TextInputHost", "ctfmon", "conhost",
            "svchost", "spoolsv", "WmiPrvSE", "MsMpEng", "SecurityHealthService",
            "MoUsoCoreWorker", "PrimeOSTuner.UI"
        };
        var userInstallPrefixes = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        }.Where(p => !string.IsNullOrEmpty(p)).ToArray();

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (systemNames.Contains(p.ProcessName)) continue;
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                var path = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)) continue;
                if (!userInstallPrefixes.Any(prefix =>
                        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;
                seen.TryAdd(path, p.ProcessName);
            }
            catch { /* access denied on system processes â€” skip */ }
            finally { p.Dispose(); }
        }
        return seen.Select(kv => (kv.Key, kv.Value));
    }

    public async Task AddAsync(PriorityRule rule)
    {
        Rules.Add(new PriorityRuleVm(rule));
        await PersistAsync();
    }

    public async Task RemoveAsync(PriorityRuleVm vm)
    {
        Rules.Remove(vm);
        // Removing a rule stops future enforcement but doesn't touch the running process —
        // restore Normal so a deprioritised app doesn't stay stuck until its next restart.
        ApplyToRunningProcesses(vm.ExePath, PriorityLevel.Normal);
        await PersistAsync();
        await SyncEngineAsync();
    }

    public async Task UpdateRuleAsync(PriorityRuleVm vm)
    {
        // The engine only acts on process START, so without this a priority change would
        // do nothing until the app was next relaunched.
        ApplyToRunningProcesses(vm.ExePath, vm.Priority);
        await PersistAsync();
        await SyncEngineAsync();
    }

    private void ApplyToRunningProcesses(string exePath, PriorityLevel level)
    {
        if (_priority is null) return;
        try
        {
            foreach (var pid in _priority.FindPidsForExe(exePath))
                _priority.TrySetPriority(pid, level);
        }
        catch { /* enforcement is best-effort; the rule itself is already saved */ }
    }

    public async Task<(int Added, int Updated)> ApplyRecommendedToAllGamesAsync()
    {
        var games = (await _games.GetAllAsync())
            .Where(g => !string.IsNullOrEmpty(g.InstallPath))
            .ToList();

        var added = 0;
        var updated = 0;
        foreach (var game in games)
        {
            // Each game has potentially multiple EXEs; use the launch executable
            // (best-effort: the first .exe under InstallPath whose name matches the game name).
            var exePath = ResolveLaunchExe(game.InstallPath!);
            if (exePath is null) continue;

            var existing = Rules.FirstOrDefault(r =>
                string.Equals(r.ExePath, exePath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                // Re-apply recommended settings â€” High priority + Protect + Booster â€” if any drift from recommended.
                var changed = existing.Priority != PriorityLevel.High
                           || !existing.ProtectFromRamCleanup
                           || !existing.GameBooster;
                if (!changed) continue;
                existing.Priority = PriorityLevel.High;
                existing.ProtectFromRamCleanup = true;
                existing.GameBooster = true;
                updated++;
                continue;
            }

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

        if (added > 0 || updated > 0) await PersistAsync();
        return (added, updated);
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

