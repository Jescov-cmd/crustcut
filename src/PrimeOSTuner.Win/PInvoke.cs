using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win;

public static class PInvoke
{
    [DllImport("ntdll.dll")]
    public static extern int NtSetTimerResolution(uint desiredResolution100ns, bool setResolution, out uint currentResolution);
}
