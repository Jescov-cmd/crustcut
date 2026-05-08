using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class VisualEffectsTweak : ITweak
{
    private const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string ValueName = "VisualFXSetting";

    private readonly IRegistryClient _registry;

    public string Id => "core.visual-effects";
    public string DisplayName => "Optimize visual effects for performance";
    public string Description => "Disables animations, transparency, and shadows to free GPU/CPU cycles.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public VisualEffectsTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.CurrentUser, SubKey, ValueName);
        return Task.FromResult(v == "2" ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.CurrentUser, SubKey, ValueName, "2");
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<RegistryBackup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        _registry.RestoreFromBackup(backup);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var current = _registry.ReadString(RegistryHive.CurrentUser, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKCU\\{SubKey}\\{ValueName} from '{current}' to '2' (best performance).");
    }
}
