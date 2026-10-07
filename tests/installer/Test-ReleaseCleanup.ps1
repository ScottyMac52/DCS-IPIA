$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/ReleaseTestCleanup.ps1')
if (-not $IsWindows) { throw 'These regression tests require Windows file-sharing semantics.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('ipia-cleanup-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$job = $null
$stream = $null
try {
    # A real competing process holds the uninstall log, then finishes deletion.
    $installed = Join-Path $root 'installed'
    New-Item -ItemType Directory -Path $installed | Out-Null
    $job = Start-Job -ArgumentList $installed -ScriptBlock {
        param($directory)
        $ErrorActionPreference = 'Stop'
        $log = Join-Path $directory 'unins000.dat'
        $handle = [IO.File]::Open($log, 'Create', 'ReadWrite', 'None')
        try {
            Set-Content (Join-Path $directory 'unins000.exe') 'test fixture'
            Set-Content (Join-Path $directory 'ready') 'ready'
            Start-Sleep -Milliseconds 1000
        } finally { $handle.Dispose() }
        Remove-Item -LiteralPath $log
        Remove-Item -LiteralPath (Join-Path $directory 'unins000.exe')
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (Test-Path (Join-Path $installed 'ready'))) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Lock fixture did not start.' }
        Start-Sleep -Milliseconds 50
    }
    Wait-ReleaseUninstall $installed -TimeoutSeconds 10
    $job | Wait-Job | Receive-Job -ErrorAction Stop
    $job | Remove-Job
    $job = $null

    # Cleanup retries an actual sharing violation until the holder exits.
    $cleanup = Join-Path $root 'cleanup'
    New-Item -ItemType Directory -Path $cleanup | Out-Null
    $job = Start-Job -ArgumentList $cleanup -ScriptBlock {
        param($directory)
        $ErrorActionPreference = 'Stop'
        $handle = [IO.File]::Open((Join-Path $directory 'unins000.dat'), 'Create', 'ReadWrite', 'None')
        try {
            Set-Content (Join-Path $directory 'ready') 'ready'
            Start-Sleep -Milliseconds 1000
        } finally { $handle.Dispose() }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (Test-Path (Join-Path $cleanup 'ready'))) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Cleanup lock fixture did not start.' }
        Start-Sleep -Milliseconds 50
    }
    Remove-ReleaseTestDirectory $cleanup -TimeoutSeconds 10
    if (Test-Path $cleanup) { throw 'Retrying cleanup did not remove its directory.' }
    $job | Wait-Job | Receive-Job -ErrorAction Stop
    $job | Remove-Job
    $job = $null

    # Persistent locks must fail, and finally must preserve an earlier test error.
    $blocked = Join-Path $root 'blocked'
    New-Item -ItemType Directory -Path $blocked | Out-Null
    $stream = [IO.File]::Open((Join-Path $blocked 'unins000.dat'), 'Create', 'ReadWrite', 'None')
    $failed = $false
    try { Wait-ReleaseUninstall $blocked -TimeoutSeconds 0 } catch { $failed = $true }
    if (-not $failed) { throw 'Uninstall verification accepted a remaining log.' }
    $failed = $false
    try { Remove-ReleaseTestDirectory $blocked -TimeoutSeconds 0 } catch { $failed = $true }
    if (-not $failed) { throw 'Cleanup silently accepted a persistent lock.' }
    $original = $null
    try {
        try { throw 'Original release verification failure' }
        finally { Remove-ReleaseTestDirectory $blocked -TimeoutSeconds 0 -PreserveFailure }
    } catch { $original = $_.Exception.Message }
    if ($original -ne 'Original release verification failure') { throw 'Cleanup masked the original failure.' }
    $stream.Dispose()
    $stream = $null
    Remove-ReleaseTestDirectory $blocked
    Remove-ReleaseTestDirectory (Join-Path $root 'already-absent')
    Write-Output 'Uninstaller completion, delayed locks, persistent locks and original failure preservation passed.'
} finally {
    if ($stream) { $stream.Dispose() }
    if ($job) { $job | Stop-Job; $job | Remove-Job -Force }
    Remove-ReleaseTestDirectory $root
}
