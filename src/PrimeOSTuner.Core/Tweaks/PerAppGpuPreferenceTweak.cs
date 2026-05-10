using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class PerAppGpuPreferenceTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueData = "GpuPreference=2;";

    private readonly IRegistryClient _registry;
    private readonly IReadOnlyList<string> _exePaths;

    public string Id => "game.per-app-gpu-pref";
    public string DisplayName => "Use the fast GPU for games";
    public string Description => "Forces high-performance GPU on detected games.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public PerAppGpuPreferenceTweak(IRegistryClient registry, IEnumerable<string> exePaths)
    {
        _registry = registry;
        _exePaths = exePaths.ToList();
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        if (_exePaths.Count == 0) return Task.FromResult(TweakState.NotApplied);
        foreach (var path in _exePaths)
            if (_registry.ReadString(RegistryHive.CurrentUser, SubKey, path) != ValueData)
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        for (int i = 0; i < _exePaths.Count; i++)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, _exePaths[i], ValueData));
            progress?.Report((i + 1) * 100 / Math.Max(1, _exePaths.Count));
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
        => Task.FromResult($"Will set HKCU\\{SubKey} entries for {_exePaths.Count} executable(s) to '{ValueData}'.");
}
