#Requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidateSet('dcs', 'elite', 'squadrons', 'war-thunder')][string]$Game,
    [Parameter(Mandatory)][string]$ConfigurationFile,
    [string]$DcsSnapshotFile,
    [string]$OutputDirectory = 'TestResults/performance-smoke',
    [ValidateRange(1, 1000)][int]$Iterations = 20,
    [ValidateRange(1, 60000)][int]$UiBudgetMilliseconds = 1000,
    [ValidateRange(1, 60)][int]$TimeoutMinutes = 10
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Assert-LocalWindows
$source = (Resolve-Path -LiteralPath $ConfigurationFile).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$runLog = Join-Path $outputRoot ('runner-' + [guid]::NewGuid().ToString('N') + '.log')
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'Heimdall.App.Smoke/Heimdall.App.Smoke.csproj'
& dotnet build $project -c Profile
if ($LASTEXITCODE -ne 0) { throw 'Profile smoke build failed.' }
$start = [Diagnostics.ProcessStartInfo]::new('dotnet')
$start.WorkingDirectory = $repoRoot
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('run', '--project', $project, '-c', 'Profile', '--no-build', '--', '--performance-game', $Game, '--input', $source, '--output', $outputRoot)) {
    $start.ArgumentList.Add($argument)
}
if ($DcsSnapshotFile) {
    if ($Game -ne 'dcs') { throw 'DcsSnapshotFile is only valid for DCS.' }
    $start.Environment['HEIMDALL_SMOKE_DCS_SNAPSHOT'] = (Resolve-Path -LiteralPath $DcsSnapshotFile).Path
} else { $start.Environment.Remove('HEIMDALL_SMOKE_DCS_SNAPSHOT') | Out-Null }
$start.Environment['HEIMDALL_SMOKE_ITERATIONS'] = [string]$Iterations
$start.Environment['HEIMDALL_SMOKE_UI_BUDGET_MS'] = [string]$UiBudgetMilliseconds
$process = [Diagnostics.Process]::new()
$process.StartInfo = $start
try {
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutMinutes * 60000)) {
        $process.Kill($true)
        $process.WaitForExit()
        ($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()) | Set-Content -LiteralPath $runLog -Encoding utf8
        throw "Performance smoke timed out; partial log: $runLog"
    }
    $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    $text | Set-Content -LiteralPath $runLog -Encoding utf8
    Write-Host $text
    $exitCode = $process.ExitCode
} finally { $process.Dispose() }
# Publish only this invocation's capture; previous runs cannot supply acceptance evidence.
$artifactLine = ($text -split "`n" | Where-Object { $_.StartsWith('Configuration performance artifacts: ') } | Select-Object -Last 1)
if ($artifactLine) {
    $artifactDirectory = $artifactLine.Substring('Configuration performance artifacts: '.Length).Trim()
    $captures = @(Get-ChildItem -LiteralPath $artifactDirectory -Filter 'capture-*.json')
    if ($captures.Count -ne 1) { throw "Scenario did not produce one telemetry capture. See $runLog" }
    foreach ($capture in $captures) {
        & py -3.12 (Join-Path $repoRoot 'scripts/performance_report.py') $capture.FullName --output $artifactDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Performance report generation failed.' }
    }
} else { throw "Scenario did not produce artifacts. See $runLog" }
if ($exitCode -ne 0) { throw "Configuration performance smoke failed. See $runLog" }
