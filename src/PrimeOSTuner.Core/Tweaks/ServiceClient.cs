using System.ServiceProcess;
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class ServiceClient : IServiceClient
{
    public ServiceState Read(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            // Trigger an actual query to detect non-existence.
            _ = sc.Status;
            var startType = ReadStartType(serviceName);
            return new ServiceState(true, startType, sc.Status == ServiceControllerStatus.Running);
        }
        catch (InvalidOperationException) { return new ServiceState(false, "Unknown", false); }
    }

    public void SetStartTypeDisabled(string serviceName) => SetStartType(serviceName, "Disabled");

    public void SetStartType(string serviceName, string startType)
    {
        // Set via registry — works regardless of whether the service is running.
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
        if (key is null) return;
        var dword = startType switch
        {
            "Boot" => 0,
            "System" => 1,
            "Auto" => 2,
            "Manual" => 3,
            "Disabled" => 4,
            _ => throw new ArgumentException($"Unknown start type: {startType}", nameof(startType))
        };
        key.SetValue("Start", dword, RegistryValueKind.DWord);
    }

    public void Stop(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
        catch { /* swallow — best-effort */ }
    }

    private static string ReadStartType(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        if (key is null) return "Unknown";
        var v = key.GetValue("Start");
        return v switch
        {
            int i => i switch
            {
                0 => "Boot", 1 => "System", 2 => "Auto", 3 => "Manual", 4 => "Disabled", _ => "Unknown"
            },
            _ => "Unknown"
        };
    }
}
