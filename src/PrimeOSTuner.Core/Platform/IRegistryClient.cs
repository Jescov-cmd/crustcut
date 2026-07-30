using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed record RegistryBackup(
    RegistryHive Hive,
    string SubKey,
    string ValueName,
    string? PreviousString,        // null if value didn't exist OR previous was DWORD
    int? PreviousDword = null,     // null if previous was string OR didn't exist
    RegistryValueKind PreviousKind = RegistryValueKind.Unknown
);

public interface IRegistryClient
{
    string? ReadString(RegistryHive hive, string subKey, string valueName);
    int? ReadDword(RegistryHive hive, string subKey, string valueName);
    RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue);
    RegistryBackup WriteDword(RegistryHive hive, string subKey, string valueName, int newValue);
    void RestoreFromBackup(RegistryBackup backup);
}
