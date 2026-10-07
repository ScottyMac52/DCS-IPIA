$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot '../../installer/Configure-SavedGames.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('ipia-setup-settings-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$output = Join-Path $root 'selected.txt'
$settings = Join-Path $root 'appsettings.json'
$legacy = Join-Path $root 'user-settings.json'
function Assert-Equal($Expected, $Actual) {
    if ($Expected -ne $Actual) { throw "Expected '$Expected', got '$Actual'." }
}
function Read-Selection {
    & $helper -Mode Read -SettingsDirectory $root -OutputPath $output
    [IO.File]::ReadAllText($output)
}
try {
    $default = Read-Selection
    if (-not [IO.Path]::IsPathRooted($default) -or (Split-Path $default -Leaf) -ne 'DCS') { throw 'No system default.' }
    $old = Join-Path $root 'DCS legacy'
    @{ SchemaVersion=1; Theme='Dark'; SavedGamesDcsFolder=$old; Custom=@{ Enabled=$true } } |
        ConvertTo-Json -Depth 8 | Set-Content $legacy -Encoding UTF8
    Assert-Equal $old (Read-Selection)
    $selected = Join-Path $root 'DCS custom & pilot'
    & $helper -Mode Write -SettingsDirectory $root -Folder $selected
    Assert-Equal $selected (Read-Selection)
    $json = Get-Content $settings -Raw | ConvertFrom-Json
    Assert-Equal 'Dark' $json.Theme
    Assert-Equal $true $json.Custom.Enabled
    Assert-Equal $old ((Get-Content $legacy -Raw | ConvertFrom-Json).SavedGamesDcsFolder)
    $changed = Join-Path $root 'DCS rerun'
    & $helper -Mode Write -SettingsDirectory $root -Folder $changed
    Assert-Equal $changed (Read-Selection)
    Assert-Equal $true ((Get-Content $settings -Raw | ConvertFrom-Json).Custom.Enabled)
    $failed = $false
    try { & $helper -Mode Write -SettingsDirectory $root -Folder 'relative' } catch { $failed = $true }
    if (-not $failed) { throw 'Relative path was accepted.' }
    Assert-Equal $changed (Read-Selection)
    [IO.File]::WriteAllText($settings, '{broken')
    Assert-Equal $old (Read-Selection)
    $failed = $false
    try { & $helper -Mode Write -SettingsDirectory $root -Folder $changed } catch { $failed = $true }
    if (-not $failed) { throw 'Malformed preferences were overwritten.' }
    Assert-Equal '{broken' ([IO.File]::ReadAllText($settings))
    Write-Output 'Installer default, legacy/current precedence, rerun persistence, preservation and failure checks passed.'
} finally { Remove-Item $root -Recurse -Force }
