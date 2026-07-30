using System.Management;

namespace PrimeOSTuner.Core.Memory;

public sealed class WmiProcessWatcher : IProcessWatcher
{
    private ManagementEventWatcher? _start;
    private ManagementEventWatcher? _stop;
    private bool _disposed;

    public event EventHandler<ProcessStartedEvent>? ProcessStarted;
    public event EventHandler<ProcessStoppedEvent>? ProcessStopped;

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WmiProcessWatcher));
        if (_start is not null) return;

        _start = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
        _start.EventArrived += (_, args) =>
        {
            try
            {
                var pid = Convert.ToInt32(args.NewEvent.Properties["ProcessID"].Value);
                var name = args.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
                ProcessStarted?.Invoke(this, new ProcessStartedEvent(pid, name));
            }
            catch { /* swallow — best effort */ }
        };
        _start.Start();

        _stop = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
        _stop.EventArrived += (_, args) =>
        {
            try
            {
                var pid = Convert.ToInt32(args.NewEvent.Properties["ProcessID"].Value);
                var name = args.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
                ProcessStopped?.Invoke(this, new ProcessStoppedEvent(pid, name));
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
