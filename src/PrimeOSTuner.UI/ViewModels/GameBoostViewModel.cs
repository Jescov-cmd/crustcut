using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProfileApplier _applier;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isWorking;

    // Persisted toggle state — survives navigation because the VM is a singleton.
    [ObservableProperty] private bool _isBasicActive;
    [ObservableProperty] private bool _isPerformanceActive;
    [ObservableProperty] private bool _isAggressiveActive;

    public GameBoostViewModel(ProfileApplier applier)
    {
        _applier = applier;
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
