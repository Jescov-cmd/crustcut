using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class SystemResponsivenessTweak : ITweak
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string ValueName = "SystemResponsiveness";
    private const string TargetValue = "0";

    private readonly IRegistryClient _registry;

    public string Id => "game.system-responsiveness";
    public string DisplayName => "Maximize multimedia thread priority";
    public string Description => "Sets SystemResponsiveness=0 so multimedia threads (like games) can fully saturate the CPU instead of yielding 20% to background tasks.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public SystemResponsivenessTweak(IRegistryClient registry) { _registry = registry; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var v = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName);
        return Task.FromResult(v == TargetValue ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backup = _registry.WriteString(RegistryHive.LocalMachine, SubKey, ValueName, TargetValue);
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
        var current = _registry.ReadString(RegistryHive.LocalMachine, SubKey, ValueName) ?? "(unset)";
        return Task.FromResult($"Will set HKLM\\{SubKey}\\{ValueName} from '{current}' to '{TargetValue}'.");
    }
}
