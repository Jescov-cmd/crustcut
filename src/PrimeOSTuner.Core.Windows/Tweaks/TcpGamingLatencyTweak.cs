using System.Text.Json;
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// The one TCPOptimizer setting that still earns its keep on modern Windows:
/// TcpAckFrequency=1 + TCPNoDelay=1 per network interface. Windows normally delays TCP
/// ACKs up to 200 ms and batches small sends (Nagle); both trade latency for efficiency —
/// the wrong trade for games. Applied to every interface subkey; undo restores the exact
/// previous per-interface values (including "value absent").
/// </summary>
public sealed class TcpGamingLatencyTweak : ITweak, ICategorizedTweak
{
    private const string InterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public string Id => "game.tcp-ack-frequency";
    public string DisplayName => "Lower network latency (TCP ACK)";
    public string Description =>
        "Stops Windows delaying TCP acknowledgements (up to 200 ms) and batching small " +
        "packets. Noticeably snappier in online games; slightly more network chatter.";
    public string Category => "network";
    public string? RiskNote => null;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => true;   // existing connections keep old behaviour until restart

    private sealed record InterfaceBackup(int? AckFrequency, int? NoDelay);

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(InterfacesKey);
            if (root is null) return Task.FromResult(TweakState.Unknown);

            var any = false;
            foreach (var name in root.GetSubKeyNames())
            {
                using var iface = root.OpenSubKey(name);
                if (iface is null) continue;
                any = true;
                if (iface.GetValue("TcpAckFrequency") is not 1 || iface.GetValue("TCPNoDelay") is not 1)
                    return Task.FromResult(TweakState.NotApplied);
            }
            return Task.FromResult(any ? TweakState.Applied : TweakState.Unknown);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var backups = new Dictionary<string, InterfaceBackup>();
            using var root = Registry.LocalMachine.OpenSubKey(InterfacesKey, writable: true)
                ?? throw new InvalidOperationException("TCP interfaces registry key not found.");

            foreach (var name in root.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                using var iface = root.OpenSubKey(name, writable: true);
                if (iface is null) continue;
                backups[name] = new InterfaceBackup(
                    iface.GetValue("TcpAckFrequency") as int?,
                    iface.GetValue("TCPNoDelay") as int?);
                iface.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                iface.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
            }
            return Task.FromResult(TweakResult.Success(JsonSerializer.Serialize(backups)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        try
        {
            var backups = JsonSerializer.Deserialize<Dictionary<string, InterfaceBackup>>(undoData)
                ?? throw new InvalidOperationException("Invalid undo data");
            using var root = Registry.LocalMachine.OpenSubKey(InterfacesKey, writable: true)
                ?? throw new InvalidOperationException("TCP interfaces registry key not found.");

            foreach (var (name, backup) in backups)
            {
                using var iface = root.OpenSubKey(name, writable: true);
                if (iface is null) continue;   // interface disappeared since apply — fine
                Restore(iface, "TcpAckFrequency", backup.AckFrequency);
                Restore(iface, "TCPNoDelay", backup.NoDelay);
            }
            return Task.FromResult(TweakResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }

        static void Restore(RegistryKey iface, string valueName, int? previous)
        {
            if (previous is int v) iface.SetValue(valueName, v, RegistryValueKind.DWord);
            else iface.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will set TcpAckFrequency=1 and TCPNoDelay=1 on every network interface (undo restores previous values).");
}
