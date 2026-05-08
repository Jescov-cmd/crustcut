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

    public RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue)
    {
        var previous = ReadString(hive, subKey, valueName);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Cannot open or create {subKey}");
        key.SetValue(valueName, newValue, RegistryValueKind.String);
        return new RegistryBackup(hive, subKey, valueName, previous);
    }

    public void RestoreFromBackup(RegistryBackup backup)
    {
        using var baseKey = RegistryKey.OpenBaseKey(backup.Hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(backup.SubKey, writable: true);
        if (key is null) return;

        if (backup.PreviousValue is null)
            key.DeleteValue(backup.ValueName, throwOnMissingValue: false);
        else
            key.SetValue(backup.ValueName, backup.PreviousValue, RegistryValueKind.String);
    }
}
