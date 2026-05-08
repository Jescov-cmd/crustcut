using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Profiles;

namespace PrimeOSTuner.UI.ViewModels;

public partial class GameBoostViewModel : ObservableObject
{
    private readonly ProfileApplier _applier;
    private readonly CustomProfileStore _customStore;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isWorking;

    public GameBoostViewModel(ProfileApplier applier, CustomProfileStore customStore)
    {
        _applier = applier;
        _customStore = customStore;
    }

    public async Task ApplyBasicAsync()
    {
        await ApplyAsync(BuiltInProfiles.Basic);
    }

    public async Task ApplyPerformanceAsync()
    {
        await ApplyAsync(BuiltInProfiles.Performance);
    }

    public async Task ApplyCustomAsync()
    {
        var profile = await _customStore.LoadAsync();
        if (profile.TweakIds.Count == 0)
        {
            StatusMessage = "Custom Mode is empty — pick tweaks in the Custom Mode tab first.";
            return;
        }
        await ApplyAsync(profile);
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
