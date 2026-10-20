param(
    [Parameter(Mandatory=$true)][string]$Publish,
    [string]$Assets = 'src/Heimdall.App/obj/project.assets.json'
)
$ErrorActionPreference = 'Stop'
$notices = Join-Path $Publish 'notices'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
$copies = @{
    'LICENSE' = 'HEIMDALL-LICENSE.txt'
    'src/Heimdall.App/Branding/PROVENANCE.md' = 'heimdall-PROVENANCE.md'
    'src/Heimdall.App/Branding/FreeEdition/PROVENANCE.md' = 'free-edition-background-PROVENANCE.md'
    'data/shared-assets/DCS-Common-LICENSE.txt' = 'DCS-Common-LICENSE.txt'
    'data/shared-assets/provenance.json' = 'shared-assets-provenance.json'
    'data/shared-assets/assets/shared/hardware/SOURCES.md' = 'hardware-SOURCES.md'
    'scripts/asset-authoring/PROVENANCE.json' = 'asset-authoring-provenance.json'
    'src/Heimdall.Rendering/Fonts/DejaVuSans-HEIMDALL.LICENSE.txt' = 'DejaVuSans-HEIMDALL.LICENSE.txt'
    'src/Heimdall.Rendering/Fonts/PROVENANCE.md' = 'font-PROVENANCE.md'
    'docs/distribution-readiness.md' = 'distribution-readiness.md'
    'docs/release-acceptance.md' = 'release-acceptance.md'
    'docs/logging.md' = 'logging.md'
    'docs/distribution-review.md' = 'distribution-review.md'
    'docs/distribution-artwork-review.json' = 'distribution-artwork-review.json'
}
foreach ($entry in $copies.GetEnumerator()) { Copy-Item -LiteralPath $entry.Key -Destination (Join-Path $notices $entry.Value) }
$resolved = Get-Content -LiteralPath $Assets -Raw | ConvertFrom-Json -AsHashtable
$inventory = @()
foreach ($key in ($resolved.libraries.Keys | Sort-Object)) {
    $library = $resolved.libraries[$key]
    if ($library.type -ne 'package') { continue }
    $package = $null
    foreach ($cache in $resolved.packageFolders.Keys) {
        $candidate = Join-Path $cache $library.path
        if (Test-Path -LiteralPath $candidate) { $package = $candidate; break }
    }
    if ($null -eq $package) { throw "Restored package metadata not found: $key" }
    $specs = @(Get-ChildItem -LiteralPath $package -Filter '*.nuspec' -File)
    if ($specs.Count -ne 1) { throw "Package metadata ambiguous: $key" }
    [xml]$spec = Get-Content -LiteralPath $specs[0].FullName -Raw
    $metadata = $spec.package.metadata
    $license = $metadata.license
    $destination = Join-Path $notices ('packages/' + $key.Replace('/', '-'))
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath $specs[0].FullName -Destination $destination
    $licenseText = if ($license) { $license.InnerText } else { $null }
    $licenseType = if ($license) { $license.GetAttribute('type') } else { $null }
    $files = @(Get-ChildItem -LiteralPath $package -File -Recurse | Where-Object {
        $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD[._-]?PARTY[._-]?NOTICES)([._-].*)?$'
    })
    if ($licenseType -eq 'file') {
        $target = [IO.Path]::GetFullPath((Join-Path $package $licenseText))
        if (-not $target.StartsWith([IO.Path]::GetFullPath($package) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Invalid package license file: $key" }
        $files += Get-Item -LiteralPath $target
    }
    $copied = @()
    foreach ($file in ($files | Sort-Object FullName -Unique)) {
        $relative = [IO.Path]::GetRelativePath($package, $file.FullName)
        $output = Join-Path $destination $relative
        New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $output
        $copied += $relative.Replace('\', '/')
    }
    $inventory += @{
        package=$key; scope='restored application graph (includes build dependencies)'
        licenseType=$licenseType; license=$licenseText; licenseUrl=[string]$metadata.licenseUrl
        projectUrl=[string]$metadata.projectUrl; copyright=[string]$metadata.copyright
        noticeFiles=$copied; reviewRequired=($null -eq $license -or $copied.Count -eq 0)
    }
}
if ($inventory.Count -eq 0) { throw 'Resolved application dependency inventory is empty.' }
$inventory | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $notices 'dependencies.json') -Encoding utf8NoBOM
& (Join-Path $PSScriptRoot 'Write-RuntimePackNotices.ps1') -ResolvedAssets $resolved -Notices $notices
@{
    sdk=(dotnet --version); sourceCommit=$env:GITHUB_SHA
    runtimeNoticeScope='Exact resolved Microsoft.NETCore.App and Microsoft.WindowsDesktop.App win-x64 runtime packs; see runtime-packs.json'
    unresolvedDecisions='https://github.com/ScottyMac52/DCS-IPIA/issues/183'
} | ConvertTo-Json | Set-Content (Join-Path $notices 'build-notices.json') -Encoding utf8NoBOM
Write-Output "Distribution inventory: $($inventory.Count) resolved application packages plus artwork/font/.NET evidence."

