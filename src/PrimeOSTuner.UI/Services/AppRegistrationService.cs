using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Two concerns folded into one service because they're both "make Windows
/// treat Crustcut as a real app":
///
/// <list type="bullet">
///   <item>
///   <b>Start Menu shortcut</b> — without one, Windows search has nothing to find
///   when the user types "Crustcut". We drop a .lnk into the per-user Start
///   Menu Programs folder pointing at the current exe.
///   </item>
///   <item>
///   <b>Start at boot</b> — the previous implementation wrote to <c>HKCU\Run</c>,
///   which cannot UAC-elevate at startup. Since the app's manifest requires
///   admin, Windows silently blocks the launch. Task Scheduler with
///   <c>/rl highest</c> elevates without prompting, so the app actually runs.
///   </item>
/// </list>
///
/// Both operations are idempotent — call them as many times as you like.
/// </summary>
public sealed class AppRegistrationService
{
    private const string TaskName = "Crustcut Autostart";
    private const string ShortcutName = "Crustcut.lnk";

    private static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs", ShortcutName);

    /// <summary>
    /// Creates the Start Menu shortcut if missing, or updates its target if the
    /// exe location changed (e.g. the user re-extracted the zip somewhere else).
    /// Silent on failure — a missing shortcut is not worth crashing the app over.
    /// </summary>
    public void EnsureStartMenuShortcut()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;

            var lnk = StartMenuShortcutPath;
            Directory.CreateDirectory(Path.GetDirectoryName(lnk)!);

            // If the shortcut already exists AND points at this exe, skip.
            if (File.Exists(lnk) && TryReadShortcutTarget(lnk) is string existingTarget &&
                string.Equals(existingTarget, exe, StringComparison.OrdinalIgnoreCase))
                return;

            CreateShortcut(lnk, exe);
        }
        catch { /* shortcut is nice-to-have, not critical */ }
    }

    /// <summary>
    /// Enables or disables start-at-boot via a Task Scheduler task that runs
    /// the exe with elevated privileges on user logon. Returns true if the
    /// requested state was achieved.
    /// </summary>
    public bool SetStartAtBoot(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;

                // /sc onlogon — fire when the current user logs in
                // /rl highest — elevate without UAC prompt (task is created admin once)
                // /f — overwrite if already exists
                // /it — interactive, so the window can show
                return RunSchTasks($"/create /tn \"{TaskName}\" /tr \"\\\"{exe}\\\"\" /sc onlogon /rl highest /it /f");
            }
            return RunSchTasks($"/delete /tn \"{TaskName}\" /f");
        }
        catch { return false; }
    }

    public bool IsStartAtBootEnabled()
    {
        try { return RunSchTasks($"/query /tn \"{TaskName}\"", suppressOutput: true); }
        catch { return false; }
    }

    private static bool RunSchTasks(string args, bool suppressOutput = false)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = suppressOutput,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        p.WaitForExit(5000);
        return p.ExitCode == 0;
    }

    // ----------------------------------------------------------------------
    // Shortcut creation via IShellLinkW + IPersistFile.
    // Doing it inline avoids shelling out to PowerShell on every launch.
    // ----------------------------------------------------------------------

    private static void CreateShortcut(string lnkPath, string exePath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(exePath);
        link.SetIconLocation(exePath, 0);   // use the exe's embedded icon
        link.SetDescription("Crustcut — PC performance optimizer");
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);
        ((IPersistFile)link).Save(lnkPath, false);
    }

    private static string? TryReadShortcutTarget(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.ToString();
        }
        catch { return null; }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                     int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                             int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
