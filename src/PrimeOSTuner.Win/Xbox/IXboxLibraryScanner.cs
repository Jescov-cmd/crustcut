namespace PrimeOSTuner.Win.Xbox;

public interface IXboxLibraryScanner
{
    /// <summary>
    /// Returns all installed Xbox app / Game Pass (PC) games found in the <c>XboxGames</c>
    /// folder on every ready drive. Empty if the Xbox app isn't installed or no games exist.
    /// </summary>
    IReadOnlyList<XboxGame> ScanInstalledGames();
}
