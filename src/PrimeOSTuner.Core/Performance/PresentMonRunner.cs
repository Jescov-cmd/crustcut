using System.Diagnostics;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Spawns the bundled PresentMon.exe as a subprocess. PresentMon is told to
/// self-terminate when the target game exits via <c>-terminate_on_proc_exit</c>
/// and to skip writing CSVs for processes that never present a frame (a
/// launcher rather than a real game).
///
/// Note: lives in Core (not Win) because the project topology is Core → Win,
/// not the reverse — so anything that consumes a Core abstraction must live
/// in Core itself.
/// </summary>
public sealed class PresentMonRunner : IPresentMonRunner
{
    private readonly string _binaryPath;
    private Process? _current;
    private readonly object _gate = new();

    public PresentMonRunner(string binaryPath)
    {
        _binaryPath = binaryPath;
    }

    public Task<string?> StartAsync(int gamePid, string outputCsvPath, CancellationToken ct = default)
    {
        if (!File.Exists(_binaryPath)) return Task.FromResult<string?>(null);

        try
        {
            var psi = new ProcessStartInfo(_binaryPath)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-process_id");
            psi.ArgumentList.Add(gamePid.ToString());
            psi.ArgumentList.Add("-output_file");
            psi.ArgumentList.Add(outputCsvPath);
            psi.ArgumentList.Add("-no_csv_for_processes_with_no_present_events");
            psi.ArgumentList.Add("-terminate_on_proc_exit");
            psi.ArgumentList.Add("-stop_existing_session");

            Directory.CreateDirectory(Path.GetDirectoryName(outputCsvPath)!);

            var proc = Process.Start(psi);
            if (proc is null) return Task.FromResult<string?>(null);

            lock (_gate) _current = proc;
            return Task.FromResult<string?>(outputCsvPath);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Process? toStop;
        lock (_gate) { toStop = _current; _current = null; }
        if (toStop is null) return;

        try
        {
            if (!toStop.HasExited) toStop.Kill(entireProcessTree: true);
            // Wait briefly so PresentMon flushes its CSV before we try to parse it.
            await toStop.WaitForExitAsync(ct);
        }
        catch { /* best effort */ }
        finally
        {
            try { toStop.Dispose(); } catch { }
        }
    }
}
