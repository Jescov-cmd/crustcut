using System.Collections.Generic;
using System.Linq;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.Services;

/// <summary>
/// Reads protected EXE paths from <see cref="PriorityRuleStore"/>. Used by the RAM cleaner
/// to skip processes the user has marked as "protect from RAM cleanups".
/// </summary>
public sealed class StoreBackedProtectList : IRamCleanerProtectList
{
    private readonly PriorityRuleStore _store;

    public StoreBackedProtectList(PriorityRuleStore store)
    {
        _store = store;
    }

    public IReadOnlyList<string> Get()
    {
        // Intentionally synchronous — IRamCleanerProtectList is called inline by the cleaner.
        // Acceptable: the file is small; LoadAsync is fast.
        return _store.LoadAsync().GetAwaiter().GetResult()
            .Where(r => r.ProtectFromRamCleanup)
            .Select(r => r.ExePath)
            .ToList();
    }
}
