using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class MouseAccelTweak : ITweak
{
    private const string SubKey = @"Control Panel\Mouse";
    private static readonly (string Name, string Value)[] Targets =
    {
        ("MouseSpeed", "0"),
        ("MouseThreshold1", "0"),
        ("MouseThreshold2", "0"),
    };

    private readonly IRegistryClient _registry;

    public string Id => "game.mouse-accel";
    public string DisplayName => "Disable mouse acceleration";
    public string Description => "Makes mouse movement 1:1 with cursor.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public MouseAccelTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        foreach (var (name, expected) in Targets)
        {
            var current = _registry.ReadString(RegistryHive.CurrentUser, SubKey, name);
            if (current != expected)
                return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        foreach (var (name, value) in Targets)
        {
            backups.Add(_registry.WriteString(RegistryHive.CurrentUser, SubKey, name, value));
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
        var speed = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseSpeed") ?? "(unset)";
        var t1 = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold1") ?? "(unset)";
        var t2 = _registry.ReadString(RegistryHive.CurrentUser, SubKey, "MouseThreshold2") ?? "(unset)";
        return Task.FromResult(
            $"Will set HKCU\\{SubKey}: MouseSpeed {speed}->0, MouseThreshold1 {t1}->0, MouseThreshold2 {t2}->0.");
    }
}
