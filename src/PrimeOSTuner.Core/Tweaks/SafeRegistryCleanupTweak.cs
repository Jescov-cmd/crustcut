using System.Text.Json;
using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Conservative registry cleanup. Only touches two well-defined classes of garbage:
///
///   1. Dangling startup entries — values under HKCU\...\Run and HKLM\...\Run whose target
///      .exe no longer exists. These do nothing useful and slow boot slightly.
///   2. Broken file associations — HKCR\.{ext} ProgIDs whose
///      shell\open\command points at a missing .exe.
///
/// Skipped categories on purpose: "unused" CLSIDs, MUI cache, AppCompat layers, etc. — those
/// are routine sources of breakage in third-party "registry cleaners" and offer near-zero
/// real-world benefit. Marked destructive — opt-in only.
/// </summary>
public sealed class SafeRegistryCleanupTweak : ITweak
{
    public string Id => "core.registry-cleanup-safe";
    public string DisplayName => "Clean broken registry entries";
    public string Description => "Removes dead startup entries and broken file associations.";
    public bool RequiresElevation => true; // HKLM Run requires admin
    public bool IsDestructive => true;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var found = ScanBrokenEntries().Count;
            return Task.FromResult(found > 0 ? TweakState.NotApplied : TweakState.Applied);
        }
        catch
        {
            return Task.FromResult(TweakState.Unknown);
        }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var broken = ScanBrokenEntries();
            if (broken.Count == 0)
                return Task.FromResult(TweakResult.Success(message: "No broken entries found."));

            var deleted = new List<BrokenEntry>();
            var failed = 0;
            for (int i = 0; i < broken.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((int)((i + 1) / (double)broken.Count * 100));
                if (TryDelete(broken[i]))
                    deleted.Add(broken[i]);
                else
                    failed++;
            }

            var undo = JsonSerializer.Serialize(deleted);
            var msg = failed == 0
                ? $"Removed {deleted.Count} broken entries."
                : $"Removed {deleted.Count}, {failed} failed (likely permission-denied).";
            return Task.FromResult(TweakResult.Success(undoData: undo, message: msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<List<BrokenEntry>>(undoData) ?? new();
            int restored = 0;
            foreach (var e in entries)
            {
                if (TryRestore(e)) restored++;
            }
            return Task.FromResult(TweakResult.Success(message: $"Restored {restored} entries."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        try
        {
            var broken = ScanBrokenEntries();
            return Task.FromResult($"Found {broken.Count} broken entries " +
                                   $"({broken.Count(b => b.Kind == EntryKind.Startup)} startup, " +
                                   $"{broken.Count(b => b.Kind == EntryKind.FileAssoc)} file association).");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Could not scan: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static List<BrokenEntry> ScanBrokenEntries()
    {
        var results = new List<BrokenEntry>();
        ScanRunKey(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run", results);
        ScanRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", results);
        ScanFileAssociations(results);
        return results;
    }

    private static void ScanRunKey(RegistryKey root, string subKey, List<BrokenEntry> sink)
    {
        try
        {
            using var key = root.OpenSubKey(subKey);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                if (key.GetValue(name) is not string raw || string.IsNullOrWhiteSpace(raw)) continue;
                var path = ExtractExePath(raw);
                if (path is not null && !File.Exists(path))
                {
                    sink.Add(new BrokenEntry
                    {
                        Kind = EntryKind.Startup,
                        Hive = root == Registry.CurrentUser ? "HKCU" : "HKLM",
                        SubKey = subKey,
                        ValueName = name,
                        OriginalValue = raw,
                    });
                }
            }
        }
        catch { /* permission-denied or missing key — skip */ }
    }

    private static void ScanFileAssociations(List<BrokenEntry> sink)
    {
        // Walk HKCR\.{ext} -> default value = ProgID -> HKCR\{ProgID}\shell\open\command
        try
        {
            using var classesRoot = Registry.ClassesRoot;
            foreach (var name in classesRoot.GetSubKeyNames())
            {
                if (!name.StartsWith('.')) continue;
                using var ext = classesRoot.OpenSubKey(name);
                if (ext?.GetValue(null) is not string progId || string.IsNullOrWhiteSpace(progId)) continue;
                using var cmd = classesRoot.OpenSubKey($@"{progId}\shell\open\command");
                if (cmd?.GetValue(null) is not string raw) continue;
                var path = ExtractExePath(raw);
                if (path is null || File.Exists(path)) continue;

                sink.Add(new BrokenEntry
                {
                    Kind = EntryKind.FileAssoc,
                    Hive = "HKCR",
                    SubKey = name, // keep just the extension; we only delete that level
                    ValueName = "(default)",
                    OriginalValue = progId,
                });
            }
        }
        catch { }
    }

    /// <summary>Extracts the .exe path from a Run value or shell\open\command. Handles quoted paths.</summary>
    private static string? ExtractExePath(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return null;
        if (s[0] == '"')
        {
            var end = s.IndexOf('"', 1);
            return end > 1 ? s.Substring(1, end - 1) : null;
        }
        var space = s.IndexOf(' ');
        return space < 0 ? s : s.Substring(0, space);
    }

    private static bool TryDelete(BrokenEntry e)
    {
        try
        {
            switch (e.Kind)
            {
                case EntryKind.Startup:
                    var root = e.Hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (var key = root.OpenSubKey(e.SubKey, writable: true))
                    {
                        if (key is null) return false;
                        if (key.GetValue(e.ValueName) is null) return false;
                        key.DeleteValue(e.ValueName, throwOnMissingValue: false);
                    }
                    return true;
                case EntryKind.FileAssoc:
                    // Drop the extension entry — Windows will re-prompt if user opens that file type.
                    Registry.ClassesRoot.DeleteSubKeyTree(e.SubKey, throwOnMissingSubKey: false);
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private static bool TryRestore(BrokenEntry e)
    {
        try
        {
            switch (e.Kind)
            {
                case EntryKind.Startup:
                    var root = e.Hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using (var key = root.CreateSubKey(e.SubKey))
                    {
                        key.SetValue(e.ValueName, e.OriginalValue, RegistryValueKind.String);
                    }
                    return true;
                case EntryKind.FileAssoc:
                    using (var ext = Registry.ClassesRoot.CreateSubKey(e.SubKey))
                    {
                        ext.SetValue(null, e.OriginalValue, RegistryValueKind.String);
                    }
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private enum EntryKind { Startup, FileAssoc }

    private sealed class BrokenEntry
    {
        public EntryKind Kind { get; set; }
        public string Hive { get; set; } = "";
        public string SubKey { get; set; } = "";
        public string ValueName { get; set; } = "";
        public string OriginalValue { get; set; } = "";
    }
}
