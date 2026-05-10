namespace PrimeOSTuner.Core.Tweaks;

public interface ICategorizedTweak
{
    string Category { get; }
    string? RiskNote { get; }
}
