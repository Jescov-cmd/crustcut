using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// One network-adapter advanced property as Windows reports it.
/// <paramref name="Valid"/> is the allowed-value list for enumerated properties (empty for
/// numeric ranges, which use <paramref name="Min"/>/<paramref name="Max"/>).
/// </summary>
public sealed record NicProperty(string Adapter, string Keyword, string Value,
    IReadOnlyList<string> Valid, int? Min, int? Max);

/// <summary>A change the fix wants: set Keyword on Adapter from From to To.</summary>
public sealed record NicChange(string Adapter, string Keyword, string From, string To);

/// <summary>
/// The network card fix: bigger receive/transmit buffers, interrupt moderation and flow
/// control off. Measured on the user's Realtek (Sleuth, 2026-08-23): worst DPC 13.67 ms →
/// 0.28 ms. Stock 32-entry buffers make the driver batch received packets into a few very
/// long DPCs on core 0, and while one runs NOTHING else on that core does — mouse input,
/// the game's frame, other packets. Felt as latency and stutter; invisible to CPU%, GPU%
/// and RAM. A driver update silently put the stock values back on 2026-09-25, which is why
/// this is a tweak: drift enforcement re-applies it whenever that happens again.
///
/// Opt-in: the adapter restarts (a few seconds without network) and the right values are
/// hardware-specific. Undo restores each adapter's exact previous values; with no undo
/// data, "revert to default" asks the driver for its own defaults.
/// </summary>
public sealed class NicLatencyTweak : ITweak, ICategorizedTweak, IOptInTweak, ISelfRevertingTweak
{
    private const int WantReceiveBuffers = 512;
    private const int WantTransmitBuffers = 128;

    private static readonly string[] Keywords =
        { "*ReceiveBuffers", "*TransmitBuffers", "*InterruptModeration", "*FlowControl" };

    public string Id => "game.nic-latency";
    public string DisplayName => "Network card latency fix";
    public string Description =>
        "Gives your network card bigger buffers and stops it batching work into long stalls. " +
        "On cards that need it (many Realtek ones) this removes lag spikes and stutter that " +
        "don't show up as CPU, GPU or memory load. Crustcut puts it back if a driver update resets it.";
    public string Category => "network";
    public string? RiskNote => "Your internet drops for a few seconds while the network card restarts.";
    public bool OptIn => true;
    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    // ── Pure planning ─────────────────────────────────────────────────────────────────

    /// <summary>What would change to bring these properties to the fix. Never lowers a
    /// buffer that is already bigger, and never writes a value the driver doesn't allow.</summary>
    public static IReadOnlyList<NicChange> Plan(IEnumerable<NicProperty> properties)
    {
        var changes = new List<NicChange>();
        foreach (var p in properties)
        {
            var target = p.Keyword switch
            {
                "*ReceiveBuffers" => BufferTarget(p, WantReceiveBuffers),
                "*TransmitBuffers" => BufferTarget(p, WantTransmitBuffers),
                "*InterruptModeration" or "*FlowControl" => Allowed(p, "0") ? "0" : null,
                _ => null,
            };
            if (target is not null && target != p.Value)
                changes.Add(new NicChange(p.Adapter, p.Keyword, p.Value, target));
        }
        return changes;
    }

    private static string? BufferTarget(NicProperty p, int want)
    {
        var current = int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? c : 0;
        if (current >= want) return null;   // already big enough — leave it

        int? best;
        if (p.Valid.Count > 0)
        {
            // Enumerated: the largest allowed value up to what we want, else nothing.
            best = p.Valid.Select(v => int.TryParse(v, out var n) ? n : (int?)null)
                .Where(n => n is not null && n <= want).Max();
        }
        else
        {
            best = p.Max is int max ? Math.Min(want, max) : want;
            if (p.Min is int min && best < min) best = null;
        }
        return best is int b && b > current ? b.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static bool Allowed(NicProperty p, string value) => p.Valid.Count == 0 || p.Valid.Contains(value);

    // ── ITweak ────────────────────────────────────────────────────────────────────────

    public async Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var props = await ReadAsync(ct);
            if (props.Count == 0) return TweakState.Unknown;   // no adapter exposes these
            return Plan(props).Count == 0 ? TweakState.Applied : TweakState.NotApplied;
        }
        catch { return TweakState.Unknown; }
    }

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var changes = Plan(await ReadAsync(ct));
            if (changes.Count == 0) return TweakResult.Success(JsonSerializer.Serialize(changes), "Already tuned.");
            await WriteAsync(changes.Select(c => (c.Adapter, c.Keyword, c.To)), ct);
            // Undo data = what each value was before, so revert is exact per adapter.
            return TweakResult.Success(JsonSerializer.Serialize(changes),
                $"Tuned {changes.Select(c => c.Adapter).Distinct().Count()} network card(s): {changes.Count} setting(s) changed.");
        }
        catch (Exception ex) { return TweakResult.Failure(ex.Message); }
    }

    public async Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        try
        {
            var changes = JsonSerializer.Deserialize<List<NicChange>>(undoData) ?? new();
            if (changes.Count == 0) return TweakResult.Success();
            await WriteAsync(changes.Select(c => (c.Adapter, c.Keyword, c.From)), ct);
            return TweakResult.Success();
        }
        catch (Exception ex) { return TweakResult.Failure(ex.Message); }
    }

    /// <summary>No undo data: ask each driver for its own defaults.</summary>
    public async Task<TweakResult> RevertToDefaultAsync(CancellationToken ct = default)
    {
        try
        {
            var props = await ReadAsync(ct);
            var sb = new StringBuilder("$ErrorActionPreference='Stop';");
            foreach (var adapter in props.Select(p => p.Adapter).Distinct())
            {
                foreach (var k in props.Where(p => p.Adapter == adapter).Select(p => p.Keyword))
                    sb.Append($"Reset-NetAdapterAdvancedProperty -Name {Q(adapter)} -RegistryKeyword {Q(k)} -NoRestart;");
                sb.Append($"Restart-NetAdapter -Name {Q(adapter)} -Confirm:$false;");
            }
            await RunAsync(sb.ToString(), ct);
            return TweakResult.Success();
        }
        catch (Exception ex) { return TweakResult.Failure(ex.Message); }
    }

    public Task<string> PreviewAsync(CancellationToken ct = default) => Task.FromResult(
        "Will raise Receive Buffers to 512 and Transmit Buffers to 128 (where allowed), turn off " +
        "Interrupt Moderation and Flow Control, then restart the network card. Undo restores the exact previous values.");

    // ── PowerShell plumbing ───────────────────────────────────────────────────────────

    private static async Task<List<NicProperty>> ReadAsync(CancellationToken ct)
    {
        var keywords = string.Join(",", Keywords.Select(Q));
        var script =
            "$r = foreach ($a in Get-NetAdapter -Physical | Where-Object Status -eq 'Up') {" +
            $" foreach ($p in Get-NetAdapterAdvancedProperty -Name $a.Name -RegistryKeyword {keywords} -ErrorAction SilentlyContinue) {{" +
            "  [pscustomobject]@{ Adapter=$a.Name; Keyword=$p.RegistryKeyword; Value=[string](@($p.RegistryValue) -join '');" +
            "   Valid=@($p.ValidRegistryValues | ForEach-Object { [string]$_ }); Min=$p.NumericParameterMinValue; Max=$p.NumericParameterMaxValue } } };" +
            "ConvertTo-Json -InputObject @($r) -Compress -Depth 3";
        var json = (await RunAsync(script, ct)).Trim();
        if (json.Length == 0) return new();

        var result = new List<NicProperty>();
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            result.Add(new NicProperty(
                e.GetProperty("Adapter").GetString() ?? "",
                e.GetProperty("Keyword").GetString() ?? "",
                e.GetProperty("Value").GetString() ?? "",
                // Numeric-range properties report Valid as [""] — one empty entry, not a list.
                e.TryGetProperty("Valid", out var v) && v.ValueKind == JsonValueKind.Array
                    ? v.EnumerateArray().Select(x => x.ToString()).Where(s => s.Length > 0).ToList()
                    : new List<string>(),
                IntOrNull(e, "Min"), IntOrNull(e, "Max")));
        }
        return result;

        // Drivers report the range as numbers OR strings ("32", "512") — accept both.
        static int? IntOrNull(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }
    }

    private static async Task WriteAsync(IEnumerable<(string Adapter, string Keyword, string Value)> values, CancellationToken ct)
    {
        var list = values.ToList();
        var sb = new StringBuilder("$ErrorActionPreference='Stop';");
        foreach (var (a, k, v) in list)
            sb.Append($"Set-NetAdapterAdvancedProperty -Name {Q(a)} -RegistryKeyword {Q(k)} -RegistryValue {Q(v)} -NoRestart;");
        // One restart per adapter, after all its values are written — not one per setting.
        foreach (var a in list.Select(x => x.Adapter).Distinct())
            sb.Append($"Restart-NetAdapter -Name {Q(a)} -Confirm:$false;");
        await RunAsync(sb.ToString(), ct);
    }

    private static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    /// <summary>Runs a script via -EncodedCommand (UTF-16 base64), so adapter names with
    /// quotes or spaces can never break the command line.</summary>
    private static async Task<string> RunAsync(string script, CancellationToken ct)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start PowerShell.");
        var stdout = proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException((await stderr).Trim() is { Length: > 0 } err ? err : $"PowerShell exited {proc.ExitCode}");
        return await stdout;
    }
}
