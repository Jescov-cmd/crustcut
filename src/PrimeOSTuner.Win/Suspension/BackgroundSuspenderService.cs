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
    private readonly List<SuspendedProcessInfo> _suspended = new();
    private readonly object _gate = new();

    public IReadOnlyList<SuspendedProcessInfo> Currently
    {
        get { lock (_gate) return _suspended.ToArray(); }
    }

    public event EventHandler? Changed;

    public BackgroundSuspenderService(
        IProcessSuspender suspender,
        IReadOnlyList<string>? targetNames = null,
        Func<string, IEnumerable<int>>? processIdsByName = null)
    {
        _suspender = suspender;
        _targetNames = targetNames ?? BackgroundSuspendList.Default;
        _processIdsByName = processIdsByName
            ?? (name => Process.GetProcessesByName(name).Select(p => p.Id));
    }

    public IReadOnlyList<SuspendedProcessInfo> SuspendBackgroundApps()
    {
        var newly = new List<SuspendedProcessInfo>();
        lock (_gate)
        {
            foreach (var name in _targetNames)
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
        }
        foreach (var info in snapshot) _suspender.Resume(info.Pid);
        if (snapshot.Length > 0) Changed?.Invoke(this, EventArgs.Empty);
    }
}
