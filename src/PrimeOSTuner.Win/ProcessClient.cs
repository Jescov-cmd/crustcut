using System.Diagnostics;

namespace PrimeOSTuner.Win;

public sealed class ProcessClient : IProcessClient
{
    public void TrimWorkingSet(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            PInvoke.EmptyWorkingSet(p.Handle);
        }
        catch (ArgumentException) { /* process exited between enumerate and trim */ }
        catch (InvalidOperationException) { /* same */ }
    }
}
