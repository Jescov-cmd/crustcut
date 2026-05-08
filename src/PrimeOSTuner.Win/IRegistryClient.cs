using Microsoft.Win32;

namespace PrimeOSTuner.Win;

public sealed record RegistryBackup(RegistryHive Hive, string SubKey, string ValueName, string? PreviousValue);

public interface IRegistryClient
{
    string? ReadString(RegistryHive hive, string subKey, string valueName);
    RegistryBackup WriteString(RegistryHive hive, string subKey, string valueName, string newValue);
    void RestoreFromBackup(RegistryBackup backup);
}
