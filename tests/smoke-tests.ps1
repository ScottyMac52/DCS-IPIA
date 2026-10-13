#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Assert-LocalWindows
    $previousPreview = $env:IPIA_MAPPING_PREVIEW
    $previousParity = $env:IPIA_PARITY_OUTPUT
    try {
        $env:IPIA_MAPPING_PREVIEW = Join-Path $PWD 'TestResults/custom-mapping-preview'
        $env:IPIA_PARITY_OUTPUT = Join-Path $PWD 'TestResults/huey-parity'
        Invoke-LocalTestCommand 'Windows solution build' { dotnet build DCS-IPIA.sln -c Release }
        Invoke-LocalTestCommand 'WPF smoke tests' {
            dotnet run --project tests/Dcs.Ipia.App.Smoke/Dcs.Ipia.App.Smoke.csproj -c Release --no-build
        }
        Invoke-LocalTestCommand 'Windows unit tests' {
            dotnet test tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj -c Release --no-build `
                --logger trx --results-directory TestResults/raw/windows
        }
    } finally {
        $env:IPIA_MAPPING_PREVIEW = $previousPreview
        $env:IPIA_PARITY_OUTPUT = $previousParity
    }
}
