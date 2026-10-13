#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Invoke-LocalTestCommand 'Asset exporter tests' {
        node --test --experimental-test-coverage `
            --test-coverage-include='scripts/asset-authoring/*.mjs' `
            --test-reporter=spec --test-reporter-destination=stdout `
            --test-reporter=lcov --test-reporter-destination=asset-authoring-coverage.info `
            tests/asset-authoring/*.test.mjs
    }
    Invoke-LocalTestCommand 'Independent exporter 80/80 gates' { py -3.12 scripts/check_asset_coverage.py asset-authoring-coverage.info }
}
