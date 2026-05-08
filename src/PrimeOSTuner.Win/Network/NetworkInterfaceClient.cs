using System.Net.NetworkInformation;

namespace PrimeOSTuner.Win.Network;

public sealed class NetworkInterfaceClient : INetworkInterfaceClient
{
    public IReadOnlyList<string> EnumerateActiveInterfaceGuids()
    {
        var result = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!string.IsNullOrWhiteSpace(nic.Id)) result.Add(nic.Id);
        }
        return result;
    }
}
