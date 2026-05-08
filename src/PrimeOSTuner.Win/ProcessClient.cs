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

    public int TrimAllUserProcesses()
    {
        var attempted = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                PInvoke.EmptyWorkingSet(p.Handle);
                attempted++;
            }
            catch { /* protected processes will refuse — that's expected */ }
            finally { p.Dispose(); }
        }
        return attempted;
    }
}
