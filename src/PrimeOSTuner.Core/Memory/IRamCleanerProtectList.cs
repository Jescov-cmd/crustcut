namespace PrimeOSTuner.Core.Memory;

public interface IRamCleanerProtectList
{
    /// <summary>EXE paths whose processes should NOT be trimmed by the RAM cleaner.</summary>
    IReadOnlyList<string> Get();
}

/// <summary>Default no-op implementation — used when nothing is registered.</summary>
public sealed class EmptyRamCleanerProtectList : IRamCleanerProtectList
{
    public IReadOnlyList<string> Get() => Array.Empty<string>();
}
