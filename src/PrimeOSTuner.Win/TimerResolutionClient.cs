using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

public sealed class TimerResolutionClient : ITimerResolutionClient
{
    public uint SetResolution(uint desiredHundredNs)
    {
        PInvoke.NtSetTimerResolution(desiredHundredNs, true, out var actual);
        return actual;
    }

    public void ClearResolution(uint desiredHundredNs)
    {
        PInvoke.NtSetTimerResolution(desiredHundredNs, false, out _);
    }

    public uint GetCurrentResolution()
    {
        NtQueryTimerResolution(out _, out _, out var current);
        return current;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint min, out uint max, out uint current);
}
