using System;

namespace PrimeOSTuner.Core.History;

public sealed record HistoryEntry(
    Guid Id,
    string TweakId,
    string DisplayName,
    DateTime AppliedAtUtc,
    string? UndoData,
    bool Reverted);
