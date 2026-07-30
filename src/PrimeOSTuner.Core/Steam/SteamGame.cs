namespace PrimeOSTuner.Win.Steam;

public sealed record SteamGame(
    string AppId,
    string Name,
    string InstallDir,
    string LibraryPath,
    string? PrimaryExecutablePath);
