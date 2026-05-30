using System.Diagnostics;
using System.Globalization;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Spawns the bundled PresentMon.exe (v2.x) as a subprocess. Uses the v2 double-dash
/// flags + <c>--v1_metrics</c> so the CSV carries the classic <c>msBetweenPresents</c>
/// column the parser reads. <c>StreamAsync</c> reads frames live from stdout (for the live
/// FPS counter + session stats); the legacy file mode is kept for completeness.
///
/// Note: lives in Core (not Win) because the project topology is Core → Win.
/// </summary>
public sealed class PresentMonRunner : IPresentMonRunner
{
    private const string FrameTimeColumn = "msBetweenPresents";

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
            var psi = NewPsi();
            AddCommonArgs(psi, gamePid);
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(outputCsvPath);

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

    public async Task StreamAsync(int gamePid, Action<double> onFrame, CancellationToken ct = default)
    {
        if (!File.Exists(_binaryPath)) return;

        Process? proc = null;
        try
        {
            var psi = NewPsi();
            AddCommonArgs(psi, gamePid);
            psi.ArgumentList.Add("--output_stdout");

            proc = Process.Start(psi);
            if (proc is null) return;
            lock (_gate) _current = proc;

            int col = -1;
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (col < 0)
                {
                    // First CSV line is the header — locate the frame-time column.
                    var headers = line.Split(',');
                    col = Array.FindIndex(headers,
                        h => h.Trim().Equals(FrameTimeColumn, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                var cells = line.Split(',');
                if (cells.Length > col
                    && double.TryParse(cells[col], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
                    && ms > 0)
                {
                    onFrame(ms);
                }
            }

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Streaming failure must never break a game launch.
        }
        finally
        {
            lock (_gate) { if (ReferenceEquals(_current, proc)) _current = null; }
            try { proc?.Dispose(); } catch { }
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
            await toStop.WaitForExitAsync(ct);
        }
        catch { /* best effort */ }
        finally
        {
            try { toStop.Dispose(); } catch { }
        }
    }

    private ProcessStartInfo NewPsi() => new(_binaryPath)
    {
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    // PresentMon 2.x flags (double-dash). --v1_metrics keeps the msBetweenPresents column;
    // --no_console_stats keeps stdout clean; the session self-terminates when the game exits.
    private static void AddCommonArgs(ProcessStartInfo psi, int gamePid)
    {
        psi.ArgumentList.Add("--process_id");
        psi.ArgumentList.Add(gamePid.ToString());
        psi.ArgumentList.Add("--v1_metrics");
        psi.ArgumentList.Add("--no_console_stats");
        psi.ArgumentList.Add("--terminate_on_proc_exit");
        psi.ArgumentList.Add("--stop_existing_session");
    }
}
