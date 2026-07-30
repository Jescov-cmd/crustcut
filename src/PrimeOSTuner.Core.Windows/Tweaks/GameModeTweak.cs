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
    public string Description => "Enables Windows Game Mode. Suppresses background notifications, pauses driver updates, and reserves CPU/GPU for the active fullscreen game.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public GameModeTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        // These are REG_DWORD values — Windows' Game Bar stores and rewrites them
        // as DWORDs. Reading them as strings returns null (so the tile always showed
        // "not applied" after a reboot once Game Bar normalized the type).
        foreach (var name in ValueNames)
            if (_registry.ReadDword(RegistryHive.CurrentUser, SubKey, name) != 1)
                return Task.FromResult(TweakState.NotApplied);
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var name in ValueNames)
            backups.Add(_registry.WriteDword(RegistryHive.CurrentUser, SubKey, name, 1));
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
