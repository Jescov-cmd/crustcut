namespace PrimeOSTuner.Win.Suspension;

/// <summary>
/// Suspends and resumes user-mode processes by process id. Implementations rely
/// on the (undocumented but very stable) NtSuspendProcess / NtResumeProcess APIs.
///
/// Suspending freezes a process completely — it stops executing until resumed.
/// Use this only for processes whose state survives a freeze (background sync
/// clients, media apps) and never for system processes — see
/// <see cref="ProcessSuspendSafety"/>.
/// </summary>
public interface IProcessSuspender
{
    void Suspend(int processId);
    void Resume(int processId);
}
