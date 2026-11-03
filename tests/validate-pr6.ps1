#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    # Existing wrappers retain their own setup and stop on failure.
    & (Join-Path $PSScriptRoot 'coverage-test-gates.ps1')
    & (Join-Path $PSScriptRoot 'crap-tests.ps1')
    & (Join-Path $PSScriptRoot 'generate-test-reports.ps1') -NoOpen
    & (Join-Path $PSScriptRoot 'smoke-tests.ps1')
} finally {
    Pop-Location
}
