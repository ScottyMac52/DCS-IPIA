#Requires -Version 7.0
param([switch]$NoOpen)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    $tool = if ($IsWindows) { './.tools/reportgenerator/reportgenerator.exe' } else { './.tools/reportgenerator/reportgenerator' }
    if (-not (Test-Path -LiteralPath $tool)) { throw 'Run tests/crap-tests.ps1 to install ReportGenerator first.' }
    $coverageFiles = @(Get-ChildItem TestResults/raw -Recurse -File -Filter '*.opencover.xml' -ErrorAction SilentlyContinue)
    if ($coverageFiles.Count -eq 0) { throw 'No OpenCover data found. Run tests/coverage-test-gates.ps1 first.' }
    Invoke-LocalTestCommand 'Coverage report' {
        & $tool '-reports:TestResults/raw/**/*.opencover.xml' '-targetdir:TestResults/report' `
            "-sourcedirs:$PWD" '-reporttypes:Html;MarkdownSummaryGithub;JsonSummary;CsvSummary'
    }
    Invoke-LocalTestCommand 'CRAP and test result summaries' {
        py -3.12 scripts/publish_test_results.py --raw TestResults/raw --output TestResults/report `
            --summary TestResults/report/LOCAL-SUMMARY.md
    }
    if (-not $NoOpen) { Invoke-Item TestResults/report/index.html }
}
