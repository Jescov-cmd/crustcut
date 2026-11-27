using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.Presentation;

/// <summary>
/// One Boost switch that stands for a group of individual settings. On = every setting in
/// the group is on; flipping it applies or undoes each through the Optimize page's own
/// toggle path (same history, same undo, same failure dialogs).
/// </summary>
public partial class BoostGroupVm : ObservableObject
{
    private readonly OptimizeViewModel _optimize;
    private readonly string[] _ids;

    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _state = "";

    public BoostGroupVm(OptimizeViewModel optimize, string title, string description, string icon, params string[] ids)
    {
        _optimize = optimize;
        Title = title;
        Description = description;
        Icon = icon;
        _ids = ids;
    }

    private List<TweakRowVm> Rows => _optimize.AllRows
        .Where(r => r.IsToggle && _ids.Contains(r.Tweak.Id, StringComparer.OrdinalIgnoreCase)).ToList();

    public bool IsAvailable => Rows.Count > 0;

    public void Sync()
    {
        var rows = Rows;
        var on = rows.Count(r => r.IsApplied);
        IsOn = rows.Count > 0 && on == rows.Count;
        State = on == 0 || on == rows.Count ? "" : $"{on} of {rows.Count} on";
    }

    public async Task SetAsync(bool on)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            foreach (var row in Rows.Where(r => r.IsApplied != on))
                await _optimize.ToggleAsync(row, on);
        }
        finally
        {
            IsBusy = false;
            Sync();
        }
    }
}

/// <summary>
/// Boost: five switches instead of forty. Each is a plain promise ("Low input lag") over a
/// group of settings; the full list is still one click away ("Show all settings").
/// </summary>
public partial class BoostViewModel : ObservableObject
{
    private readonly OptimizeViewModel _optimize;
    private readonly FansViewModel _fans;
    private readonly GameBoostViewModel? _gameBoost;
    private readonly AppSettingsStore _settings;

    public BoostGroupVm LowLag { get; }
    public BoostGroupVm Online { get; }

    [ObservableProperty] private bool _isLoaded;

    public BoostViewModel(OptimizeViewModel optimize, FansViewModel fans,
        GameBoostViewModel? gameBoost, AppSettingsStore settings)
    {
        _optimize = optimize;
        _fans = fans;
        _gameBoost = gameBoost;
        _settings = settings;

        LowLag = new BoostGroupVm(optimize, "Low input lag",
            "Mouse moves 1:1, Windows gives the game first claim on the CPU, and recording and fullscreen tricks stay out of the way.",
            "",
            "game.mouse-accel", "game.game-mode", "core.win32-priority-separation",
            "game.mmcss-games-priority", "game.system-responsiveness",
            "core.fullscreen-optimizations", "core.game-dvr-disable");
        Online = new BoostGroupVm(optimize, "Less lag online",
            "Fixes network cards that ship with tiny buffers, and sends game packets right away instead of batching them.",
            "",
            "game.nic-latency", "game.tcp-ack-frequency");
    }

    /// <summary>Called once the Optimize page's probe has read the real system state.</summary>
    public void Sync()
    {
        LowLag.Sync();
        Online.Sync();
        IsLoaded = true;
        OnPropertyChanged(nameof(SmartFans));
        OnPropertyChanged(nameof(SleepApps));
        OnPropertyChanged(nameof(GameMode));
    }

    /// <summary>Applies the game set while any game runs, puts it all back after.</summary>
    public bool GameMode
    {
        get { try { return _settings.Load().GameModeAuto; } catch { return false; } }
        set
        {
            try { _settings.Update(s => s.GameModeAuto = value); } catch { }
            OnPropertyChanged();
        }
    }

    public bool FansAvailable => _fans.IsSupported;

    /// <summary>On = Crustcut drives the fans in Auto. Off = back to the motherboard.</summary>
    public bool SmartFans
    {
        get => _fans.Enabled && _fans.IsAuto;
        set
        {
            if (value)
            {
                _fans.Toggle(true);
                _fans.SelectMode(PrimeOSTuner.Core.Fans.FanMode.Auto);
            }
            else _fans.Toggle(false);
            OnPropertyChanged();
        }
    }

    public bool SleepAvailable => _gameBoost?.GameOnlyAvailable == true;

    public bool SleepApps
    {
        get => _gameBoost?.GameOnlyEnabled ?? false;
        set
        {
            if (_gameBoost is not null) _gameBoost.GameOnlyEnabled = value;
            OnPropertyChanged();
        }
    }

    public string AllSettingsText => $"All {_optimize.AllRows.Count(r => r.IsToggle)} settings";
}
