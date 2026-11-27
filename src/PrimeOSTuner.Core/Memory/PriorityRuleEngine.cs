using PrimeOSTuner.Core.Safety;

namespace PrimeOSTuner.Core.Memory;

public sealed class PriorityRuleEngine : IDisposable
{
    private readonly IProcessWatcher _watcher;
    private readonly IPriorityClient _priority;
    private readonly IGameBooster _booster;
    private readonly object _lock = new();
    private Dictionary<string, PriorityRule> _rulesByExeName = new(StringComparer.OrdinalIgnoreCase);
    private List<PriorityRule> _allRules = new();

    public PriorityRuleEngine(IProcessWatcher watcher, IPriorityClient priority, IGameBooster booster)
    {
        _watcher = watcher;
        _priority = priority;
        _booster = booster;
        _watcher.ProcessStarted += OnProcessStarted;
    }

    public Task ReloadAsync(IEnumerable<PriorityRule> rules)
    {
        lock (_lock)
        {
            // Device software is never deprioritised or capped, whatever a rule says — a
            // rule file from an older build, or a hand edit, must not get past this.
            _allRules = rules.Select(DeviceSoftware.Sanitize).ToList();
            // Index by EXE filename (lowercased) for fast lookup on process-start events.
            _rulesByExeName = _allRules
                .GroupBy(r => Path.GetFileName(r.ExePath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        return Task.CompletedTask;
    }

    public void Start() => _watcher.Start();

    // WMI raises this on its own thread. async void + an exception after the first await
    // would take the whole app down, so the work runs as an observed task instead.
    private void OnProcessStarted(object? sender, ProcessStartedEvent e) =>
        _ = HandleStartedAsync(e).ContinueWith(t => _ = t.Exception,
            TaskContinuationOptions.OnlyOnFaulted);

    private async Task HandleStartedAsync(ProcessStartedEvent e)
    {
        PriorityRule? rule;
        List<string> protectExes;
        lock (_lock)
        {
            if (!_rulesByExeName.TryGetValue(e.ProcessName, out rule)) return;
            protectExes = _allRules
                .Where(r => r.ProtectFromRamCleanup)
                .Select(r => r.ExePath)
                .ToList();
        }

        // The WMI ProcessName comes from Win32_ProcessStartTrace and is just the EXE filename.
        // Verify the running PID's EXE path matches the rule's ExePath before applying —
        // by asking about that one pid. (This used to snapshot every process on the
        // machine, on every launch of node.exe, Code.exe and friends.)
        var path = _priority.ImagePathOf(e.Pid);
        var matches = path is not null
            ? path.Equals(rule.ExePath, StringComparison.OrdinalIgnoreCase)
            : _priority.FindPidsForExe(rule.ExePath).Contains(e.Pid);
        if (!matches) return;

        // Rules were sanitised on load; check the running image's own name as well.
        if (DeviceSoftware.IsProtected(e.ProcessName))
            rule = DeviceSoftware.Sanitize(rule with { ExePath = e.ProcessName });

        _priority.TrySetPriority(e.Pid, rule.Priority);
        // Working-set caps die with the process, so every fresh launch gets re-capped here.
        if (rule.MemoryLimitMb is int limitMb)
            _priority.TrySetMemoryLimit(e.Pid, limitMb, hard: !rule.LimitAutoAssigned);

        if (rule.GameBooster)
        {
            var protectPids = _priority.FindPidsForExes(protectExes);
            await _booster.QueueAsync(e.Pid, protectPids);
        }
    }

    public void Dispose()
    {
        _watcher.ProcessStarted -= OnProcessStarted;
    }
}
