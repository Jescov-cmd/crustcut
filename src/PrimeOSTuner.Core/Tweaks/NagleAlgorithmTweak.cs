using System.Text.Json;
using Microsoft.Win32;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class NagleAlgorithmTweak : ITweak
{
    private const string BaseKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private static readonly string[] ValueNames = { "TcpAckFrequency", "TCPNoDelay" };

    private readonly IRegistryClient _registry;
    private readonly INetworkInterfaceClient _nics;

    public string Id => "game.nagle-algorithm";
    public string DisplayName => "Disable Nagle's Algorithm";
    public string Description => "Sends small packets immediately. Lower lag online.";
    public bool RequiresElevation => true;
    public bool IsDestructive => false;

    public NagleAlgorithmTweak(IRegistryClient registry, INetworkInterfaceClient nics)
    {
        _registry = registry;
        _nics = nics;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        var guids = _nics.EnumerateActiveInterfaceGuids();
        if (guids.Count == 0) return Task.FromResult(TweakState.Unknown);
        foreach (var guid in guids)
        {
            var key = $@"{BaseKey}\{guid}";
            foreach (var name in ValueNames)
                if (_registry.ReadString(RegistryHive.LocalMachine, key, name) != "1")
                    return Task.FromResult(TweakState.NotApplied);
        }
        return Task.FromResult(TweakState.Applied);
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var backups = new List<RegistryBackup>();
        var guids = _nics.EnumerateActiveInterfaceGuids();
        foreach (var guid in guids)
        {
            var key = $@"{BaseKey}\{guid}";
            foreach (var name in ValueNames)
                backups.Add(_registry.WriteString(RegistryHive.LocalMachine, key, name, "1"));
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
        var guids = _nics.EnumerateActiveInterfaceGuids();
        return Task.FromResult($"Will set TcpAckFrequency=1 and TCPNoDelay=1 on {guids.Count} active NIC(s).");
    }
}
