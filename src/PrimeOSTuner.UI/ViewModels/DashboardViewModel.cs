using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Tweaks;

namespace PrimeOSTuner.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly ActiveTweaksStore _activeStore;
    private readonly IEnumerable<ITweak> _tweaks;
    private readonly System.Timers.Timer _refreshTimer = new(2000) { AutoReset = true };

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private long _ramUsedBytes;
    [ObservableProperty] private long _ramTotalBytes;
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private double _gpuTempC;
    [ObservableProperty] private long _networkDownBps;
    [ObservableProperty] private long _networkUpBps;
    [ObservableProperty] private string _networkText = "0 B/s";

    [ObservableProperty] private string? _activeProfileName;
    [ObservableProperty] private string? _activeGameName;
    [ObservableProperty] private bool _hasActiveProfile;

    [ObservableProperty] private int _boostScore;
    [ObservableProperty] private string _boostScoreTier = "—";
    [ObservableProperty] private string _boostScoreSubtitle = "Computing…";

    public ObservableCollection<double> CpuHistory { get; } = new();
    public ObservableCollection<double> RamHistory { get; } = new();
    public ObservableCollection<double> GpuHistory { get; } = new();
    public ObservableCollection<double> NetHistory { get; } = new();

    public DashboardViewModel(SystemSampler sampler, ActiveTweaksStore activeStore, IEnumerable<ITweak> tweaks)
    {
        _sampler = sampler;
        _activeStore = activeStore;
        _tweaks = tweaks;
        _sampler.Sampled += OnSampled;
        _sampler.Start();
        _refreshTimer.Elapsed += async (_, _) => await RefreshActiveAsync();
        _refreshTimer.Start();
        _ = RefreshBoostScoreAsync();
    }

    public async Task RefreshBoostScoreAsync()
    {
        try
        {
            var result = await BoostScoreCalculator.ComputeAsync(_tweaks);
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Action update = () =>
            {
                BoostScore = result.Score;
                BoostScoreTier = result.Tier;
                BoostScoreSubtitle = result.Total == 0
                    ? "No tweaks available."
                    : $"{result.Applied} of {result.Total} optimizations active";
            };
            if (dispatcher is null || dispatcher.CheckAccess()) update();
            else dispatcher.Invoke(update);
        }
        catch
        {
            // Probe failures already get bucketed as Unknown by the calculator;
            // a thrown exception here means something deeper went wrong — don't crash the dashboard.
        }
    }

    private async Task RefreshActiveAsync()
    {
        var rec = await _activeStore.LoadAsync();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        Action update = () =>
        {
            HasActiveProfile = rec is not null;
            ActiveProfileName = rec?.ProfileId;
            ActiveGameName = rec?.GameId;
        };
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.Invoke(update);
    }

    private void OnSampled(object? sender, SystemSample s)
    {
        // Marshal to UI thread when running under WPF; tests run synchronously on test thread.
        var dispatcher = Application.Current?.Dispatcher;
        Action update = () =>
        {
            CpuPercent = s.CpuPercent;
            RamPercent = s.RamPercent;
            RamUsedBytes = s.RamUsedBytes;
            RamTotalBytes = s.RamTotalBytes;
            GpuPercent = s.GpuPercent;
            GpuTempC = s.GpuTempC;
            NetworkDownBps = s.NetworkDownBps;
            NetworkUpBps = s.NetworkUpBps;
            NetworkText = FormatBytesPerSec(s.NetworkDownBps + s.NetworkUpBps);
            Push(CpuHistory, s.CpuPercent);
            Push(RamHistory, s.RamPercent);
            Push(GpuHistory, s.GpuPercent);
            // Push raw bytes/sec — auto-scale handles fitting it into the chart range.
            Push(NetHistory, s.NetworkDownBps + s.NetworkUpBps);
        };
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.Invoke(update);
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 60) series.RemoveAt(0);
    }

    private static string FormatBytesPerSec(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B/s";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB/s";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB/s";
        double gb = mb / 1024.0;
        return $"{gb:F2} GB/s";
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _sampler.Stop();
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}
