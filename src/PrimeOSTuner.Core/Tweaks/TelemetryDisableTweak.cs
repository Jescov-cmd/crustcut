using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class TelemetryDisableTweak : ITweak, ICategorizedTweak
{
    private readonly IRegistryClient _registry;
    private readonly IServiceClient _service;

    public TelemetryDisableTweak(IRegistryClient registry, IServiceClient service)
    {
        _registry = registry;
        _service = service;
    }

    public string Id => "core.telemetry-disable";
    public string DisplayName => "Disable Windows telemetry";
    public string Description => "Disables telemetry registry policies and stops the DiagTrack service.";
    public string Category => "privacy";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    private sealed record Backup(List<RegistryBackup> Registry, string ServiceStartType);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var policy = _registry.ReadDword(
            RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
            "AllowTelemetry");
        var svc = _service.Read("DiagTrack");
        return Task.FromResult(policy == 0 && svc.CurrentStartType == "Disabled"
            ? TweakState.Applied : TweakState.NotApplied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var registryBackups = new List<RegistryBackup>
        {
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                "AllowTelemetry", 0),
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                "AllowTelemetry", 0),
            _registry.WriteDword(RegistryHive.LocalMachine,
                "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                "DoNotShowFeedbackNotifications", 1),
        };
        var prevSvc = _service.Read("DiagTrack");
        _service.Stop("DiagTrack");
        _service.SetStartTypeDisabled("DiagTrack");
        var backup = new Backup(registryBackups, prevSvc.CurrentStartType);
        return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backup)));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var backup = JsonSerializer.Deserialize<Backup>(undoData)
            ?? throw new InvalidOperationException("Invalid undo data");
        foreach (var b in backup.Registry) _registry.RestoreFromBackup(b);
        _service.SetStartType("DiagTrack", backup.ServiceStartType);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will set 3 telemetry registry policies to 0 and disable the DiagTrack service.");
}
