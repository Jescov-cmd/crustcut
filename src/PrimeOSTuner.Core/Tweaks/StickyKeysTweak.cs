using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Disables the keyboard shortcuts that pop up Sticky / Filter / Toggle Keys
/// dialogs. Doesn't disable the accessibility features themselves — just stops
/// Shift×5, prolonged-Shift, and Num-Lock-hold from taking focus mid-game.
///
/// Standard "shortcut off" Flags values: StickyKeys 506, FilterKeys 122,
/// ToggleKeys 58. All HKCU REG_SZ. Reversible.
/// </summary>
public sealed class StickyKeysTweak : ITweak
{
    private const string StickyKeysKey = @"Control Panel\Accessibility\StickyKeys";
    private const string FilterKeysKey = @"Control Panel\Accessibility\Keyboard Response";
    private const string ToggleKeysKey = @"Control Panel\Accessibility\ToggleKeys";

    private static readonly (string Key, string Value)[] Targets =
    {
        (StickyKeysKey, "506"),
        (FilterKeysKey, "122"),
        (ToggleKeysKey, "58"),
    };

    private readonly IRegistryClient _registry;

    public string Id => "core.sticky-keys-shortcuts";
    public string DisplayName => "Disable accessibility key shortcuts";
    public string Description => "Stops Sticky/Filter/Toggle Keys popups from stealing focus mid-game. The features themselves still work via Settings.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public StickyKeysTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var (key, expected) in Targets)
        {
            var current = _registry.ReadString(RegistryHive.CurrentUser, key, "Flags");
            if (current != expected)
                return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var (key, value) in Targets)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, key, "Flags", value));
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
    {
        return Task.FromResult(
            "Will set HKCU Accessibility Flags: StickyKeys=506, FilterKeys=122, ToggleKeys=58.");
    }
}
