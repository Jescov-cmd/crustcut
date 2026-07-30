using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Removes outdated driver packages from the Windows DriverStore. After years of GPU/printer/USB
/// updates, multiple versions of the same .inf accumulate — this finds the duplicates and keeps
/// only the newest version of each.
///
/// Implementation: shells out to pnputil. Marked destructive so one-click never runs it without
/// explicit user opt-in. Requires admin (pnputil /delete-driver needs elevation).
/// </summary>
public sealed class DriverStoreCleanupTweak : ITweak
{
    public string Id => "core.driver-store-cleanup";
    public string DisplayName => "Clean old driver packages";
    public string Description => "Removes superseded drivers from the DriverStore.";
    public bool RequiresElevation => true;
    public bool IsDestructive => true;
    public bool RequiresReboot => true;

    public async Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var groups = await EnumDuplicateGroupsAsync(ct);
            return groups.Any(g => g.Count > 1) ? TweakState.NotApplied : TweakState.Applied;
        }
        catch
        {
            return TweakState.Unknown;
        }
    }

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var groups = await EnumDuplicateGroupsAsync(ct);
            // Keep the newest version of each duplicated original-name group; delete the rest.
            var toDelete = groups
                .Where(g => g.Count > 1)
                .SelectMany(g => g.OrderByDescending(d => d.Date)
                                  .ThenByDescending(d => d.Version, VersionStringComparer.Instance)
                                  .Skip(1))
                .ToList();

            if (toDelete.Count == 0)
                return TweakResult.Success(message: "No old driver packages to remove.");

            int deleted = 0, failed = 0;
            for (int i = 0; i < toDelete.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((int)((i + 1) / (double)toDelete.Count * 100));
                if (await DeleteDriverAsync(toDelete[i].PublishedName, ct)) deleted++;
                else failed++;
            }

            var undoData = JsonSerializer.Serialize(toDelete.Select(d => d.PublishedName).ToArray());
            var msg = failed == 0
                ? $"Removed {deleted} old driver package(s)."
                : $"Removed {deleted}, {failed} could not be removed (may still be in use).";
            return TweakResult.Success(undoData: undoData, message: msg);
        }
        catch (Exception ex)
        {
            return TweakResult.Failure(ex.Message);
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Driver removals can't be reverted from here. Reinstall the driver from its source."));

    public async Task<string> PreviewAsync(CancellationToken ct = default)
    {
        try
        {
            var groups = await EnumDuplicateGroupsAsync(ct);
            var dupCount = groups.Where(g => g.Count > 1).Sum(g => g.Count - 1);
            return dupCount == 0
                ? "No duplicate driver packages found."
                : $"Will remove {dupCount} superseded driver package(s).";
        }
        catch (Exception ex)
        {
            return $"Could not enumerate driver packages: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs <c>pnputil /enum-drivers</c> and parses out one record per oem*.inf, grouped by
    /// the original .inf name. Each group with more than one entry has older versions to clean.
    /// </summary>
    private static async Task<List<List<DriverPackage>>> EnumDuplicateGroupsAsync(CancellationToken ct)
    {
        var output = await RunCaptureAsync("pnputil.exe", "/enum-drivers", ct);
        var packages = ParsePnputilEnum(output);
        return packages
            .GroupBy(p => p.OriginalName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.ToList())
            .ToList();
    }

    private static async Task<bool> DeleteDriverAsync(string publishedName, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("pnputil.exe", $"/delete-driver \"{publishedName}\" /force")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<string> RunCaptureAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException($"Could not start {exe}.");
        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return output;
    }

    /// <summary>
    /// pnputil emits records like:
    ///   Published Name:     oem42.inf
    ///   Original Name:      nv_dispi.inf
    ///   Provider Name:      NVIDIA
    ///   Class Name:         Display adapters
    ///   Driver Version:     04/02/2024 31.0.15.4651
    ///   Signer Name:        Microsoft Windows Hardware Compatibility Publisher
    /// Records are separated by blank lines.
    /// </summary>
    private static List<DriverPackage> ParsePnputilEnum(string output)
    {
        var result = new List<DriverPackage>();
        DriverPackage? current = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                if (current is { } finished && finished.IsValid()) result.Add(finished);
                current = null;
                continue;
            }

            var sep = line.IndexOf(':');
            if (sep <= 0) continue;
            var key = line.Substring(0, sep).Trim();
            var val = line.Substring(sep + 1).Trim();

            current ??= new DriverPackage();
            switch (key)
            {
                case "Published Name":  current.PublishedName = val; break;
                case "Original Name":   current.OriginalName  = val; break;
                case "Provider Name":   current.Provider      = val; break;
                case "Class Name":      current.ClassName     = val; break;
                case "Driver Version":  ParseVersion(val, current); break;
            }
        }
        if (current is { } last && last.IsValid()) result.Add(last);
        return result;
    }

    private static void ParseVersion(string val, DriverPackage pkg)
    {
        // "04/02/2024 31.0.15.4651" -> Date + Version
        var parts = val.Split(' ', 2);
        if (parts.Length == 2)
        {
            if (DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                pkg.Date = d;
            pkg.Version = parts[1];
        }
        else
        {
            pkg.Version = val;
        }
    }

    private sealed class DriverPackage
    {
        public string PublishedName { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public string Provider { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Version { get; set; } = "";
        public DateTime Date { get; set; }
        public bool IsValid() => !string.IsNullOrEmpty(PublishedName) && !string.IsNullOrEmpty(OriginalName);
    }

    /// <summary>Compares dotted version strings. "31.0.15.4651" &gt; "30.9.99.9999".</summary>
    private sealed class VersionStringComparer : IComparer<string>
    {
        public static readonly VersionStringComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            x ??= ""; y ??= "";
            if (Version.TryParse(x, out var vx) && Version.TryParse(y, out var vy))
                return vx.CompareTo(vy);
            return string.CompareOrdinal(x, y);
        }
    }
}
