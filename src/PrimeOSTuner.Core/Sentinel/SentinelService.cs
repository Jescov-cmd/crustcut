using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Default <see cref="ISentinelService"/>. Owns the per-tick sample loop, the
/// rolling CPU history, and the current problem set.
/// </summary>
public sealed class SentinelService : ISentinelService
{
    private readonly ISpecFetcher _specs;
    private readonly IMetricsSampler _sampler;

    private readonly object _gate = new();
    private readonly LinkedList<(DateTime At, double Percent)> _cpuWindow = new();
    private SteamPcRequirements? _spec;
    private MetricsSnapshot? _latestSnapshot;
    private int _pid;
    private string? _watchingGame;
    private IReadOnlyList<Problem> _currently = Array.Empty<Problem>();
    private bool _enabled = true;
    private int _epoch;
    private System.Threading.Timer? _timer;

    private static readonly TimeSpan CpuWindowSize = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SamplePeriod = TimeSpan.FromSeconds(4);
    private static readonly SteamPcRequirements EmptySpec = new(null, null, null, null);

    public SentinelService(ISpecFetcher specs, IMetricsSampler sampler)
    {
        _specs = specs;
        _sampler = sampler;
    }

    public IReadOnlyList<Problem> Currently { get { lock (_gate) return _currently; } }
    public string? WatchingGame { get { lock (_gate) return _watchingGame; } }
    public MetricsSnapshot? LatestSnapshot { get { lock (_gate) return _latestSnapshot; } }
    public SteamPcRequirements? CurrentSpec { get { lock (_gate) return _spec; } }

    public bool Enabled
    {
        get { lock (_gate) return _enabled; }
        set
        {
            bool changed;
            lock (_gate) { changed = _enabled != value; _enabled = value; }
            if (changed && !value) OnGameStopped();   // toggling off clears state immediately
        }
    }

    public event EventHandler? Changed;

    public void OnGameStarted(KnownGame game, int pid)
    {
        int epoch;
        lock (_gate)
        {
            if (!_enabled) return;
            _watchingGame = game.DisplayName;
            _pid = pid;
            _spec = null;
            _latestSnapshot = null;
            _cpuWindow.Clear();
            _currently = Array.Empty<Problem>();
            _epoch++;
            epoch = _epoch;
        }
        Changed?.Invoke(this, EventArgs.Empty);

        if (!string.IsNullOrWhiteSpace(game.SteamAppId))
            _ = FetchSpecAsync(game.SteamAppId, epoch);

        _timer ??= new System.Threading.Timer(_ => _ = TickOnceAsync(), null, SamplePeriod, SamplePeriod);
    }

    public void OnGameStopped()
    {
        bool fire;
        lock (_gate)
        {
            fire = _watchingGame is not null || _currently.Count > 0;
            _watchingGame = null;
            _pid = 0;
            _spec = null;
            _latestSnapshot = null;
            _cpuWindow.Clear();
            _currently = Array.Empty<Problem>();
            _epoch++;
        }
        _timer?.Dispose();
        _timer = null;
        if (fire) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Test entry point — sample once and evaluate. Production code uses a timer over this.</summary>
    public async Task TickOnceAsync(CancellationToken ct = default)
    {
        int pid;
        SteamPcRequirements spec;
        lock (_gate)
        {
            if (_watchingGame is null) return;
            pid = _pid;
            spec = _spec ?? EmptySpec;
        }

        MetricsSnapshot snap;
        try { snap = await _sampler.SampleAsync(pid, ct); }
        catch { return; }

        lock (_gate)
        {
            _latestSnapshot = snap;
            // Evaluate against the prior history (DetectionRules treats `snap` and
            // `window` as separate inputs — the current sample is the snapshot, the
            // linked list is the trailing window). Then push the current sample and trim.
            _currently = DetectionRules.Evaluate(snap, spec, _cpuWindow);
            PushAndTrimCpu(snap);
            // Even when the problem set is unchanged, the latest snapshot is fresh — so
            // we always raise Changed; the VM reads LatestSnapshot to refresh the metric rows.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PushAndTrimCpu(MetricsSnapshot snap)
    {
        _cpuWindow.AddLast((snap.At, snap.SystemCpuPercent));
        var cutoff = snap.At - CpuWindowSize;
        // Keep one anchor sample on or before cutoff (so boundary ticks still measure full span).
        while (_cpuWindow.Count >= 2
               && _cpuWindow.First!.Next!.Value.At <= cutoff)
        {
            _cpuWindow.RemoveFirst();
        }
    }

    private async Task FetchSpecAsync(string steamAppId, int epoch)
    {
        try
        {
            var spec = await _specs.FetchAsync(steamAppId);
            if (spec is null) return;
            lock (_gate)
            {
                if (_epoch != epoch) return;       // game switched out from under us
                _spec = spec;
            }
        }
        catch { /* failure to fetch = silent degrade */ }
    }
}
