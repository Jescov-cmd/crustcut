namespace PrimeOSTuner.Core.Performance;

public interface IFrameSessionStore
{
    /// <summary>Sessions ordered newest-first.</summary>
    IReadOnlyList<FrameSession> Load();

    /// <summary>Append a session. Older entries past the cap are dropped on save.</summary>
    void Save(FrameSession session);

    /// <summary>Raised after Save() succeeds.</summary>
    event EventHandler? Updated;
}
