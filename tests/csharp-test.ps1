#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalTestSupport.ps1')
Invoke-LocalTestSuite {
    Invoke-LocalTestCommand 'C# layout' { py -3.12 scripts/check_csharp_layout.py }
    Invoke-LocalTestCommand 'Documentation' { py -3.12 scripts/check_documentation.py }
    Invoke-LocalTestCommand 'Theme resources' { py -3.12 scripts/check_theme_resources.py }
    Invoke-LocalTestCommand 'Common inventory' { py -3.12 scripts/check_common_migration.py --check }
    Invoke-LocalTestCommand 'Migration tests' { py -3.12 -m unittest discover -s tests/migration -p 'test_*.py' }
    Invoke-LocalTestCommand 'CI helper tests' { py -3.12 -m unittest discover -s tests/ci -p 'test_*.py' }
}
