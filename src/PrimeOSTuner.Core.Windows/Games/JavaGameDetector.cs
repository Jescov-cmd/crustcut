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
    private readonly record struct Classified(bool IsMinecraft, long? MaxHeapBytes);
    private readonly Dictionary<int, Classified> _classified = new();

    /// <summary>How much memory the running Minecraft may take (its -Xmx), when known.
    /// On a 16 GB machine this single number often decides whether the game stutters.</summary>
    public long? MinecraftMaxHeapBytes { get; private set; }

    /// <summary>The synthetic Minecraft game if it's running right now, else null.</summary>
    /// <param name="runningExes">This tick's process names ("javaw.exe"…). The watcher
    /// polls every 2 s; without this, every poll paid for a WMI query even on the
    /// overwhelming majority of ticks when no Java runtime exists at all.</param>
    public KnownGame? DetectRunning(IReadOnlySet<string> runningExes)
    {
        if (!runningExes.Contains("javaw.exe") && !runningExes.Contains("java.exe"))
        {
            _classified.Clear();
            MinecraftMaxHeapBytes = null;
            return null;
        }

        // Which Java processes exist is cheap to ask; WHAT they are needs WMI (the command
        // line). Ask WMI only about pids never seen before. The old version re-queried every
        // tick and, when WMI failed — which it does under heavy memory pressure — reported
        // "no Minecraft", ending the session mid-game; the next tick started a new one.
        // One 10-minute game came out as three sessions. A process already known to be
        // Minecraft now stays Minecraft for as long as it's alive, whatever WMI is doing.
        var live = LiveJavaPids();
        if (live is null)   // couldn't look: keep the last verdict rather than guess "gone"
            return _classified.Values.Any(c => c.IsMinecraft) ? JavaGameHeuristics.MinecraftJava : null;
        foreach (var dead in _classified.Keys.Where(p => !live.Contains(p)).ToList())
            _classified.Remove(dead);   // also stops a recycled pid inheriting a verdict

        if (live.Any(p => !_classified.ContainsKey(p)))
            ClassifyNew(live);   // failures leave pids unclassified; retried next tick

        var minecraft = _classified.Values.Where(c => c.IsMinecraft).ToList();
        MinecraftMaxHeapBytes = minecraft.Select(c => c.MaxHeapBytes).Max();
        return minecraft.Count > 0 ? JavaGameHeuristics.MinecraftJava : null;
    }

    private static HashSet<int>? LiveJavaPids()
    {
        var pids = new HashSet<int>();
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                using (p)
                {
                    var name = p.ProcessName;
                    if (name.Equals("javaw", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("java", StringComparison.OrdinalIgnoreCase))
                        pids.Add(p.Id);
                }
            }
        }
        catch { return null; }
        return pids;
    }

    private void ClassifyNew(HashSet<int> live)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'");
            using var results = searcher.Get();
            foreach (var mo in results)
            {
                using (mo)
                {
                    var pid = Convert.ToInt32(mo["ProcessId"]);
                    if (!live.Contains(pid) || _classified.ContainsKey(pid)) continue;
                    var cmd = mo["CommandLine"]?.ToString();
                    _classified[pid] = new Classified(JavaGameHeuristics.IsMinecraft(cmd),
                        JavaGameHeuristics.MaxHeapBytes(cmd));
                }
            }
        }
        catch { /* WMI hiccup: unclassified pids get another try next tick */ }
    }
}
