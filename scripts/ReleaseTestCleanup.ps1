# Release-test helpers: Inno's temporary uninstaller clone may outlive its launcher.
function Wait-ReleaseUninstall([string]$Directory, [double]$TimeoutSeconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ((Test-Path -LiteralPath (Join-Path $Directory 'unins000.dat')) -or
           (Test-Path -LiteralPath (Join-Path $Directory 'unins000.exe'))) {
        if ([DateTime]::UtcNow -ge $deadline) { throw "Uninstaller files remain after $TimeoutSeconds seconds: $Directory" }
        Start-Sleep -Milliseconds 250
    }
}
function Remove-ReleaseTestDirectory([string]$Directory, [double]$TimeoutSeconds = 30, [switch]$PreserveFailure) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (Test-Path -LiteralPath $Directory) {
        try {
            Remove-Item -LiteralPath $Directory -Recurse -Force -ErrorAction Stop
            return
        } catch {
            # Retry transient Windows locks, but do not hide persistent failures.
            $retryable = $_.Exception -is [IO.IOException] -or $_.Exception -is [UnauthorizedAccessException]
            if (-not $retryable -or [DateTime]::UtcNow -ge $deadline) {
                if ($PreserveFailure) {
                    Write-Warning "Release test cleanup failed; preserving the original failure. $($_.Exception.Message)"
                    return
                }
                throw
            }
            Start-Sleep -Milliseconds 250
        }
    }
}
