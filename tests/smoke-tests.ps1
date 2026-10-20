#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Assert-LocalWindows
    $previousPreview = $env:HEIMDALL_MAPPING_PREVIEW
    $previousParity = $env:HEIMDALL_PARITY_OUTPUT
    try {
        $env:HEIMDALL_MAPPING_PREVIEW = Join-Path $PWD 'TestResults/custom-mapping-preview'
        $env:HEIMDALL_PARITY_OUTPUT = Join-Path $PWD 'TestResults/huey-parity'
        Invoke-LocalTestCommand 'Windows solution build' { dotnet build HEIMDALL.sln -c Release }
        Invoke-LocalTestCommand 'WPF smoke tests' {
            dotnet run --project tests/Heimdall.App.Smoke/Heimdall.App.Smoke.csproj -c Release --no-build
        }
        Invoke-LocalTestCommand 'Windows unit tests' {
            dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --no-build `
                --logger trx --results-directory TestResults/raw/windows
        }
    } finally {
        $env:HEIMDALL_MAPPING_PREVIEW = $previousPreview
        $env:HEIMDALL_PARITY_OUTPUT = $previousParity
    }
}
