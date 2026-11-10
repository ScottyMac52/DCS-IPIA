#Requires -Version 7.0
[CmdletBinding()]
param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$ProfileName = 'performance')
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The WPF profiling session requires Windows.' }
$root = Split-Path $PSScriptRoot -Parent
$previousDirectory = Get-Location
$previousCapture = $env:HEIMDALL_PERFORMANCE_DIRECTORY
try {
    Set-Location $root
    $session = Join-Path $root ('TestResults/performance/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $session -Force | Out-Null
    dotnet build src/Heimdall.App/Heimdall.App.csproj -c Profile
    if ($LASTEXITCODE -ne 0) { throw "Profile build failed: $LASTEXITCODE" }
    $env:HEIMDALL_PERFORMANCE_DIRECTORY = $session
    $app = Join-Path $root 'src/Heimdall.App/bin/Profile/net10.0-windows/Heimdall.exe'
    Write-Host "Capture folder: $session"
    Write-Host "Using isolated test profile '$ProfileName'. Exercise the scenario, then close HEIMDALL normally."
    $process = Start-Process -FilePath $app -ArgumentList '--test-profile', $ProfileName -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Profile application failed: $($process.ExitCode). Capture folder: $session" }
    $captures = @(Get-ChildItem -LiteralPath $session -Filter 'capture-*.json')
    if ($captures.Count -ne 1) { throw "Expected one capture; found $($captures.Count). Inspect $session and the application log." }
    py -3.12 scripts/performance_report.py $captures[0].FullName --output $session
    if ($LASTEXITCODE -ne 0) { throw "Report generation failed: $LASTEXITCODE" }
    Write-Host "Capture and reports: $session"
}
finally {
    $env:HEIMDALL_PERFORMANCE_DIRECTORY = $previousCapture
    Set-Location $previousDirectory
}
