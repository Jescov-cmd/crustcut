using System.Diagnostics;
using System.Text.Json;

namespace PrimeOSTuner.Core.Bloatware;

public sealed class AppxClient : IAppxClient
{
    public async Task<IReadOnlyList<InstalledAppx>> ListInstalledAsync(CancellationToken ct = default)
    {
        // Get-AppxPackage returns objects; pipe to ConvertTo-Json so we can parse.
        // -Compress keeps the output single-line per item so it's easier to debug.
        const string script =
            "Get-AppxPackage | Select-Object Name,PackageFullName,InstallLocation | ConvertTo-Json -Compress";
        var stdout = await RunPowerShellAsync(script, ct);
        if (string.IsNullOrWhiteSpace(stdout)) return Array.Empty<InstalledAppx>();

        // ConvertTo-Json on a single object emits a JSON object; on multiple, a JSON array.
        // Normalize to array.
        if (!stdout.TrimStart().StartsWith("["))
            stdout = "[" + stdout + "]";

        var raw = JsonSerializer.Deserialize<List<RawAppx>>(stdout, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();

        return raw
            .Where(r => !string.IsNullOrEmpty(r.Name) && !string.IsNullOrEmpty(r.PackageFullName))
            .Select(r => new InstalledAppx(r.Name!, r.PackageFullName!, r.InstallLocation))
            .ToList();
    }

    public async Task RemoveAsync(string packageFullName, CancellationToken ct = default)
    {
        // -ErrorAction Stop forces non-zero exit on failure so RunPowerShellAsync throws.
        var script = $"Get-AppxPackage -Name '{packageFullName}' | Remove-AppxPackage -ErrorAction Stop";
        await RunPowerShellAsync(script, ct);
    }

    public async Task RemoveProvisionedAsync(string packageName, CancellationToken ct = default)
    {
        // Get-AppxProvisionedPackage matches on DisplayName (which equals package Name).
        var script = $@"
Get-AppxProvisionedPackage -Online |
  Where-Object {{ $_.DisplayName -eq '{packageName}' }} |
  Remove-AppxProvisionedPackage -Online -ErrorAction Stop";
        await RunPowerShellAsync(script, ct);
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"")}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start powershell.exe");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"PowerShell exited {proc.ExitCode}: {stderr.Trim()}");
        return stdout;
    }

    private sealed class RawAppx
    {
        public string? Name { get; set; }
        public string? PackageFullName { get; set; }
        public string? InstallLocation { get; set; }
    }
}
