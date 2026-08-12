namespace PrimeOSTuner.Core.Memory;

public sealed record PriorityRule(
    string ExePath,                  // canonical full path; case-insensitive comparison
    string DisplayName,              // friendly, user-editable
    PriorityLevel Priority,
    bool ProtectFromRamCleanup,
    bool GameBooster,                // run SafeRamCleaner ~2s after launch
    bool IsGame,                     // tagged from GameLibrary at add time
    int? MemoryLimitMb = null,       // hard working-set cap; null = unlimited. Optional with
                                     // a default so rule files from before the field still load.
    bool LimitAutoAssigned = false   // true when Crustcut chose the cap, false when the user
                                     // picked it. Only auto caps may be revised automatically.
);
