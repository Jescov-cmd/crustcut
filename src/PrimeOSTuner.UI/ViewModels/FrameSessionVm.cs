using PrimeOSTuner.Core.Performance;

namespace PrimeOSTuner.UI.ViewModels;

public sealed class FrameSessionVm
{
    private readonly FrameSession _model;

    public FrameSessionVm(FrameSession model) { _model = model; }

    public string GameName => _model.GameName;
    public string StartedAtDisplay => _model.StartedAt.ToLocalTime().ToString("MMM d, h:mm tt");
    public string DurationDisplay => _model.Duration switch
    {
        var d when d.TotalHours >= 1 => $"{(int)d.TotalHours}h {d.Minutes}m",
        var d when d.TotalMinutes >= 1 => $"{(int)d.TotalMinutes} min",
        _ => $"{(int)_model.Duration.TotalSeconds} sec",
    };
    public string AvgFpsDisplay        => $"{_model.Stats.AvgFps:F0} FPS avg";
    public string OnePctLowDisplay     => $"1% low: {_model.Stats.OnePctLowFps:F0} FPS";
    public string ZeroPointOnePctDisplay => $"0.1% low: {_model.Stats.ZeroPointOnePctLowFps:F0} FPS";
}
