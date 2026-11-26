using System.Diagnostics;

namespace PrimeOSTuner.Win.Suspension;

/// <summary>
/// Default <see cref="IBackgroundSuspenderService"/>. Wraps an <see cref="IProcessSuspender"/>
/// plus a curated name list. Process enumeration is overridable so tests don't have
/// to spawn real processes.
/// </summary>
public sealed class BackgroundSuspenderService : IBackgroundSuspenderService
{
    private readonly IProcessSuspender _suspender;
    private readonly IReadOnlyList<string> _targetNames;
    private readonly Func<string, IEnumerable<int>> _processIdsByName;
    private readonly Func<IEnumerable<string>> _ownedElsewhere;
    private readonly List<SuspendedProcessInfo> _suspended = new();
    private readonly HashSet<string> _wokenThisGame = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public IReadOnlyList<SuspendedProcessInfo> Currently
    {
        get { lock (_gate) return _suspended.ToArray(); }
    }

    public event EventHandler? Changed;

    public BackgroundSuspenderService(
        IProcessSuspender suspender,
        IReadOnlyList<string>? targetNames = null,
        Func<string, IEnumerable<int>>? processIdsByName = null,
        Func<IEnumerable<string>>? ownedElsewhere = null)
    {
        _suspender = suspender;
        _ownedElsewhere = ownedElsewhere ?? (() => Array.Empty<string>());
        _targetNames = targetNames ?? BackgroundSuspendList.Default;
        _processIdsByName = processIdsByName
            ?? PidsByName;
    }

    // Disposes what it reads: each Process holds a handle that otherwise waits for a finalizer.
    private static IEnumerable<int> PidsByName(string name)
    {
        var procs = Process.GetProcessesByName(name);
        var pids = new int[procs.Length];
        for (var i = 0; i < procs.Length; i++) { pids[i] = procs[i].Id; procs[i].Dispose(); }
        return pids;
    }

    public IReadOnlyList<SuspendedProcessInfo> SuspendBackgroundApps()
    {
        var newly = new List<SuspendedProcessInfo>();
        lock (_gate)
        {
            // Whatever list a profile hands in, never freeze hardware, audio, input or
            // system software (GameOnlyPolicy's list is the single source of truth). Game
            // companions must be awake in games, and apps on the sleep list are that
            // feature's to freeze: suspending a process twice needs two resumes, and two
            // owners would leave it frozen.
            var skip = new HashSet<string>(_ownedElsewhere(), StringComparer.OrdinalIgnoreCase);
            foreach (var name in _targetNames.Where(n =>
                         PrimeOSTuner.Core.Suspension.GameOnlyPolicy.CanFreeze(n)
                         && !PrimeOSTuner.Core.Suspension.GameOnlyPolicy.WakesForGames(n)
                         && !skip.Contains(n) && !_wokenThisGame.Contains(n)))
            {
                foreach (var pid in _processIdsByName(name))
                {
                    if (_suspended.Any(s => s.Pid == pid)) continue;
                    _suspender.Suspend(pid);
                    var info = new SuspendedProcessInfo(pid, name);
                    _suspended.Add(info);
                    newly.Add(info);
                }
            }
        }
        if (newly.Count > 0) Changed?.Invoke(this, EventArgs.Empty);
        return newly;
    }

    public void ResumeAll()
    {
        SuspendedProcessInfo[] snapshot;
        lock (_gate)
        {
            snapshot = _suspended.ToArray();
            _suspended.Clear();
            _wokenThisGame.Clear();
        }
        foreach (var info in snapshot) _suspender.Resume(info.Pid);
        if (snapshot.Length > 0) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Resume(string name)
    {
        SuspendedProcessInfo[] mine;
        lock (_gate)
        {
            mine = _suspended.Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var s in mine) _suspended.Remove(s);
            _wokenThisGame.Add(name);   // a profile re-applying mustn't freeze it again
        }
        foreach (var info in mine) _suspender.Resume(info.Pid);
        if (mine.Length > 0) Changed?.Invoke(this, EventArgs.Empty);
    }
}
