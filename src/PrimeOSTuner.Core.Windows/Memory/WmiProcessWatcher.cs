using System.Management;

namespace PrimeOSTuner.Core.Memory;

public sealed class WmiProcessWatcher : IProcessWatcher
{
    private ManagementEventWatcher? _start;
    private ManagementEventWatcher? _stop;
    private bool _disposed;

    public event EventHandler<ProcessStartedEvent>? ProcessStarted;

    // The stop trace is only subscribed to WMI once someone actually listens. Nothing in
    // the app does today, and an unused watcher still had every process exit on the
    // machine marshalled into Crustcut through WMI.
    private EventHandler<ProcessStoppedEvent>? _processStopped;
    public event EventHandler<ProcessStoppedEvent>? ProcessStopped
    {
        add { _processStopped += value; if (_start is not null) EnsureStopWatcher(); }
        remove { _processStopped -= value; }
    }

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WmiProcessWatcher));
        if (_start is not null) return;

        // Only the two fields we read: SELECT * marshalled the whole trace record (SIDs,
        // session, parent…) for every process launched on the machine.
        var watcher = new ManagementEventWatcher(
            new WqlEventQuery("SELECT ProcessID, ProcessName FROM Win32_ProcessStartTrace"));
        watcher.EventArrived += (_, args) =>
        {
            using var tracked = PrimeOSTuner.Core.Diagnostics.MemoryWatch.Track("process-start events");
            // Each event wraps a native COM object. Undisposed, it lived until a finalizer
            // ran — native memory that piled up with process churn and only fell at GC.
            using var ev = args.NewEvent;
            try
            {
                var pid = Convert.ToInt32(ev["ProcessID"]);
                var name = ev["ProcessName"]?.ToString() ?? "";
                ProcessStarted?.Invoke(this, new ProcessStartedEvent(pid, name));
            }
            catch { /* swallow — best effort */ }
        };
        try { watcher.Start(); }
        catch { watcher.Dispose(); throw; }   // leave _start null so a later Start() can retry
        _start = watcher;

        if (_processStopped is not null) EnsureStopWatcher();
    }

    private void EnsureStopWatcher()
    {
        if (_stop is not null || _disposed) return;
        _stop = new ManagementEventWatcher(new WqlEventQuery("SELECT ProcessID, ProcessName FROM Win32_ProcessStopTrace"));
        _stop.EventArrived += (_, args) =>
        {
            using var ev = args.NewEvent;
            try
            {
                var pid = Convert.ToInt32(ev["ProcessID"]);
                var name = ev["ProcessName"]?.ToString() ?? "";
                _processStopped?.Invoke(this, new ProcessStoppedEvent(pid, name));
            }
            catch { }
        };
        _stop.Start();
    }

    public void Stop()
    {
        _start?.Stop(); _start?.Dispose(); _start = null;
        _stop?.Stop(); _stop?.Dispose(); _stop = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
