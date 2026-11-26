using PrimeOSTuner.Core.Performance;

namespace Crustcut.Presentation;

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
    public string AvgFpsDisplay          => $"{_model.Stats.AvgFps:F0} FPS avg";
    public string OnePctLowDisplay       => $"1% low: {_model.Stats.OnePctLowFps:F0} FPS";
    public string ZeroPointOnePctDisplay => $"0.1% low: {_model.Stats.ZeroPointOnePctLowFps:F0} FPS";

    // Bare numbers, for table columns that already carry a header. The *Display variants
    // repeat their own label, which collides when they sit under one.
    public string AvgFpsValue      => $"{_model.Stats.AvgFps:F0}";
    public string OnePctLowValue   => $"{_model.Stats.OnePctLowFps:F0}";

    /// <summary>
    /// Fewer frames than seconds played means the recorder wasn't watching the game — it
    /// attached to the wrong process (every pre-fix Minecraft session: 16 frames in 2½
    /// minutes). Those numbers are noise, and the row says so instead of presenting them.
    /// </summary>
    public bool IsUnreliable => _model.Stats.SampleCount < _model.Duration.TotalSeconds;

    /// <summary>The line under the numbers: why it stuttered, or why not to trust it.</summary>
    public string Verdict => IsUnreliable
        ? "Recorded before the capture fix — these numbers aren't from the game. New sessions are accurate."
        : _model.Stutter?.Verdict ?? "";

    public bool HasVerdict => Verdict.Length > 0;
}
