using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Updates;

namespace Crustcut.Presentation;

/// <summary>Applies a staged update; the app layer owns the download/swap machinery.</summary>
public interface IUpdateInstaller
{
    Task<string?> ApplyAsync(AvailableUpdate update, IProgress<string>? progress = null);
}

/// <summary>
/// Drives the "a new version is available" banner. Checking is silent and failure-tolerant
/// — if GitHub is unreachable the user simply never sees a banner.
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateChecker _checker;
    private readonly IUpdateInstaller? _installer;
    private readonly Version _current;
    private readonly IUiDispatcher _ui;

    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _headline = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    public AvailableUpdate? Pending { get; private set; }

    public UpdateViewModel(UpdateChecker checker, Version current, IUiDispatcher ui,
        IUpdateInstaller? installer = null)
    {
        _checker = checker;
        _current = current;
        _ui = ui;
        _installer = installer;
    }

    /// <summary>Looks for a newer release. Safe to call on every launch.</summary>
    public async Task CheckAsync()
    {
        var found = await _checker.CheckAsync(_current);
        if (found is null) return;
        _ui.Post(() =>
        {
            Pending = found;
            Headline = $"Version {found.Version} is available — you have {_current.ToString(3)}.";
            UpdateAvailable = true;
        });
    }

    /// <summary>Downloads and installs the pending update, then the app restarts itself.</summary>
    public async Task UpdateNowAsync()
    {
        if (Pending is null || _installer is null || IsBusy) return;
        IsBusy = true;
        Status = "Starting…";
        var error = await _installer.ApplyAsync(Pending, new Progress<string>(m => _ui.Post(() => Status = m)));
        if (error is not null)
        {
            _ui.Post(() =>
            {
                Status = error;
                IsBusy = false;
            });
        }
        // On success the process is replaced by the new build, so there is nothing to reset.
    }
}
