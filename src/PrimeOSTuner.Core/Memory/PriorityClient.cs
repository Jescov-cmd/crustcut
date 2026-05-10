using System.Diagnostics;

namespace PrimeOSTuner.Core.Memory;

public sealed class PriorityClient : IPriorityClient
{
    public bool TrySetPriority(int pid, PriorityLevel level)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.PriorityClass = level switch
            {
                PriorityLevel.High => ProcessPriorityClass.High,
                PriorityLevel.AboveNormal => ProcessPriorityClass.AboveNormal,
                PriorityLevel.Normal => ProcessPriorityClass.Normal,
                PriorityLevel.BelowNormal => ProcessPriorityClass.BelowNormal,
                _ => ProcessPriorityClass.Normal
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<int> FindPidsForExe(string exePath)
        => FindPidsForExes(new[] { exePath });

    public IReadOnlyList<int> FindPidsForExes(IEnumerable<string> exePaths)
    {
        var set = new HashSet<string>(exePaths, StringComparer.OrdinalIgnoreCase);
        var pids = new List<int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (path is not null && set.Contains(path))
                    pids.Add(p.Id);
            }
            catch
            {
                // Access denied to query module path — common for system processes.
            }
            finally
            {
                p.Dispose();
            }
        }
        return pids;
    }
}
