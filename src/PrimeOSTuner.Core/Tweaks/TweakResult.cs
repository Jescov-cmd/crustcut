namespace PrimeOSTuner.Core.Tweaks;

public sealed record TweakResult(bool Succeeded, string? UndoData, string? Error)
{
    public static TweakResult Success(string? undoData = null) => new(true, undoData, null);
    public static TweakResult Failure(string error) => new(false, null, error);
}
