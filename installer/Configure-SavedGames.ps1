param(
    [Parameter(Mandatory=$true)][ValidateSet('Read', 'Write')][string]$Mode,
    [string]$SettingsDirectory,
    [string]$DefaultsPath,
    [string]$OutputPath,
    [string]$Folder
)
$ErrorActionPreference = 'Stop'
if (-not $SettingsDirectory) {
    $current = Join-Path $env:LOCALAPPDATA 'HEIMDALL'
    $legacy = Join-Path $env:LOCALAPPDATA 'DCS-IPIA'
    # Match ProductDataPaths: a log directory alone cannot hide existing data.
    $SettingsDirectory = if ((Test-Path (Join-Path $current 'ipia.db')) -or -not (Test-Path $legacy)) { $current } else { $legacy }
}
$settingsPath = Join-Path $SettingsDirectory 'appsettings.json'
$legacyPath = Join-Path $SettingsDirectory 'user-settings.json'
function Read-Settings([string]$Path) {
    if ($Path -and (Test-Path -LiteralPath $Path)) {
        $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ($json -isnot [pscustomobject] -or $json.SchemaVersion -ne 1) {
            throw "Unsupported settings schema in $Path."
        }
        return $json
    }
}
function Test-AbsoluteFolder($Value) {
    return $Value -is [string] -and $Value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\]+\\[^\\]+)' -and
        $Value.IndexOfAny([IO.Path]::GetInvalidPathChars()) -lt 0
}
if ($Mode -eq 'Read') {
    # Current preferences take priority over the previous release and packaged defaults.
    foreach ($path in @($settingsPath, $legacyPath, $DefaultsPath)) {
        try { $json = Read-Settings $path } catch { $json = $null }
        if ($json -and (Test-AbsoluteFolder $json.SavedGamesDcsFolder)) {
            $Folder = $json.SavedGamesDcsFolder
            break
        }
    }
    if (-not $Folder) {
        # Windows stores the redirected Saved Games known-folder location under this GUID.
        $key = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
        $savedGames = $key.'{4C5C32FF-BB9D-43b0-BF56-DFB24C4E478A}'
        if (-not $savedGames) { $savedGames = Join-Path $env:USERPROFILE 'Saved Games' }
        $Folder = Join-Path ([Environment]::ExpandEnvironmentVariables($savedGames)) 'DCS'
    }
    if (-not $OutputPath) { throw 'Read requires OutputPath.' }
    [IO.File]::WriteAllText($OutputPath, $Folder, [Text.UTF8Encoding]::new($true))
    return
}
if (-not (Test-AbsoluteFolder $Folder)) { throw 'Choose an absolute DCS Saved Games instance folder.' }
$Folder = [IO.Path]::GetFullPath($Folder)
# Refuse to overwrite corrupt/future preferences, and preserve all unrelated properties.
$json = Read-Settings $settingsPath
if (-not $json) { $json = Read-Settings $legacyPath }
if (-not $json) { $json = [pscustomobject]@{ SchemaVersion = 1 } }
$json | Add-Member -NotePropertyName SavedGamesDcsFolder -NotePropertyValue $Folder -Force
New-Item -ItemType Directory -Path $SettingsDirectory -Force | Out-Null
$temp = $settingsPath + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($temp, ($json | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $settingsPath) {
        # Windows PowerShell coerces $null to an empty string for .NET string arguments.
        # File.Replace requires a true null when no backup file is requested.
        [IO.File]::Replace($temp, $settingsPath, [NullString]::Value)
    }
    else { [IO.File]::Move($temp, $settingsPath) }
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
