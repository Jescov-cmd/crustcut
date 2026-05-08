namespace PrimeOSTuner.Win.Network;

public interface INetworkInterfaceClient
{
    /// <summary>Enumerate operational network adapters' interface GUIDs (the registry-key-style strings used under Tcpip\Parameters\Interfaces).</summary>
    IReadOnlyList<string> EnumerateActiveInterfaceGuids();
}
