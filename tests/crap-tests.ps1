#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    # Original filename retained; this prepares the report tool rather than running tests.
    $installed = Invoke-LocalTestCommand 'ReportGenerator inventory' {
        dotnet tool list --tool-path .tools/reportgenerator
    }
    if ($installed -match '^dotnet-reportgenerator-globaltool\s+5\.5\.11\s+') {
        Write-Output 'ReportGenerator 5.5.11 is already installed.'
    } elseif ($installed -match '^dotnet-reportgenerator-globaltool\s+') {
        Invoke-LocalTestCommand 'Pin ReportGenerator' {
            dotnet tool update dotnet-reportgenerator-globaltool --tool-path .tools/reportgenerator --version 5.5.11 --allow-downgrade
        }
    } else {
        Invoke-LocalTestCommand 'Install ReportGenerator' {
            dotnet tool install dotnet-reportgenerator-globaltool --tool-path .tools/reportgenerator --version 5.5.11
        }
    }
}
