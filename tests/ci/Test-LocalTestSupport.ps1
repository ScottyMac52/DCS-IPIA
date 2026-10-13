#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../LocalTestSupport.ps1')
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Fails([scriptblock]$Action, [string]$Expected) {
    try { & $Action } catch {
        Assert-True ($_.Exception.Message.Contains($Expected)) "Unexpected failure: $($_.Exception.Message)"
        return
    }
    throw "Expected failure containing: $Expected"
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('ipia-local-tests-' + [guid]::NewGuid().ToString('N'))
$before = (Get-Location).Path
$hostPath = (Get-Process -Id $PID).Path
try {
    # Real child process failures must be detected, and caller location must be restored.
    Assert-Fails {
        Invoke-LocalTestSuite {
            Assert-True ((Get-Location).Path -eq (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)) 'Suite did not select repository root.'
            Invoke-LocalTestCommand 'Native fixture' { & $hostPath -NoProfile -Command 'exit 23' }
            throw 'Unreachable command ran.'
        }
    } 'Native fixture failed with exit code 23'
    Assert-True ((Get-Location).Path -eq $before) 'Failure changed caller location.'
    Invoke-LocalTestCommand 'Successful native fixture' { & $hostPath -NoProfile -Command 'exit 0' }
    Assert-Fails { Invoke-LocalTestSuite { throw 'Original script failure' } } 'Original script failure'
    Assert-True ((Get-Location).Path -eq $before) 'Script exception changed caller location.'

    # Use isolated copies and command doubles to exercise orchestration without running the production suites.
    $tests = Join-Path $root 'tests'
    New-Item -ItemType Directory -Path $tests -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot '../*.ps1') $tests
    foreach ($file in Get-ChildItem $tests -Filter '*.ps1') {
        $tokens = $null; $errors = $null
        [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
        Assert-True ($errors.Count -eq 0) "Invalid PowerShell syntax: $($file.Name)"
        Assert-True ((Get-Content $file.FullName -Raw).StartsWith('#Requires -Version 7.0')) "Missing shell guard: $($file.Name)"
    }
    $global:LocalTestCalls = [Collections.Generic.List[string]]::new()
    $global:LocalTestFailure = ''
    $global:LocalTestToolVersion = ''
    function global:dotnet {
        $command = $args -join ' '
        $global:LocalTestCalls.Add('dotnet ' + $command)
        if ($global:LocalTestFailure -and $command.StartsWith($global:LocalTestFailure)) { $global:LASTEXITCODE = 17; return }
        $global:LASTEXITCODE = 0
        if ($command.StartsWith('tool list') -and $global:LocalTestToolVersion) {
            'dotnet-reportgenerator-globaltool ' + $global:LocalTestToolVersion + ' reportgenerator'
        } elseif ($command.StartsWith('tool install') -or $command.StartsWith('tool update')) {
            $global:LocalTestToolVersion = '5.5.11'
        }
    }
    function global:py { $global:LocalTestCalls.Add('py ' + ($args -join ' ')); $global:LASTEXITCODE = 19 }
    function global:node { $global:LocalTestCalls.Add('node ' + ($args -join ' ')); $global:LASTEXITCODE = 21 }
    function global:git { $global:LocalTestCalls.Add('git ' + ($args -join ' ')); $global:LASTEXITCODE = 22 }
    Push-Location ([IO.Path]::GetTempPath())
    try {
        Assert-Fails { & (Join-Path $tests 'csharp-test.ps1') } 'C# layout failed with exit code 19'
        Assert-True ($global:LocalTestCalls.Count -eq 1) 'Layout continued after a failure.'
        $global:LocalTestCalls.Clear()
        Assert-Fails { & (Join-Path $tests 'asset-export-tests.ps1') } 'Asset exporter tests failed with exit code 21'
        Assert-True ($global:LocalTestCalls.Count -eq 1) 'Asset gate ran after failed exporter tests.'
        $global:LocalTestCalls.Clear(); $global:LocalTestFailure = 'restore'
        Assert-Fails { & (Join-Path $tests 'coverage-test-gates.ps1') } 'Restore failed with exit code 17'
        Assert-True ($global:LocalTestCalls.Count -eq 1) 'Coverage continued after restore failed.'
        $global:LocalTestCalls.Clear(); $global:LocalTestFailure = ''
        & (Join-Path $tests 'coverage-test-gates.ps1')
        Assert-True ($global:LocalTestCalls.Count -eq 4) 'Coverage lost its restore/format/build/test sequence.'
        Assert-True ($global:LocalTestCalls[3].Contains('/p:ThresholdStat=minimum') -and
            $global:LocalTestCalls[3].Contains('/p:Threshold=80%2c80')) 'Independent 80/80 gates changed.'

        $global:LocalTestCalls.Clear()
        & (Join-Path $tests 'crap-tests.ps1')
        Assert-True ($global:LocalTestCalls.Count -eq 2 -and $global:LocalTestCalls[1].Contains('tool install')) 'Tool was not installed.'
        $global:LocalTestCalls.Clear()
        & (Join-Path $tests 'crap-tests.ps1')
        Assert-True ($global:LocalTestCalls.Count -eq 1) 'Pinned tool was needlessly reinstalled.'
        $global:LocalTestToolVersion = '5.4.0'; $global:LocalTestCalls.Clear()
        & (Join-Path $tests 'crap-tests.ps1')
        Assert-True ($global:LocalTestCalls[1].Contains('tool update')) 'Wrong tool version was not updated.'
        Assert-Fails { & (Join-Path $tests 'generate-test-reports.ps1') -NoOpen } 'install ReportGenerator first'
        $toolRoot = Join-Path $root '.tools/reportgenerator'
        New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
        Set-Content (Join-Path $toolRoot $(if ($IsWindows) { 'reportgenerator.exe' } else { 'reportgenerator' })) 'test fixture'
        Assert-Fails { & (Join-Path $tests 'generate-test-reports.ps1') -NoOpen } 'No OpenCover data found'

        if ($IsWindows) {
            $oldPreview = $env:IPIA_MAPPING_PREVIEW; $oldParity = $env:IPIA_PARITY_OUTPUT
            $global:LocalTestCalls.Clear(); $global:LocalTestFailure = 'build'
            Assert-Fails { & (Join-Path $tests 'smoke-tests.ps1') } 'Windows solution build failed with exit code 17'
            Assert-True ($global:LocalTestCalls.Count -eq 1) 'Smoke tests ran after a failed build.'
            Assert-True ($env:IPIA_MAPPING_PREVIEW -eq $oldPreview -and $env:IPIA_PARITY_OUTPUT -eq $oldParity) 'Smoke variables leaked.'
            $oldTemp = $env:RUNNER_TEMP; $oldCommit = $env:GITHUB_SHA
            Assert-Fails { & (Join-Path $tests 'release-candidate-tests.ps1') } 'Source commit failed with exit code 22'
            Assert-True ($env:RUNNER_TEMP -eq $oldTemp -and $env:GITHUB_SHA -eq $oldCommit) 'Release variables leaked.'
        } else {
            Assert-Fails { & (Join-Path $tests 'smoke-tests.ps1') } 'requires Windows'
            Assert-Fails { & (Join-Path $tests 'installer-helper-tests.ps1') } 'requires Windows'
            Assert-Fails { & (Join-Path $tests 'release-candidate-tests.ps1') } 'requires Windows'
        }
        Assert-True ((Get-Location).Path -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar)) 'Wrapper changed caller directory.'
    } finally { Pop-Location }
    Write-Output 'Local wrapper syntax, native failures, ordering, tool pinning, preconditions and directory restoration passed.'
} finally {
    foreach ($name in 'dotnet', 'py', 'node', 'git') { Remove-Item ('function:global:' + $name) -ErrorAction SilentlyContinue }
    foreach ($name in 'LocalTestCalls', 'LocalTestFailure', 'LocalTestToolVersion') { Remove-Variable $name -Scope Global -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
