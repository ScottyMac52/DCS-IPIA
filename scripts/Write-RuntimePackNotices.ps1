param(
    [Parameter(Mandatory=$true)][hashtable]$ResolvedAssets,
    [Parameter(Mandatory=$true)][string]$Notices,
    [string]$DotnetRoot = ''
)
$ErrorActionPreference = 'Stop'
# Runtime packs are downloadDependencies, not ordinary libraries in project.assets.json.
$packs = @{}
foreach ($framework in $ResolvedAssets.project.frameworks.Values) {
    foreach ($dependency in $framework.downloadDependencies) {
        if ($dependency.name -notmatch '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$') { continue }
        if ($dependency.version -notmatch '^\[([^,\]]+),\s*([^\]]+)\]$' -or $Matches[1] -ne $Matches[2]) {
            throw "Runtime pack version is not exact: $($dependency.name) $($dependency.version)"
        }
        $version = $Matches[1]
        if ($packs.ContainsKey($dependency.name) -and $packs[$dependency.name] -ne $version) {
            throw "Conflicting runtime pack versions: $($dependency.name)"
        }
        $packs[$dependency.name] = $version
    }
}
foreach ($required in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    if (-not $packs.ContainsKey($required)) { throw "Resolved self-contained runtime pack missing: $required" }
}
if (-not $DotnetRoot) { $DotnetRoot = Split-Path (Get-Command dotnet).Source -Parent }
$inventory = @()
foreach ($name in ($packs.Keys | Sort-Object)) {
    $version = $packs[$name]
    $candidates = @($ResolvedAssets.packageFolders.Keys | ForEach-Object {
        Join-Path $_ ($name.ToLowerInvariant() + '/' + $version.ToLowerInvariant())
    })
    $candidates += Join-Path $DotnetRoot ('packs/' + $name + '/' + $version)
    $package = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
    if (-not $package) { throw "Exact runtime pack unavailable: $name/$version" }
    $files = @(Get-ChildItem -LiteralPath $package -File | Where-Object {
        $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD[._-]?PARTY[._-]?NOTICES)([._-].*)?$'
    })
    if (-not ($files | Where-Object { $_.Name -match '^LICENSE([._-].*)?$' })) {
        throw "Runtime pack license missing: $name/$version"
    }
    if ($name -eq 'Microsoft.NETCore.App.Runtime.win-x64' -and
        -not ($files | Where-Object { $_.Name -match '^THIRD[._-]?PARTY[._-]?NOTICES([._-].*)?$' })) {
        throw "Core runtime third-party notices missing: $name/$version"
    }
    $destination = Join-Path $Notices ('runtime-packs/' + $name + '-' + $version)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $copied = @()
    foreach ($file in ($files | Sort-Object Name)) {
        $output = Join-Path $destination $file.Name
        Copy-Item -LiteralPath $file.FullName -Destination $output
        $copied += @{
            path=[IO.Path]::GetRelativePath($Notices, $output).Replace('\', '/')
            sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $inventory += @{ package="$name/$version"; scope='resolved win-x64 self-contained runtime pack'; noticeFiles=$copied }
}
ConvertTo-Json -InputObject @($inventory) -Depth 6 |
    Set-Content (Join-Path $Notices 'runtime-packs.json') -Encoding utf8NoBOM
Write-Output "Exact runtime pack notices: $($inventory.Count) packs."
