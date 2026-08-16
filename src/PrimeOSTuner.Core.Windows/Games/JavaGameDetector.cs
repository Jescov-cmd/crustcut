using System.Management;
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Windows.Games;

/// <summary>
/// Finds Minecraft running inside javaw/java by reading process command lines (WMI —
/// the only place a command line is readable without opening the process). Results are
/// cached per pid: command lines are immutable for a process's lifetime, so each pid is
/// classified exactly once no matter how often the watcher polls.
/// </summary>
public sealed class JavaGameDetector
{
    private readonly Dictionary<int, bool> _classified = new();

    /// <summary>The synthetic Minecraft game if it's running right now, else null.</summary>
    public KnownGame? DetectRunning()
    {
        try
        {
            var alive = new HashSet<int>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'");
            var any = false;
            foreach (var mo in searcher.Get())
            {
                var pid = Convert.ToInt32(mo["ProcessId"]);
                alive.Add(pid);
                if (!_classified.TryGetValue(pid, out var isMc))
                {
                    isMc = JavaGameHeuristics.IsMinecraft(mo["CommandLine"]?.ToString());
                    _classified[pid] = isMc;
                }
                any |= isMc;
            }
            // Forget exited pids so the cache can't misclassify a recycled pid.
            foreach (var dead in _classified.Keys.Where(p => !alive.Contains(p)).ToList())
                _classified.Remove(dead);
            return any ? JavaGameHeuristics.MinecraftJava : null;
        }
        catch
        {
            return null;   // WMI hiccup: report "no game" this tick, try again next
        }
    }
}
