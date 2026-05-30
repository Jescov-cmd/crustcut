using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Lifecycle;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Core.Settings;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SentinelViewModel : ObservableObject
{
    private const int MaxRecentAlerts = 5;
    private readonly ISentinelService _service;
    private readonly IFrameSessionStore _frames;
    private readonly AppSettingsStore _settingsStore;
    private readonly ProfileLifecycleService _lifecycle;
    private bool _redDotLatched;   // stays true until the user opens the tab
    private bool _silencedUntilNextEmpty;
    private bool _suspendEnabledSave;

    public SentinelRowViewModel Vram { get; } = new() { Label = "VRAM" };
    public SentinelRowViewModel Ram  { get; } = new() { Label = "System RAM" };
    public SentinelRowViewModel Cpu  { get; } = new() { Label = "System CPU" };

    public ObservableCollection<Problem> RecentAlerts { get; } = new();

    [ObservableProperty] private string _status = "Not watching any game right now.";
    [ObservableProperty] private bool _hasActiveProblem;
    [ObservableProperty] private bool _enabled = true;

    // Last finished game's performance (from PresentMon frame recording).
    [ObservableProperty] private bool _hasLastSession;
    [ObservableProperty] private string _lastGameName = "";
    [ObservableProperty] private string _lastWhen = "";
    [ObservableProperty] private string _lastAvgFps = "--";
    [ObservableProperty] private string _lastMaxFps = "--";
    [ObservableProperty] private string _lastOnePctLow = "--";

    public SentinelViewModel(
        ISentinelService service,
        IFrameSessionStore frames,
        AppSettingsStore settingsStore,
        ProfileLifecycleService lifecycle)
    {
        _service = service;
        _frames = frames;
        _settingsStore = settingsStore;
        _lifecycle = lifecycle;

        _suspendEnabledSave = true;
        Enabled = _service.Enabled;
        _suspendEnabledSave = false;

        _service.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        _frames.Updated += (_, _) => Application.Current?.Dispatcher.BeginInvoke(LoadLastSession);
        LoadLastSession();
        Refresh();
    }

    partial void OnEnabledChanged(bool value)
    {
        _service.Enabled = value;
        if (!_suspendEnabledSave)
        {
            try
            {
                var s = _settingsStore.Load();
                s.SentinelEnabled = value;
                _settingsStore.Save(s);
            }
            catch { /* persistence best-effort */ }
        }
        // Turning Sentinel on while a game is already running — start watching immediately.
        if (value && _lifecycle.CurrentRunningGame is { } current)
        {
            try { _service.OnGameStarted(current.Game, current.Pid); }
            catch { }
        }
        Refresh();
    }

    private void LoadLastSession()
    {
        try
        {
            var last = _frames.Load().FirstOrDefault();
            if (last is null) { HasLastSession = false; return; }

            HasLastSession = true;
            LastGameName = last.GameName;
            LastWhen = $"{last.StartedAt.ToLocalTime():MMM d, h:mm tt} · {(int)last.Duration.TotalMinutes} min";
            LastAvgFps = $"{last.Stats.AvgFps:F0}";
            LastMaxFps = last.Stats.MaxFps > 0 ? $"{last.Stats.MaxFps:F0}" : "--";
            LastOnePctLow = $"{last.Stats.OnePctLowFps:F0}";
        }
        catch
        {
            HasLastSession = false;
        }
    }

    public void AcknowledgeDot()
    {
        _redDotLatched = false;
        HasActiveProblem = false;
        // Suppress re-latching for the currently-active problem set. The dot is allowed
        // to reappear after the rules go quiet and then fire again — that's a new event.
        _silencedUntilNextEmpty = _service.Currently.Count > 0;
    }

    private void Refresh()
    {
        Status = _service.WatchingGame is null
            ? "Not watching any game right now."
            : $"Watching: {_service.WatchingGame}";

        Reset(Vram); Reset(Ram); Reset(Cpu);
        PopulateLiveValues(_service.LatestSnapshot, _service.CurrentSpec);
        foreach (var p in _service.Currently) ApplyProblem(p);

        if (_service.Currently.Count == 0)
        {
            _silencedUntilNextEmpty = false;
        }
        else
        {
            if (!_silencedUntilNextEmpty) _redDotLatched = true;
            foreach (var p in _service.Currently)
                if (!RecentAlerts.Any(a => a.Kind == p.Kind && a.DetectedAt == p.DetectedAt))
                    RecentAlerts.Insert(0, p);
            while (RecentAlerts.Count > MaxRecentAlerts) RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }
        HasActiveProblem = _redDotLatched;
    }

    private void PopulateLiveValues(MetricsSnapshot? snap, SteamPcRequirements? spec)
    {
        if (snap is not null)
        {
            Vram.Current = snap.VramUsedBytes < 0 || snap.VramTotalBytes <= 0
                ? "VRAM unavailable on this system"
                : $"{snap.VramUsedBytes / 1024 / 1024} MB of {snap.VramTotalBytes / 1024 / 1024} MB";
            Ram.Current  = snap.RamUsedBytes  < 0 || snap.RamTotalBytes  <= 0
                ? "RAM unavailable"
                : $"{snap.RamUsedBytes  / 1024 / 1024} MB of {snap.RamTotalBytes  / 1024 / 1024} MB";
            Cpu.Current  = snap.SystemCpuPercent < 0
                ? "CPU unavailable"
                : $"{snap.SystemCpuPercent:F0}% (system)";
        }

        Vram.Recommended = spec?.RecVramMb is int v ? $"Game wants ≥ {v} MB" : "(spec unknown)";
        Ram.Recommended  = spec?.RecRamMb  is int r ? $"Game wants ≥ {r} MB" : "(spec unknown)";
        Cpu.Recommended  = "Watching for sustained 90%+";
    }

    private static void Reset(SentinelRowViewModel row)
    {
        row.IsProblem = false;
        row.Explanation = "";
    }

    private void ApplyProblem(Problem p)
    {
        var row = p.Kind switch
        {
            ProblemKind.VramOverhead => Vram,
            ProblemKind.RamPressure  => Ram,
            ProblemKind.CpuSaturated => Cpu,
            _ => null
        };
        if (row is null) return;
        row.IsProblem = true;
        row.Explanation = p.Detail;
    }
}
