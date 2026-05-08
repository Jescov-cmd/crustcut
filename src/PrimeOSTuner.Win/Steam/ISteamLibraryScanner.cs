namespace PrimeOSTuner.Win.Steam;

public interface ISteamLibraryScanner
{
    /// <summary>Returns all installed Steam games found across all configured Steam libraries on this machine. Returns empty list if Steam is not installed.</summary>
    IReadOnlyList<SteamGame> ScanInstalledGames();
}
