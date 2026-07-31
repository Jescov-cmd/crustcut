namespace PrimeOSTuner.Core.Memory;

/// <summary>
/// Windows standby-cache access (ISLC-style). The standby list is cached file data that
/// normally SPEEDS THE SYSTEM UP — purging it is only ever useful when free memory is
/// nearly gone and the cache is huge, where Windows reclaiming pages on demand causes
/// in-game stutter. Callers must gate on both conditions; never purge habitually.
/// </summary>
public interface IStandbyListClient
{
    /// <summary>Bytes currently on the standby (cached-file) lists.</summary>
    long GetStandbyBytes();

    /// <summary>Bytes on the free/zeroed page lists — memory that is truly idle.</summary>
    long GetFreeBytes();

    /// <summary>Asks the memory manager to drop the standby lists. Needs admin. False on refusal.</summary>
    bool TryPurge();
}
