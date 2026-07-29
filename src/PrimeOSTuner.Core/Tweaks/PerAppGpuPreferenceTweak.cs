using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PerAppGpuPreferenceTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueData = "GpuPreference=2;";

    private readonly IRegistryClient _registry;
    private readonly IEnumerable<string> _exePathsSource;

    public string Id => "game.per-app-gpu-pref";
    public string DisplayName => "Use the fast GPU for games";
    public string Description => "Forces high-performance GPU on detected games.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public PerAppGpuPreferenceTweak(IRegistryClient registry, IEnumerable<string> exePaths)
    {
        _registry = registry;
        // Do NOT materialise here: callers pass a lazily-resolved game list, and forcing
        // it in the constructor ran a multi-second library scan during app composition —
        // on the UI thread it deadlocked startup outright (sync-over-async via the
        // Avalonia synchronisation context). Enumerate at probe/apply time instead.
        _exePathsSource = exePaths;
    }

    // Snapshot per use. Cheap after the source's first resolution (it caches).
    private IReadOnlyList<string> Paths() => _exePathsSource.ToList();

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var paths = Paths();
        if (paths.Count == 0) return Task.FromResult(TweakState.NotApplied);
        foreach (var path in paths)
            if (_registry.ReadString(RegistryHive.CurrentUser, SubKey, path) != ValueData)
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var paths = Paths();
        var backups = new List<RegistryBackup>();
        for (int i = 0; i < paths.Count; i++)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, paths[i], ValueData));
            progress?.Report((i + 1) * 100 / Math.Max(1, paths.Count));
        }
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backups)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backups = JsonSerializer.Deserialize<List<RegistryBackup>>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backups) _registry.RestoreFromBackup(b);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set HKCU\\{SubKey} entries for {Paths().Count} executable(s) to '{ValueData}'.");
}
