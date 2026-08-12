using System.Text.Json;

namespace PrimeOSTuner.Core.Fans;

/// <summary>
/// Per-fan facts learned from THIS machine, not assumed.
/// <paramref name="MinDutyPercent"/> is the lowest duty at which the fan still spins
/// (found by calibration; 3-pin DC fans and pumps stall much higher than PWM fans).
/// <paramref name="RpmScale"/> corrects tachometer pulse counts: most fans send 2 pulses
/// per revolution, but 1- and 4-pulse fans exist and read half/double. No software can
/// detect this, so the user corrects it against their BIOS reading.
/// </summary>
public sealed record FanTuning(string Name, double MinDutyPercent, double RpmScale = 1.0);

public sealed class FanTuningStore
{
    private readonly string _path;
    private Dictionary<string, FanTuning> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "fan-tuning.json");

    public FanTuningStore(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>True once calibration has run at least once on this machine.</summary>
    public bool HasCalibration => _byName.Count > 0;

    public FanTuning? For(string fanName) =>
        _byName.TryGetValue(fanName, out var t) ? t : null;

    /// <summary>Safe floor for one fan: its measured minimum, or the conservative
    /// uncalibrated default. Never below the global hard floor.</summary>
    public double MinDutyFor(string fanName) =>
        Math.Max(FanPolicy.HardMinDutyPercent,
                 For(fanName)?.MinDutyPercent ?? FanPolicy.UncalibratedMinDutyPercent);

    public double RpmScaleFor(string fanName) => For(fanName)?.RpmScale ?? 1.0;

    public void Set(FanTuning tuning)
    {
        _byName[tuning.Name] = tuning;
        Save();
    }

    public void SetRpmScale(string fanName, double scale)
    {
        var existing = For(fanName);
        Set(existing is null
            ? new FanTuning(fanName, FanPolicy.UncalibratedMinDutyPercent, scale)
            : existing with { RpmScale = scale });
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var list = JsonSerializer.Deserialize<List<FanTuning>>(File.ReadAllText(_path));
            if (list is null) return;
            _byName = list.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _byName = new(StringComparer.OrdinalIgnoreCase);   // corrupt file: recalibrate
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_byName.Values.ToList()));
            File.Move(tmp, _path, overwrite: true);
        }
        catch { /* tuning is an optimization, never fatal */ }
    }
}
