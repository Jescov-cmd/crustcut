using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Fans;

namespace Crustcut.Presentation;

public sealed record FanRowVm(string Name, string Rpm, string Duty);

/// <summary>
/// Fans page. The service owns the control loop and all safety; this is a remote control
/// plus live gauges. Refresh() is driven by the view on a 2s timer while visible.
/// </summary>
public partial class FansViewModel : ObservableObject
{
    private readonly IFanControlService? _service;

    [ObservableProperty] private bool _isSupported;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isSilent;
    [ObservableProperty] private bool _isBalanced;
    [ObservableProperty] private bool _isPerformance;
    [ObservableProperty] private string _tempText = "—";
    [ObservableProperty] private string _dutyText = "—";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _conflictSuspected;

    public ObservableCollection<FanRowVm> Fans { get; } = new();

    public FansViewModel(IFanControlService? service = null)
    {
        _service = service;
        IsSupported = service?.IsSupported == true;
        if (_service is null) return;
        Enabled = _service.Enabled;
        SetModeFlags(_service.Mode);
        Refresh();
    }

    public void Toggle(bool on)
    {
        if (_service is null) return;
        _service.Enabled = on;
        Enabled = on;
        Refresh();
    }

    public void SelectMode(FanMode mode)
    {
        if (_service is null) return;
        _service.Mode = mode;
        SetModeFlags(mode);
        Refresh();
    }

    private void SetModeFlags(FanMode mode)
    {
        IsSilent = mode == FanMode.Silent;
        IsBalanced = mode == FanMode.Balanced;
        IsPerformance = mode == FanMode.Performance;
    }

    /// <summary>Pulls the live picture from the service. Called by the view every ~2s.</summary>
    public void Refresh()
    {
        if (_service is null || !IsSupported) return;
        var s = _service.Status();

        TempText = s.TempC is double t ? $"{t:F0} °C" : "—";
        DutyText = s.Engaged && s.DutyPercent is double d ? $"{d:F0} %" : "auto (BIOS)";
        ConflictSuspected = s.ConflictSuspected;
        StatusText = !Enabled
            ? "Fans are under your motherboard's automatic control."
            : s.ConflictSuspected
                ? "Another program is overriding fan speeds — if you use SignalRGB, turn OFF its fan control (keep the RGB)."
                : s.Engaged
                    ? "Crustcut is controlling your fans."
                    : "Starting…";

        Fans.Clear();
        foreach (var f in s.Fans)
            Fans.Add(new FanRowVm(
                f.Name,
                f.Rpm is double r ? $"{r:F0} RPM" : "—",
                f.DutyPercent is double p ? $"{p:F0} %" : "—"));
    }
}
