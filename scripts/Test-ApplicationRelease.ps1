param([Parameter(Mandatory=$true)][string]$Version, [string]$Output = 'release-assets')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Version must contain four numeric components.' }
$taskRoot = Join-Path $env:RUNNER_TEMP ('ipia-release-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $taskRoot 'publish'
$installed = Join-Path $taskRoot 'installed'
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$previousLocalAppData = $env:LOCALAPPDATA
$env:LOCALAPPDATA = Join-Path $taskRoot 'setup-preferences'
$setupSettings = Join-Path $env:LOCALAPPDATA 'DCS-IPIA/appsettings.json'
$setupLogs = @()
function Assert-Exit([string]$Operation) { if ($LASTEXITCODE -ne 0) { throw "$Operation failed: $LASTEXITCODE" } }
function Test-App([string]$Executable, [string]$Name) {
    $data = Join-Path $taskRoot $Name
    New-Item -ItemType Directory -Path $data | Out-Null
    $process = Start-Process -FilePath $Executable -ArgumentList @('--release-smoke', "`"$data`"") -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "$Name startup timed out." }
    if ($process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $data 'release-smoke.ok')) -or
        -not (Test-Path (Join-Path $data 'ipia.db'))) {
        $errorFile = Join-Path $data 'release-smoke.error'
        if (Test-Path $errorFile) { Write-Output (Get-Content $errorFile -Raw) }
        throw "$Name published application verification failed."
    }
    if ((Get-Item $Executable).VersionInfo.FileVersion -ne $Version) { throw "$Name executable version differs." }
}
try {
    dotnet publish src/Dcs.Ipia.App/Dcs.Ipia.App.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        "-p:Version=$Version" "-p:FileVersion=$Version" "-p:AssemblyVersion=$Version" -o $publish
    Assert-Exit 'Application publish'
    Test-App (Join-Path $publish 'Dcs.Ipia.App.exe') 'portable-smoke'
    $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (-not (Test-Path $iscc)) { choco install innosetup -y --no-progress; Assert-Exit 'Inno Setup installation' }
    & $iscc "/DAppVersion=$Version" "/DPublishDir=$publish" installer/installer.iss
    Assert-Exit 'Installer build'
    $setup = (Resolve-Path installer/output/setup.exe).Path
    $setupLog = Join-Path $taskRoot 'install.log'
    $setupLogs += $setupLog
    $install = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/LOG=`"$setupLog`"", "/DIR=`"$installed`"") -PassThru
    if (-not $install.WaitForExit(90000)) { $install.Kill(); throw 'Installer timed out.' }
    if ($install.ExitCode -ne 0) { throw 'Silent installation failed.' }
    $initialFolder = (Get-Content $setupSettings -Raw | ConvertFrom-Json).SavedGamesDcsFolder
    if (-not [IO.Path]::IsPathFullyQualified($initialFolder) -or (Split-Path $initialFolder -Leaf) -ne 'DCS') {
        throw 'Setup did not persist a system-default DCS folder.'
    }
    $customFolder = Join-Path $taskRoot 'DCS custom instance'
    $setupLog = Join-Path $taskRoot 'custom-folder-rerun.log'
    $setupLogs += $setupLog
    $rerun = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/LOG=`"$setupLog`"",
        "/DIR=`"$installed`"", "/SAVEDGAMES=`"$customFolder`"") -PassThru
    if (-not $rerun.WaitForExit(90000)) { $rerun.Kill(); throw 'Setup rerun timed out.' }
    if ($rerun.ExitCode -ne 0 -or (Get-Content $setupSettings -Raw | ConvertFrom-Json).SavedGamesDcsFolder -ne $customFolder) {
        throw 'Setup did not persist the chosen DCS folder.'
    }
    $setupLog = Join-Path $taskRoot 'persistence-rerun.log'
    $setupLogs += $setupLog
    $rerun = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/LOG=`"$setupLog`"", "/DIR=`"$installed`"") -PassThru
    if (-not $rerun.WaitForExit(90000)) { $rerun.Kill(); throw 'Setup persistence rerun timed out.' }
    if ($rerun.ExitCode -ne 0 -or (Get-Content $setupSettings -Raw | ConvertFrom-Json).SavedGamesDcsFolder -ne $customFolder) {
        throw 'Setup rerun reset the assigned DCS folder.'
    }
    Test-App (Join-Path $installed 'Dcs.Ipia.App.exe') 'installed-smoke'
    Get-ChildItem $publish -File -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($publish, $_.FullName)
        $copy = Join-Path $installed $relative
        if (-not (Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) {
            throw "Installed file differs: $relative"
        }
    }
    $uninstall = Start-Process (Join-Path $installed 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru
    if (-not $uninstall.WaitForExit(90000)) { $uninstall.Kill(); throw 'Uninstaller timed out.' }
    if ($uninstall.ExitCode -ne 0 -or (Test-Path (Join-Path $installed 'Dcs.Ipia.App.exe'))) { throw 'Uninstall verification failed.' }
    if (Test-Path $Output) { throw 'Release output already exists.' }
    New-Item -ItemType Directory -Path $Output | Out-Null
    Copy-Item $setup (Join-Path $Output 'setup.exe')
    $archiveName = "DCS-IPIA_v${Version}_win-x64.zip"
    Compress-Archive -Path "$publish/*" -DestinationPath (Join-Path $Output $archiveName)
    $extracted = Join-Path $taskRoot 'extracted'
    Expand-Archive (Join-Path $Output $archiveName) $extracted
    Test-App (Join-Path $extracted 'Dcs.Ipia.App.exe') 'archive-smoke'
    @{
        version=$Version; sourceCommit=$env:GITHUB_SHA; runtime='win-x64'; selfContained=$true
        checks=@('portable startup','installed startup','archive startup','isolated SQLite','bundled hardware integrity',
            'native PNG rendering','installed payload hashes','silent install/uninstall',
            'setup system-default folder','setup custom folder','setup rerun persistence')
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Output 'TEST-EVIDENCE.json') -Encoding utf8NoBOM
    $lines = Get-ChildItem $Output -File | Sort-Object Name | ForEach-Object {
        "$( (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() )  $($_.Name)"
    }
    [IO.File]::WriteAllText((Join-Path (Resolve-Path $Output).Path 'SHA256SUMS.txt'), ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Write-Output 'Portable, installed and archived executables verified; install/uninstall and exact payload checks passed.'
} catch {
    foreach ($log in $setupLogs) {
        if (Test-Path -LiteralPath $log) {
            Write-Output "Installer log: $log"
            Get-Content -LiteralPath $log -Tail 120 | Write-Output
        }
    }
    throw
} finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    if (Test-Path $taskRoot) { Remove-Item $taskRoot -Recurse -Force }
}
