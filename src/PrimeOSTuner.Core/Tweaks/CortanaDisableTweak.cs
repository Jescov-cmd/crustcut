using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class CortanaDisableTweak : ITweak
{
    private const string PolicyKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search";
    private readonly IRegistryClient _registry;

    public CortanaDisableTweak(IRegistryClient registry) { _registry = registry; }

    public string Id => "core.cortana-disable";
    public string DisplayName => "Disable Cortana";
    public string Description => "Disables Cortana voice assistant and web-search policies.";
    public string Category => "privacy";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var allow = _registry.ReadDword(RegistryHive.LocalMachine, PolicyKey, "AllowCortana");
        return Task.FromResult(allow == 0 ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "AllowCortana", 0),
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "DisableWebSearch", 1),
            _registry.WriteDword(RegistryHive.LocalMachine, PolicyKey, "ConnectedSearchUseWeb", 0),
        };
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
        => Task.FromResult($"Will set 3 values under HKLM\\{PolicyKey} to disable Cortana.");
}
