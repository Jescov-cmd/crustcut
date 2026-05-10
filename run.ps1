# One-shot build + run for PrimeOS Tuner.
# Kills any running instance, builds, launches with admin.
# Usage: ./run.ps1

$ErrorActionPreference = 'Stop'

Write-Host "→ Stopping any running instance..." -ForegroundColor DarkGray
Get-Process PrimeOSTuner.UI -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_ | Stop-Process -Force } catch { }
}
Start-Sleep -Milliseconds 400

Write-Host "→ Building..." -ForegroundColor DarkGray
dotnet build PrimeOSTuner.sln --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed." -ForegroundColor Red
    exit 1
}

$exe = Join-Path $PSScriptRoot 'src\PrimeOSTuner.UI\bin\Debug\net9.0-windows\PrimeOSTuner.UI.exe'
if (-not (Test-Path $exe)) {
    Write-Host "✗ Build output not found at $exe" -ForegroundColor Red
    exit 1
}

Write-Host "→ Launching $exe (UAC prompt incoming)..." -ForegroundColor DarkGray
Start-Process $exe -Verb RunAs
