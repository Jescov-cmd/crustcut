using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Lifecycle;

namespace PrimeOSTuner.UI.ViewModels;

public partial class WatcherStatusViewModel : ObservableObject
{
    private readonly ProfileLifecycleService _lifecycle;
    private readonly IGameProcessWatcher _watcher;

    [ObservableProperty] private bool _isWatching;
    [ObservableProperty] private string _statusText = "Watching for games";

    public WatcherStatusViewModel(ProfileLifecycleService lifecycle, IGameProcessWatcher watcher)
    {
        _lifecycle = lifecycle;
        _watcher = watcher;
        IsWatching = watcher.IsRunning;
        UpdateText();
    }

    partial void OnIsWatchingChanged(bool value)
    {
        if (value) _lifecycle.Start(); else _lifecycle.Stop();
        UpdateText();
    }

    private void UpdateText()
    {
        StatusText = IsWatching ? "Watching for games" : "Watcher off";
    }
}
