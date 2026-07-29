using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Sentinel;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.Presentation;

/// <summary>
/// Sessions page (was "Sentinel"): the recorded frame-time sessions, plus the switch for
/// watching performance while apps run.
/// </summary>
public partial class SessionsViewModel : ObservableObject
{
    private readonly IFrameSessionStore _frames;
    private readonly ISentinelService _sentinel;
    private readonly AppSettingsStore _settings;
    private readonly IUiDispatcher _ui;
    private bool _restoringEnabled;

    public ObservableCollection<FrameSessionVm> Rows { get; } = new();

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _status = "";

    public SessionsViewModel(
        IFrameSessionStore frames, ISentinelService sentinel,
        AppSettingsStore settings, IUiDispatcher ui)
    {
        _frames = frames;
        _sentinel = sentinel;
        _settings = settings;
        _ui = ui;

        _restoringEnabled = true;
        try { Enabled = settings.Load().SentinelEnabled; } catch { Enabled = true; }
        _restoringEnabled = false;

        _frames.Updated += (_, _) => _ui.Post(Reload);
        Reload();
    }

    partial void OnEnabledChanged(bool value)
    {
        try { _sentinel.Enabled = value; } catch { }
        if (_restoringEnabled) return;
        try
        {
            // Load-mutate-save: several owners write this file.
            var s = _settings.Load();
            s.SentinelEnabled = value;
            _settings.Save(s);
        }
        catch { }
    }

    public void Reload()
    {
        try
        {
            Rows.Clear();
            foreach (var s in _frames.Load()) Rows.Add(new FrameSessionVm(s));
            Status = Rows.Count == 0
                ? "No sessions yet. Launch a game and its frame times get recorded automatically."
                : $"{Rows.Count} session(s), newest first.";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load sessions: {ex.Message}";
        }
    }
}
