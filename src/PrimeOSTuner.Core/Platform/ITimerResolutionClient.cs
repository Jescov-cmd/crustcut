namespace PrimeOSTuner.Win;

public interface ITimerResolutionClient
{
    /// <summary>Set timer resolution in 100-ns units (5000 = 0.5 ms). Returns the actual resolution Windows granted.</summary>
    uint SetResolution(uint desiredHundredNs);
    /// <summary>Release a previously requested resolution.</summary>
    void ClearResolution(uint desiredHundredNs);
    /// <summary>Read the current effective system timer resolution in 100-ns units.</summary>
    uint GetCurrentResolution();
}
