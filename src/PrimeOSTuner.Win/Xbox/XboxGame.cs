namespace PrimeOSTuner.Win.Xbox;

/// <summary>An installed Xbox app / Game Pass (PC) game discovered on disk.</summary>
public sealed record XboxGame(
    string Id,                      // stable id, e.g. "xbox.Fortnite"
    string Name,                    // folder/display name
    string InstallDir,              // ...\XboxGames\<Name>
    string? PrimaryExecutablePath); // resolved game exe, or null if it couldn't be determined
