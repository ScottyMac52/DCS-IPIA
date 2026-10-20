#Requires -Version 7.0
param([Parameter(Mandatory=$true)][string]$Version, [string]$Output = 'release-assets')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Version must contain four numeric components.' }
$taskRoot = Join-Path $env:RUNNER_TEMP ('heimdall-release-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $taskRoot 'publish'
$installed = Join-Path $taskRoot 'installed'
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$previousLocalAppData = $env:LOCALAPPDATA
$env:LOCALAPPDATA = Join-Path $taskRoot 'setup-preferences'
$setupSettings = Join-Path $env:LOCALAPPDATA 'HEIMDALL/appsettings.json'
$setupLogs = @()
$releaseFailure = $null
. (Join-Path $PSScriptRoot 'ReleaseTestCleanup.ps1')
function Assert-Exit([string]$Operation) { if ($LASTEXITCODE -ne 0) { throw "$Operation failed: $LASTEXITCODE" } }
function Test-App([string]$Executable, [string]$Name, [string]$ExistingData = '') {
    $data = if ($ExistingData) { $ExistingData } else { Join-Path $taskRoot $Name }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Remove-Item (Join-Path $data 'release-smoke.ok') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $data 'release-smoke.error') -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $Executable -ArgumentList @('--release-smoke', "`"$data`"") -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "$Name startup timed out." }
    if ($process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $data 'release-smoke.ok')) -or
        -not (Test-Path (Join-Path $data 'ipia.db'))) {
        $errorFile = Join-Path $data 'release-smoke.error'
        if (Test-Path $errorFile) { Write-Output (Get-Content $errorFile -Raw) }
        $diagnostics = Join-Path $data 'release-diagnostics'
        if (Test-Path $diagnostics) {
            $destination = Join-Path 'release-acceptance-diagnostics' $Name
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            Copy-Item (Join-Path $diagnostics '*') $destination -Recurse
        }
        throw "$Name published application verification failed."
    }
    if ((Get-Item $Executable).VersionInfo.FileVersion -ne $Version) { throw "$Name executable version differs." }
}
function Assert-ProfilePreservation {
    foreach ($file in $profileHashes.Keys) {
        if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file).Hash -ne $profileHashes[$file]) {
            throw "Install/uninstall changed profile data: $file"
        }
    }
}
try {
    dotnet publish src/Heimdall.App/Heimdall.App.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        "-p:Version=$Version" "-p:FileVersion=$Version" "-p:AssemblyVersion=$Version" -o $publish
    Assert-Exit 'Application publish'
    & (Join-Path $PSScriptRoot 'Write-DistributionNotices.ps1') -Publish $publish
    if (-not (Test-Path (Join-Path $publish 'notices/dependencies.json'))) { throw 'Release dependency inventory missing.' }
    Test-App (Join-Path $publish 'Heimdall.exe') 'portable-smoke'
    # Seed real SQLite and user files outside the install directory before testing upgrade/uninstall.
    $profileHashes = @{}
    foreach ($profile in @('HEIMDALL', 'HEIMDALL-TestProfiles/fresh-install', 'HEIMDALL-TestProfiles/second', 'DCS-IPIA', 'DCS-IPIA-TestProfiles/fresh-install', 'DCS-IPIA-TestProfiles/second')) {
        $profileRoot = Join-Path $env:LOCALAPPDATA $profile
        New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
        Copy-Item (Join-Path $taskRoot 'portable-smoke/*') $profileRoot -Recurse
        Remove-Item (Join-Path $profileRoot 'release-smoke.ok') -ErrorAction SilentlyContinue
        [IO.File]::WriteAllText((Join-Path $profileRoot 'onboarding-state.json'), '{"version":1,"completed":true}')
        Get-ChildItem $profileRoot -File -Recurse | ForEach-Object {
            $profileHashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash
        }
    }
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
    Test-App (Join-Path $installed 'Heimdall.exe') 'installed-smoke'
    Assert-ProfilePreservation
    Test-App (Join-Path $installed 'Heimdall.exe') 'upgrade-reopen' (Join-Path $env:LOCALAPPDATA 'HEIMDALL')
    Assert-ProfilePreservation
    Get-ChildItem $publish -File -Recurse | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($publish, $_.FullName)
        $copy = Join-Path $installed $relative
        if (-not (Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) {
            throw "Installed file differs: $relative"
        }
    }
    $uninstall = Start-Process (Join-Path $installed 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru
    if (-not $uninstall.WaitForExit(90000)) { $uninstall.Kill(); throw 'Uninstaller timed out.' }
    if ($uninstall.ExitCode -ne 0) { throw 'Uninstall verification failed.' }
    # The original process exits before Inno's temporary clone finishes deleting its files.
    Wait-ReleaseUninstall $installed
    if (Test-Path (Join-Path $installed 'Heimdall.exe')) { throw 'Uninstall verification failed.' }
    Assert-ProfilePreservation
    if ((Get-Content $setupSettings -Raw | ConvertFrom-Json).SavedGamesDcsFolder -ne $customFolder) {
        throw 'Uninstall did not preserve user settings.'
    }
    $setupLog = Join-Path $taskRoot 'reinstall.log'
    $setupLogs += $setupLog
    $reinstall = Start-Process $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
        "/LOG=`"$setupLog`"", "/DIR=`"$installed`"") -PassThru
    if (-not $reinstall.WaitForExit(90000)) { $reinstall.Kill(); throw 'Reinstall timed out.' }
    if ($reinstall.ExitCode -ne 0) { throw 'Reinstall failed.' }
    Assert-ProfilePreservation
    Test-App (Join-Path $installed 'Heimdall.exe') 'reinstall-reopen' (Join-Path $env:LOCALAPPDATA 'HEIMDALL')
    Assert-ProfilePreservation
    if ((Get-Content $setupSettings -Raw | ConvertFrom-Json).SavedGamesDcsFolder -ne $customFolder) {
        throw 'Reinstall reset user settings.'
    }
    $uninstall = Start-Process (Join-Path $installed 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru
    if (-not $uninstall.WaitForExit(90000)) { $uninstall.Kill(); throw 'Second uninstall timed out.' }
    if ($uninstall.ExitCode -ne 0) { throw 'Second uninstall failed.' }
    Wait-ReleaseUninstall $installed
    Assert-ProfilePreservation
    if (Test-Path $Output) { throw 'Release output already exists.' }
    New-Item -ItemType Directory -Path $Output | Out-Null
    Copy-Item $setup (Join-Path $Output 'setup.exe')
    $archiveName = "HEIMDALL_v${Version}_win-x64.zip"
    Compress-Archive -Path "$publish/*" -DestinationPath (Join-Path $Output $archiveName)
    # Publish readable review evidence for the exact candidate, alongside artifact checksums.
    @{
        schemaVersion=1; version=$Version; sourceCommit=$env:GITHUB_SHA; runtime='win-x64'
        dependencies=@(Get-Content (Join-Path $publish 'notices/dependencies.json') -Raw | ConvertFrom-Json)
        runtimePacks=@(Get-Content (Join-Path $publish 'notices/runtime-packs.json') -Raw | ConvertFrom-Json)
        artworkReview='notices/distribution-artwork-review.json'
        distributionDecision='pending: #183; packaging evidence is not permission or notice-completeness approval'
    } | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $Output 'DISTRIBUTION-EVIDENCE.json') -Encoding utf8NoBOM
    $extracted = Join-Path $taskRoot 'extracted'
    Expand-Archive (Join-Path $Output $archiveName) $extracted
    Test-App (Join-Path $extracted 'Heimdall.exe') 'archive-smoke'
    @{
        version=$Version; sourceCommit=$env:GITHUB_SHA; runtime='win-x64'; selfContained=$true
        checks=@('portable startup','installed startup','archive startup','isolated SQLite','bundled hardware integrity',
            'native PNG rendering','installed payload hashes','silent install/uninstall',
            'setup system-default folder','setup custom folder','setup rerun persistence',
            'production and two test-profile checksums across upgrade/uninstall/reinstall',
            'saved bundled/generic/custom module snapshot, PNG and native Lua behavior after installed/reinstalled reopen',
            'custom editable source and library persistence', 'privacy-scoped support capture from persisted release fixtures',
            'resolved transitive dependency metadata and available license files in exact payload')
        manualAcceptance='pending: real hardware/DCS, clean Windows, older-release upgrade, keyboard/scaling and rights review (#163/#183)'
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Output 'TEST-EVIDENCE.json') -Encoding utf8NoBOM
    $lines = Get-ChildItem $Output -File | Sort-Object Name | ForEach-Object {
        "$( (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() )  $($_.Name)"
    }
    [IO.File]::WriteAllText((Join-Path (Resolve-Path $Output).Path 'SHA256SUMS.txt'), ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    Write-Output 'Portable, installed and archived executables verified; install/uninstall and exact payload checks passed.'
} catch {
    $releaseFailure = $_
    foreach ($log in $setupLogs) {
        if (Test-Path -LiteralPath $log) {
            Write-Output "Installer log: $log"
            Get-Content -LiteralPath $log -Tail 120 | Write-Output
        }
    }
    throw
} finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    Remove-ReleaseTestDirectory $taskRoot -PreserveFailure:($null -ne $releaseFailure)
}

