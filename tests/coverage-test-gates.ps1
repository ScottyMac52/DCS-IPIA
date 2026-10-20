#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    $project = 'tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj'
    $include = '[Dcs.Ipia.Domain]*%2c[Dcs.Ipia.Application]*%2c[Dcs.Ipia.Infrastructure]*%2c[Dcs.Ipia.Dcs]*%2c[Dcs.Ipia.EliteDangerous]*%2c[Dcs.Ipia.Rendering]*%2c[Dcs.Ipia.Packaging]*'
    $coverage = Join-Path $PWD 'TestResults/raw/dotnet/coverage'
    Invoke-LocalTestCommand 'Restore' { dotnet restore $project }
    Invoke-LocalTestCommand 'Formatting' { dotnet format style $project --verify-no-changes --no-restore --severity warn }
    Invoke-LocalTestCommand 'Test build' { dotnet build $project -c Release --no-restore }
    Invoke-LocalTestCommand 'Tests and independent 80/80 gates' {
        dotnet test $project -c Release --no-build `
            --logger trx --results-directory TestResults/raw/dotnet `
            /p:CollectCoverage=true "/p:CoverletOutputFormat=cobertura%2copencover" `
            "/p:CoverletOutput=$coverage" "/p:Threshold=80%2c80" `
            "/p:ThresholdType=line%2cbranch" /p:ThresholdStat=minimum "/p:Include=$include"
    }
}
