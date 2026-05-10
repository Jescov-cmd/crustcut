using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class GameModeTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\GameBar";
    private static readonly string[] ValueNames = { "AllowAutoGameMode", "AutoGameModeEnabled" };

    private readonly IRegistryClient _registry;

    public string Id => "game.game-mode";
    public string DisplayName => "Turn on Game Mode";
    public string Description => "Lets Windows prioritize games automatically.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public GameModeTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var name in ValueNames)
            if (_registry.ReadString(RegistryHive.CurrentUser, SubKey, name) != "1")
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var name in ValueNames)
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, name, "1"));
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
        => Task.FromResult($"Will set HKCU\\{SubKey}\\AllowAutoGameMode=1 and AutoGameModeEnabled=1.");
}
