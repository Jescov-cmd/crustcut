using System.Text.Json;

namespace PrimeOSTuner.Core.Performance;

public sealed class FrameSessionStore : IFrameSessionStore
{
    private const int MaxEntries = 50;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _path;
    private readonly object _gate = new();

    public event EventHandler? Updated;

    public FrameSessionStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "frame-sessions.json");

    public IReadOnlyList<FrameSession> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return Array.Empty<FrameSession>();
            try
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<List<FrameSession>>(json) ?? new();
                return list;
            }
            catch
            {
                return Array.Empty<FrameSession>();
            }
        }
    }

    public void Save(FrameSession session)
    {
        lock (_gate)
        {
            var existing = LoadInternalLocked();
            existing.Insert(0, session);
            while (existing.Count > MaxEntries) existing.RemoveAt(existing.Count - 1);
            WriteAtomicLocked(existing);
        }
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private List<FrameSession> LoadInternalLocked()
    {
        if (!File.Exists(_path)) return new List<FrameSession>();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<FrameSession>>(json) ?? new();
        }
        catch
        {
            return new List<FrameSession>();
        }
    }

    private void WriteAtomicLocked(List<FrameSession> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }
}
