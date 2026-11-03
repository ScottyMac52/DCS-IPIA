#Requires -Version 7.0
param(
    [string]$OutputPath = (Join-Path (Get-Location) ('controller-evidence-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
)
$ErrorActionPreference = 'Stop'
$destination = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw "Evidence capture already exists: $destination" }
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet run --project .\src\Heimdall.App -c Release --no-build -- --controller-evidence $destination
    if ($LASTEXITCODE -ne 0) { throw "Controller evidence export failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath $destination)) { throw 'Controller evidence export did not create its output file.' }
    Write-Host "Controller evidence: $destination"
} finally {
    Pop-Location
}
