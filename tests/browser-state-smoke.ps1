#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Assert-LocalWindows
    Invoke-LocalTestCommand 'Windows solution build' { dotnet build HEIMDALL.sln -c Release }
    Invoke-LocalTestCommand 'Browser/editor state unit regressions' {
        dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --no-build `
            --filter 'FullyQualifiedName~WorkspaceEditorObserverTests|FullyQualifiedName~AssignmentBrowserNativePolicyTests'
    }
    Invoke-LocalTestCommand 'Four-game browser/editor state conformance' {
        dotnet run --project tests/Heimdall.App.Smoke/Heimdall.App.Smoke.csproj -c Release --no-build -- --group browser-state
    }
}
