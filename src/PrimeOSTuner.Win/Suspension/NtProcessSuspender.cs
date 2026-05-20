using System.Runtime.InteropServices;

namespace PrimeOSTuner.Win.Suspension;

/// <summary>
/// Suspends and resumes processes via the Nt* APIs in ntdll. Errors are
/// swallowed — a missing or exited process is not a problem we want to
/// propagate to a game-launch flow.
/// </summary>
public sealed class NtProcessSuspender : IProcessSuspender
{
    private const uint PROCESS_SUSPEND_RESUME = 0x0800;

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr hProcess);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    public void Suspend(int processId) => InvokeOnHandle(processId, NtSuspendProcess);

    public void Resume(int processId) => InvokeOnHandle(processId, NtResumeProcess);

    private static void InvokeOnHandle(int pid, Func<IntPtr, int> op)
    {
        var handle = OpenProcess(PROCESS_SUSPEND_RESUME, bInheritHandle: false, dwProcessId: (uint)pid);
        if (handle == IntPtr.Zero) return; // process exited or access denied — nothing to do

        try { op(handle); }
        catch { /* defensive: the call shouldn't throw, but never crash a game-launch */ }
        finally { CloseHandle(handle); }
    }
}
