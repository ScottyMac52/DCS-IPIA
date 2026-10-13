#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Assert-LocalWindows
    $previousTemp = $env:RUNNER_TEMP
    $previousCommit = $env:GITHUB_SHA
    try {
        $env:RUNNER_TEMP = Join-Path ([IO.Path]::GetTempPath()) 'ipia-release-tests'
        New-Item -ItemType Directory -Force $env:RUNNER_TEMP | Out-Null
        $env:GITHUB_SHA = (Invoke-LocalTestCommand 'Source commit' { git rev-parse HEAD }).Trim()
        $version = (Get-Content release/version.json -Raw | ConvertFrom-Json).version
        $output = 'release-assets-local-' + [guid]::NewGuid().ToString('N')
        & ./scripts/Test-ApplicationRelease.ps1 -Version $version -Output $output
        Write-Output "Verified release artifacts: $output"
    } finally {
        $env:RUNNER_TEMP = $previousTemp
        $env:GITHUB_SHA = $previousCommit
    }
}
