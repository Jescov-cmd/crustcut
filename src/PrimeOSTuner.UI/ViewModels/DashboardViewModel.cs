using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Profiles;

namespace PrimeOSTuner.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly ActiveTweaksStore _activeStore;
    private readonly System.Timers.Timer _refreshTimer = new(2000) { AutoReset = true };

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private long _ramUsedBytes;
    [ObservableProperty] private long _ramTotalBytes;
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private double _gpuTempC;
    [ObservableProperty] private long _networkDownBps;
    [ObservableProperty] private long _networkUpBps;

    [ObservableProperty] private string? _activeProfileName;
    [ObservableProperty] private string? _activeGameName;
    [ObservableProperty] private bool _hasActiveProfile;

    public ObservableCollection<double> CpuHistory { get; } = new();
    public ObservableCollection<double> RamHistory { get; } = new();
    public ObservableCollection<double> GpuHistory { get; } = new();
    public ObservableCollection<double> NetHistory { get; } = new();

    public DashboardViewModel(SystemSampler sampler, ActiveTweaksStore activeStore)
    {
        _sampler = sampler;
        _activeStore = activeStore;
        _sampler.Sampled += OnSampled;
        _sampler.Start();
        _refreshTimer.Elapsed += async (_, _) => await RefreshActiveAsync();
        _refreshTimer.Start();
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
            Push(CpuHistory, s.CpuPercent);
            Push(RamHistory, s.RamPercent);
            Push(GpuHistory, s.GpuPercent);
            Push(NetHistory, Math.Min(100, (s.NetworkDownBps + s.NetworkUpBps) / 1_000_000.0));
        };
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.Invoke(update);
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 60) series.RemoveAt(0);
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _sampler.Stop();
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}
