$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../../scripts/Write-RuntimePackNotices.ps1'
$taskRoot = Join-Path ([IO.Path]::GetTempPath()) ('ipia-runtime-notices-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$script:count = 0
function New-Fixture {
    $root = Join-Path $taskRoot ([guid]::NewGuid().ToString('N'))
    $cache = Join-Path $root 'cache'
    $sdk = Join-Path $root 'sdk'
    $notices = Join-Path $root 'notices'
    foreach ($path in @($cache, $sdk, $notices)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
    # Deliberately unrelated SDK notices must never be substituted for runtime notices.
    Set-Content (Join-Path $sdk 'LICENSE.txt') 'wrong SDK license'
    Set-Content (Join-Path $sdk 'ThirdPartyNotices.txt') 'wrong SDK notices'
    $dependencies = @()
    foreach ($name in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
        $pack = Join-Path $cache ($name.ToLowerInvariant() + '/10.0.12')
        New-Item -ItemType Directory -Path $pack -Force | Out-Null
        Set-Content (Join-Path $pack 'LICENSE') ("exact " + $name + " license")
        if ($name -like '*NETCore*') { Set-Content (Join-Path $pack 'THIRD-PARTY-NOTICES.TXT') 'exact core notices' }
        $dependencies += @{ name=$name; version='[10.0.12, 10.0.12]' }
    }
    return @{
        root=$root; cache=$cache; sdk=$sdk; notices=$notices
        assets=@{ packageFolders=@{ $cache=@{} }; project=@{ frameworks=@{ net10=@{ downloadDependencies=$dependencies } } } }
    }
}
function Invoke-Fixture($Fixture) {
    & $scriptPath -ResolvedAssets $Fixture.assets -Notices $Fixture.notices -DotnetRoot $Fixture.sdk | Out-Null
}
function Assert-Fails($Fixture, [string]$Expected) {
    try { Invoke-Fixture $Fixture } catch {
        if ($_.Exception.Message -notlike ("*" + $Expected + "*")) { throw }
        $script:count++
        return
    }
    throw "Expected failure: $Expected"
}
try {
    $f = New-Fixture
    # Multiple frameworks referencing the same packs must not duplicate the inventory.
    $f.assets.project.frameworks.other = $f.assets.project.frameworks.net10
    Invoke-Fixture $f
    $inventory = @(Get-Content (Join-Path $f.notices 'runtime-packs.json') -Raw | ConvertFrom-Json)
    if ($inventory.Count -ne 2) { throw 'Runtime pack deduplication failed.' }
    foreach ($pack in $inventory) {
        foreach ($file in $pack.noticeFiles) {
            $output = Join-Path $f.notices $file.path
            if ((Get-Content $output -Raw) -like '*wrong SDK*') { throw 'SDK notices substituted.' }
            if ((Get-FileHash $output -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) {
                throw 'Pack notice hash differs.'
            }
        }
    }
    if (@($inventory | Where-Object { $_.package -like '*WindowsDesktop*' })[0].noticeFiles.Count -ne 1) {
        throw 'Desktop pack license-only layout was not preserved.'
    }
    $script:count++
    $f = New-Fixture
    $f.assets.project.frameworks.net10.downloadDependencies = @()
    Assert-Fails $f 'runtime pack missing'
    $f = New-Fixture
    $f.assets.project.frameworks.net10.downloadDependencies[0].version = '[10.0.0, 11.0.0)'
    Assert-Fails $f 'version is not exact'
    $f = New-Fixture
    $f.assets.project.frameworks.other = @{ downloadDependencies=@(@{
        name='Microsoft.NETCore.App.Runtime.win-x64'; version='[10.0.11, 10.0.11]'
    }) }
    Assert-Fails $f 'Conflicting runtime pack versions'
    $f = New-Fixture
    Remove-Item (Join-Path $f.cache 'microsoft.netcore.app.runtime.win-x64/10.0.12') -Recurse
    Assert-Fails $f 'Exact runtime pack unavailable'
    $f = New-Fixture
    Remove-Item (Join-Path $f.cache 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.12/LICENSE')
    Assert-Fails $f 'Runtime pack license missing'
    $f = New-Fixture
    Remove-Item (Join-Path $f.cache 'microsoft.netcore.app.runtime.win-x64/10.0.12/THIRD-PARTY-NOTICES.TXT')
    Assert-Fails $f 'Core runtime third-party notices missing'
    $f = New-Fixture
    foreach ($name in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
        $destination = Join-Path $f.sdk ('packs/' + $name + '/10.0.12')
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Move-Item (Join-Path $f.cache ($name.ToLowerInvariant() + '/10.0.12')) $destination
    }
    Invoke-Fixture $f
    $script:count++
    Write-Output "Runtime notice regression scenarios passed: $script:count"
} finally { Remove-Item -LiteralPath $taskRoot -Recurse -Force }
