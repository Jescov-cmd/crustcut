namespace PrimeOSTuner.Core.Memory;

public sealed record PriorityRule(
    string ExePath,                  // canonical full path; case-insensitive comparison
    string DisplayName,              // friendly, user-editable
    PriorityLevel Priority,
    bool ProtectFromRamCleanup,
    bool GameBooster,                // run SafeRamCleaner ~2s after launch
    bool IsGame                      // tagged from GameLibrary at add time
);
