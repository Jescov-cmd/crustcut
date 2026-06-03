using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed class RegistryClient : IRegistryClient
{
    public string? ReadString(RegistryHive hive, string subKey, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey);
        return key?.GetValue(valueName) as string;
    }

    public int? ReadDword(RegistryHive hive, string subKey, string valueName)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = root.OpenSubKey(subKey);
        if (key is null) return null;
        var v = key.GetValue(valueName);
        if (v is null) return null;
        return v switch
        {
            int i => i,
            long l => unchecked((int)l),
            _ => int.TryParse(v.ToString(), out var parsed) ? parsed : null
        };
    }

    public RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Could not open or create {hive}\\{subKey}");
        var rawPrev = key.GetValue(valueName);
        var prevKind = rawPrev is null ? RegistryValueKind.Unknown : key.GetValueKind(valueName);
        var prev = rawPrev?.ToString();
        key.SetValue(valueName, newValue, RegistryValueKind.String);
        return new RegistryBackup(hive, subKey, valueName, prev, null, prevKind);
    }

    public RegistryBackup WriteDword(RegistryHive hive, string subKey, string valueName, int newValue)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = root.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Could not open or create {hive}\\{subKey}");
        var prev = key.GetValue(valueName);
        var prevKind = prev is null ? RegistryValueKind.Unknown : key.GetValueKind(valueName);
        string? prevString = null;
        int? prevDword = null;
        if (prev is int i) prevDword = i;
        else if (prev is long l) prevDword = unchecked((int)l);  // QWord truncation matches ReadDword
        else if (prev is not null) prevString = prev.ToString();

        // Idempotent: only write when the value isn't already exactly what we want. Some
        // values are tamper-protected by Windows (e.g. the Win11 taskbar 'TaskbarDa'), where
        // SetValue throws "Attempted to perform an unauthorized operation" EVEN when re-writing
        // the identical value — which made re-applying an already-applied tweak fail with a
        // misleading "needs administrator" error. Skipping the no-op write avoids that and is
        // harmless everywhere else.
        if (prevDword != newValue || prevKind != RegistryValueKind.DWord)
            key.SetValue(valueName, newValue, RegistryValueKind.DWord);

        return new RegistryBackup(hive, subKey, valueName, prevString, prevDword, prevKind);
    }

    public void RestoreFromBackup(RegistryBackup backup)
    {
        using var root = RegistryKey.OpenBaseKey(backup.Hive, RegistryView.Default);
        using var key = root.CreateSubKey(backup.SubKey, writable: true);
        if (key is null) return;

        if (backup.PreviousKind == RegistryValueKind.Unknown
            && backup.PreviousString is null
            && backup.PreviousDword is null)
        {
            // Value didn't exist before — delete it.
            key.DeleteValue(backup.ValueName, throwOnMissingValue: false);
            return;
        }

        if (backup.PreviousDword is int d)
        {
            key.SetValue(backup.ValueName, d, RegistryValueKind.DWord);
        }
        else if (backup.PreviousString is string s)
        {
            var kind = backup.PreviousKind == RegistryValueKind.Unknown
                ? RegistryValueKind.String
                : backup.PreviousKind;
            key.SetValue(backup.ValueName, s, kind);
        }
    }
}
