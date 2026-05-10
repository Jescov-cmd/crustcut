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
            _allRules = rules.ToList();
            // Index by EXE filename (lowercased) for fast lookup on process-start events.
            _rulesByExeName = _allRules
                .GroupBy(r => Path.GetFileName(r.ExePath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        return Task.CompletedTask;
    }

    public void Start() => _watcher.Start();

    private async void OnProcessStarted(object? sender, ProcessStartedEvent e)
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
        // Verify the running PID's EXE path matches the rule's ExePath before applying.
        var matchingPids = _priority.FindPidsForExe(rule.ExePath);
        if (!matchingPids.Contains(e.Pid)) return;

        _priority.TrySetPriority(e.Pid, rule.Priority);

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
