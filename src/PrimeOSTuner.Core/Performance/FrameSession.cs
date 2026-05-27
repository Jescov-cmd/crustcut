namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// A single completed game session's frame-time recording, persisted across
/// app restarts.
/// </summary>
public sealed record FrameSession(
    string GameId,
    string GameName,
    DateTime StartedAt,
    TimeSpan Duration,
    FrameSessionStats Stats);
