namespace PrimeOSTuner.Core.Tweaks;

public sealed record TweakResult(bool Succeeded, string? UndoData, string? Error, string? Message = null)
{
    public static TweakResult Success(string? undoData = null, string? message = null) => new(true, undoData, null, message);
    public static TweakResult Failure(string error) => new(false, null, error, null);
}
