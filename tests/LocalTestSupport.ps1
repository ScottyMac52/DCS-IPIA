#Requires -Version 7.0
# Shared execution policy for the local test entry points.
function Invoke-LocalTestCommand {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][scriptblock]$Command)
    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE. Stop here and fix the first failure." }
}

function Invoke-LocalTestSuite {
    param([Parameter(Mandatory)][scriptblock]$Action)
    Push-Location (Split-Path $PSScriptRoot -Parent)
    try { & $Action }
    finally { Pop-Location }
}

function Assert-LocalWindows {
    if (-not $IsWindows) { throw 'This test suite requires Windows and PowerShell 7 (pwsh).' }
}
