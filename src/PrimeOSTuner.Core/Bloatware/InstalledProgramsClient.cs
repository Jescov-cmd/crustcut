using Microsoft.Win32;

namespace PrimeOSTuner.Core.Bloatware;

public interface IInstalledProgramsClient
{
    IReadOnlyList<DesktopProgram> ListInstalled();
}

/// <summary>Enumerates the three standard Uninstall registry roots (64-bit, 32-bit, per-user).</summary>
public sealed class InstalledProgramsClient : IInstalledProgramsClient
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow64Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<DesktopProgram> ListInstalled()
    {
        var result = new List<DesktopProgram>();
        Collect(RegistryHive.LocalMachine, UninstallPath, result);
        Collect(RegistryHive.LocalMachine, Wow64Path, result);
        Collect(RegistryHive.CurrentUser, UninstallPath, result);
        return result;
    }

    private static void Collect(RegistryHive hive, string path, List<DesktopProgram> into)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = root.OpenSubKey(path);
            if (key is null) return;
            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var app = key.OpenSubKey(sub);
                    if (app is null) continue;
                    if (app.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    var name = app.GetValue("DisplayName") as string;
                    var uninstall = app.GetValue("UninstallString") as string;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uninstall)) continue;
                    into.Add(new DesktopProgram(
                        name,
                        app.GetValue("Publisher") as string,
                        uninstall,
                        app.GetValue("QuietUninstallString") as string,
                        app.GetValue("InstallLocation") as string));
                }
                catch { /* one unreadable entry must not kill the scan */ }
            }
        }
        catch { /* missing hive/path — fine */ }
    }
}
