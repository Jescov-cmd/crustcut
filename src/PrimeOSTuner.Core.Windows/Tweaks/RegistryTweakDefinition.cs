using Microsoft.Win32;

namespace PrimeOSTuner.Core.Tweaks;

public sealed record RegistryTweakDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Category,                 // "fps" | "network" | "system" | "privacy" | "power"
    bool RequiresElevation,
    bool RequiresReboot,
    string Hive,                     // "LocalMachine" | "CurrentUser" | "ClassesRoot"
    string Key,
    string ValueName,
    string ValueKind,                // "DWord" | "String"
    string AppliedData,              // for DWord, parseable as int; for String, used as-is
    string? RiskNote                 // optional inline warning shown on the tile
)
{
    public RegistryHive ParsedHive => Hive switch
    {
        "LocalMachine" => RegistryHive.LocalMachine,
        "CurrentUser" => RegistryHive.CurrentUser,
        "ClassesRoot" => RegistryHive.ClassesRoot,
        _ => throw new InvalidOperationException($"Unsupported hive: {Hive}")
    };

    public RegistryValueKind ParsedKind => ValueKind switch
    {
        "DWord" => RegistryValueKind.DWord,
        "String" => RegistryValueKind.String,
        _ => throw new InvalidOperationException($"Unsupported value kind: {ValueKind}")
    };
}
