namespace PrimeOSTuner.Core.Memory;

public sealed record ProcessStartedEvent(int Pid, string ProcessName);
public sealed record ProcessStoppedEvent(int Pid, string ProcessName);

public interface IProcessWatcher : IDisposable
{
    event EventHandler<ProcessStartedEvent>? ProcessStarted;
    event EventHandler<ProcessStoppedEvent>? ProcessStopped;
    void Start();
    void Stop();
}
