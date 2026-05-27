namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Starts and stops the PresentMon subprocess. Implementations swallow
/// all failures and return null on Start so the recording service can
/// degrade silently — frame-time capture must never break a game launch.
/// </summary>
public interface IPresentMonRunner
{
    /// <summary>
    /// Spawn PresentMon targeting the given pid. Returns the CSV path it
    /// is writing to, or null if PresentMon could not be started.
    /// </summary>
    Task<string?> StartAsync(int gamePid, string outputCsvPath, CancellationToken ct = default);

    /// <summary>Stop the in-flight PresentMon process, if any. Safe to call when nothing is running.</summary>
    Task StopAsync(CancellationToken ct = default);
}
