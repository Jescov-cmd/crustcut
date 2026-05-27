using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Sentinel;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SentinelViewModel : ObservableObject
{
    private const int MaxRecentAlerts = 5;
    private readonly ISentinelService _service;
    private bool _redDotLatched;   // stays true until the user opens the tab
    private bool _silencedUntilNextEmpty;

    public SentinelRowViewModel Vram { get; } = new() { Label = "VRAM" };
    public SentinelRowViewModel Ram  { get; } = new() { Label = "System RAM" };
    public SentinelRowViewModel Cpu  { get; } = new() { Label = "System CPU" };

    public ObservableCollection<Problem> RecentAlerts { get; } = new();

    [ObservableProperty] private string _status = "Not watching any game right now.";
    [ObservableProperty] private bool _hasActiveProblem;

    public SentinelViewModel(ISentinelService service)
    {
        _service = service;
        _service.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
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
