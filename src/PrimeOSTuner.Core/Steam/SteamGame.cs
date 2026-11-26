namespace PrimeOSTuner.Win.Steam;

public sealed record SteamGame(
    string AppId,
    string Name,
    string InstallDir,
    string LibraryPath,
    string? PrimaryExecutablePath,
    IReadOnlyList<string>? Executables = null);   // every plausible game exe in the install folder
