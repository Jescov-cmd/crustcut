namespace PrimeOSTuner.Win;

public interface IRestorePointClient
{
    bool IsAvailable();
    bool TryCreate(string description, out string? error);
}
