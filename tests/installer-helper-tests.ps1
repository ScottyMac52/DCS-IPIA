#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Assert-LocalWindows
    & ./tests/installer/Test-ReleaseCleanup.ps1
    & ./tests/installer/Test-RuntimePackNotices.ps1
    # This compatibility test deliberately uses Windows PowerShell 5.1, as in CI.
    Invoke-LocalTestCommand 'Saved Games settings compatibility' {
        powershell.exe -NoProfile -File ./tests/installer/Test-SavedGamesSettings.ps1
    }
}
