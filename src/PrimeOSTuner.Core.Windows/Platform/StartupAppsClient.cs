using Microsoft.Win32;
using PrimeOSTuner.Core.Platform;

namespace PrimeOSTuner.Core.Windows.Platform;

/// <summary>
/// Startup programs via the Run keys and the Startup folder, toggled through the
/// StartupApproved marks — the exact mechanism Task Manager's Startup tab uses. Nothing
/// is ever deleted: a disabled entry keeps its Run value/shortcut and only carries the
/// "don't launch this" mark, so Task Manager and Crustcut always agree and either can
/// re-enable what the other disabled.
/// </summary>
public sealed class StartupAppsClient : IStartupAppsClient
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    /// <summary>First byte 0x02 = enabled; 0x03 (or any odd value) = disabled. The rest
    /// of the 12 bytes is a disable timestamp Windows fills in; zeros are accepted.</summary>
    public static bool IsEnabledMark(byte[]? mark) => mark is null || mark.Length == 0 || (mark[0] & 0x01) == 0;

    public static byte[] MakeMark(bool enabled)
    {
        var bytes = new byte[12];
        bytes[0] = enabled ? (byte)0x02 : (byte)0x03;
        return bytes;
    }

    public IReadOnlyList<StartupApp> List()
    {
        var apps = new List<StartupApp>();
        CollectRunKey(Registry.CurrentUser, StartupSource.CurrentUser, apps);
        CollectRunKey(Registry.LocalMachine, StartupSource.AllUsers, apps);
        CollectStartupFolder(apps);
        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectRunKey(RegistryKey hive, StartupSource source, List<StartupApp> apps)
    {
        try
        {
            using var run = hive.OpenSubKey(RunKey);
            if (run is null) return;
            using var approved = hive.OpenSubKey(ApprovedRun);
            foreach (var name in run.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var cmd = run.GetValue(name)?.ToString() ?? "";
                var enabled = IsEnabledMark(approved?.GetValue(name) as byte[]);
                apps.Add(new StartupApp(name, cmd, source, enabled));
            }
        }
        catch { /* a hive we can't read contributes nothing */ }
    }

    private static void CollectStartupFolder(List<StartupApp> apps)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!Directory.Exists(dir)) return;
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedFolder);
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileName(f);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                var enabled = IsEnabledMark(approved?.GetValue(name) as byte[]);
                apps.Add(new StartupApp(name, f, StartupSource.StartupFolder, enabled));
            }
        }
        catch { }
    }

    public bool TrySetEnabled(StartupApp app, bool enabled)
    {
        try
        {
            var (hive, key) = app.Source switch
            {
                StartupSource.AllUsers => (Registry.LocalMachine, ApprovedRun),
                StartupSource.StartupFolder => (Registry.CurrentUser, ApprovedFolder),
                _ => (Registry.CurrentUser, ApprovedRun),
            };
            using var approved = hive.CreateSubKey(key);
            approved.SetValue(app.Name, MakeMark(enabled), RegistryValueKind.Binary);
            return true;
        }
        catch
        {
            return false;   // access denied (unelevated + AllUsers) or hive trouble
        }
    }
}
