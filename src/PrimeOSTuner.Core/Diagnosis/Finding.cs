namespace PrimeOSTuner.Core.Diagnosis;

public enum FindingSeverity { Passed, Warning, Problem }

/// <summary>One result line of an on-demand diagnosis scan.</summary>
public sealed record Finding(
    string Id,
    FindingSeverity Severity,
    string Title,
    string Detail,
    string? FixSuggestion = null,
    string? NavTarget = null);   // nav Tag, e.g. "Optimize" — UI may render a jump link
