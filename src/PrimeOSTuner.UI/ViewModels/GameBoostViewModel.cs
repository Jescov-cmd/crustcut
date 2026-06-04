using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.Win.Suspension;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProfileApplier _applier;
    private readonly AppSettingsStore _settings;
    private readonly IBackgroundSuspenderService? _suspender;
    private bool _suppressSave;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isWorking;

    // Persisted toggle state — survives navigation because the VM is a singleton.
    [ObservableProperty] private bool _isBasicActive;
    [ObservableProperty] private bool _isPerformanceActive;
    [ObservableProperty] private bool _isAggressiveActive;

    public ObservableCollection<SuspendedProcessInfo> SuspendedApps { get; } = new();

    public GameBoostViewModel(ProfileApplier applier, AppSettingsStore settings, IBackgroundSuspenderService? suspender = null)
    {
        _applier = applier;
        _settings = settings;
        _suspender = suspender;

        // Restore the last-activated mode so the toggle reflects what's still active after a
        // restart, instead of always resetting to off. Suppressed so restoring doesn't re-save.
        _suppressSave = true;
        switch (settings.Load().GameBoostMode)
        {
            case "basic":       IsBasicActive = true; break;
            case "performance": IsPerformanceActive = true; break;
            case "aggressive":  IsAggressiveActive = true; break;
        }
        _suppressSave = false;

        if (_suspender is not null)
        {
            _suspender.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshSuspended);
            RefreshSuspended();
        }
    }

    partial void OnIsBasicActiveChanged(bool value) => PersistMode();
    partial void OnIsPerformanceActiveChanged(bool value) => PersistMode();
    partial void OnIsAggressiveActiveChanged(bool value) => PersistMode();

    // Persist whichever mode is active (or "" when all are off) so it survives a restart.
    private void PersistMode()
    {
        if (_suppressSave) return;
        var mode = IsAggressiveActive ? "aggressive"
                 : IsPerformanceActive ? "performance"
                 : IsBasicActive ? "basic" : "";
        try
        {
            var s = _settings.Load();
            if (s.GameBoostMode != mode) { s.GameBoostMode = mode; _settings.Save(s); }
        }
        catch { /* persistence is best-effort */ }
    }

    private void RefreshSuspended()
    {
        SuspendedApps.Clear();
        if (_suspender is null) return;
        foreach (var info in _suspender.Currently) SuspendedApps.Add(info);
    }

    public async Task ApplyBasicAsync()
    {
        IsBasicActive = true; IsPerformanceActive = false; IsAggressiveActive = false;
        await ApplyAsync(BuiltInProfiles.Basic);
    }

    public async Task ApplyPerformanceAsync()
    {
        IsPerformanceActive = true; IsBasicActive = false; IsAggressiveActive = false;
        await ApplyAsync(BuiltInProfiles.Performance);
    }

    public async Task ApplyAggressiveAsync()
    {
        IsAggressiveActive = true; IsBasicActive = false; IsPerformanceActive = false;
        await ApplyAsync(BuiltInProfiles.Aggressive);
    }

    private async Task ApplyAsync(ModeProfile profile)
    {
        IsWorking = true;
        StatusMessage = $"Applying {profile.DisplayName}...";
        try
        {
            var result = await _applier.ApplyAsync(profile);
            StatusMessage = $"{profile.DisplayName}: {result.SuccessCount} applied, {result.FailureCount} failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }
}
