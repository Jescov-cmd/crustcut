<#
  Samples a running Crustcut process at a fixed interval and writes a CSV.
  Usage: .\scripts\measure-idle.ps1 -Minutes 10
  Leave the app idle on the Overview tab for the duration — no clicking.
#>
param(
    [int]$Minutes = 10,
    [string]$ProcessName = 'Crustcut',
    [string]$OutFile = 'idle-samples.csv'
)

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $proc) { Write-Error "No process named '$ProcessName' is running."; exit 1 }

Write-Host "Sampling PID $($proc.Id) for $Minutes minute(s)..."

$samples = @()
$deadline = (Get-Date).AddMinutes($Minutes)
$lastCpu  = $proc.TotalProcessorTime
$lastTime = Get-Date

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
    if ($proc.HasExited) { Write-Warning 'Process exited early.'; break }

    $now    = Get-Date
    $cpuNow = $proc.TotalProcessorTime
    $cpuPct = (($cpuNow - $lastCpu).TotalSeconds / ($now - $lastTime).TotalSeconds / [Environment]::ProcessorCount) * 100
    $lastCpu = $cpuNow; $lastTime = $now

    $samples += [pscustomobject]@{
        Timestamp   = $now.ToString('o')
        CpuPercent  = [math]::Round($cpuPct, 3)
        PrivateMB   = [math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
        WorkingSetMB= [math]::Round($proc.WorkingSet64 / 1MB, 2)
        Handles     = $proc.HandleCount
        Threads     = $proc.Threads.Count
    }
}

$samples | Export-Csv -Path $OutFile -NoTypeInformation -Encoding utf8
Write-Host "Wrote $($samples.Count) samples to $OutFile"

$samples | Measure-Object -Property CpuPercent  -Average -Maximum | Format-List
$samples | Measure-Object -Property PrivateMB   -Average -Maximum | Format-List
$samples | Measure-Object -Property Handles     -Average -Maximum | Format-List
$samples | Measure-Object -Property Threads     -Average -Maximum | Format-List
