namespace PrimeOSTuner.Core.Memory;

public enum PriorityLevel
{
    Normal,
    AboveNormal,
    High,
    BelowNormal
    // Realtime intentionally omitted — can starve OS processes.
}
