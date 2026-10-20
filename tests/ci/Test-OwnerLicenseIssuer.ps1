$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path ([IO.Path]::GetTempPath()) ('ipia-issuer-' + [guid]::NewGuid().ToString('N'))
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$key = [Security.Cryptography.RSA]::Create(2048)
$wrong = [Security.Cryptography.RSA]::Create(2048)
try {
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts'), (Join-Path $root 'src/Heimdall.Application') -Force | Out-Null
    $script = Join-Path $root 'scripts/Issue-OwnerLicense.ps1'
    Copy-Item (Join-Path $repo 'scripts/Issue-OwnerLicense.ps1') $script
    [IO.File]::WriteAllText((Join-Path $root 'src/Heimdall.Application/OwnerLicensePublicKey.cs'), $key.ExportSubjectPublicKeyInfoPem())
    $private = Join-Path $root 'test-key.pem'
    [IO.File]::WriteAllText($private, $key.ExportPkcs8PrivateKeyPem())
    $license = Join-Path $root 'test-license.json'
    & $script -SigningKeyPath $private -OutputPath $license -OwnerName 'Isolated issuer test'
    $document = Get-Content -LiteralPath $license -Raw | ConvertFrom-Json
    $payload = [Convert]::FromBase64String($document.Payload)
    if (-not $key.VerifyData($payload, [Convert]::FromBase64String($document.Signature), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)) { throw 'Issuer signature is invalid.' }
    $claims = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    if ($claims.Product -ne 'HEIMDALL' -or $claims.Edition -ne 'Owner' -or $claims.SchemaVersion -ne 1 -or $claims.Owner -ne 'Isolated issuer test' -or [guid]$claims.LicenseId -eq [guid]::Empty) { throw 'Issuer claims are invalid.' }
    $preserved = [IO.File]::ReadAllText($license)
    $rejected = $false
    try { & $script -SigningKeyPath $private -OutputPath $license } catch { $rejected = $true }
    if (-not $rejected -or [IO.File]::ReadAllText($license) -ne $preserved) { throw 'Existing issuance was overwritten.' }
    [IO.File]::WriteAllText($private, $wrong.ExportPkcs8PrivateKeyPem())
    $rejected = $false
    $invalid = Join-Path $root 'invalid-license.json'
    try { & $script -SigningKeyPath $private -OutputPath $invalid } catch { $rejected = $true }
    if (-not $rejected -or (Test-Path -LiteralPath $invalid)) { throw 'Wrong-key issuance was accepted.' }
    Write-Host 'Owner issuer signing, claims, overwrite protection and key mismatch checks passed with ephemeral keys.'
}
finally { $key.Dispose(); $wrong.Dispose(); Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
