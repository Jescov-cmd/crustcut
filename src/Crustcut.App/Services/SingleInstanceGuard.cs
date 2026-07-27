using System.Threading;

namespace Crustcut.App.Services;

/// <summary>
/// Ensures only one Crustcut runs at a time. Without this, autostart plus a manual launch
/// gives you two windows and two tray icons, only one of which responds — a bug the WPF
/// build hit in the field.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\Crustcut.Avalonia.SingleInstance";
    private const string ShowEventName = @"Local\Crustcut.Avalonia.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _listenCts;

    /// <summary>Raised when another instance asks this one to come to the front.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>
    /// True when this process owns the single-instance slot. False means another instance
    /// is already running and has been signalled to show itself — the caller should exit.
    /// </summary>
    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            if (!createdNew)
            {
                _showEvent.Set();   // ask the running instance to surface
                return false;
            }

            StartListening();
            return true;
        }
        catch
        {
            // If the OS refuses the handles, prefer running over refusing to start.
            return true;
        }
    }

    private void StartListening()
    {
        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        var handle = _showEvent;

        var thread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (handle is null) return;
                    if (handle.WaitOne(500)) ShowRequested?.Invoke(this, EventArgs.Empty);
                }
                catch { return; }
            }
        })
        { IsBackground = true, Name = "Crustcut single-instance listener" };

        thread.Start();
    }

    public void Dispose()
    {
        try { _listenCts?.Cancel(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        _showEvent?.Dispose();
        _listenCts?.Dispose();
    }
}
