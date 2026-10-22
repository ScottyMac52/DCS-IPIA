param(
    [Parameter(Mandatory)][string]$SigningKeyPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$OwnerName = 'Scott McIntosh',
    [ValidateSet('Owner', 'Premium')][string]$Edition = 'Owner'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OwnerName) -or $OwnerName.Length -gt 120) { throw 'Owner name must contain 1–120 characters.' }
if (Test-Path -LiteralPath $OutputPath) { throw 'Choose a new output file; existing licenses are not overwritten.' }
$issuer = [System.Security.Cryptography.RSA]::Create()
$verifier = [System.Security.Cryptography.RSA]::Create()
try {
    $issuer.ImportFromPem([System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKeyPath)))
    $source = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot '../src/Heimdall.Application/OwnerLicensePublicKey.cs'))
    $publicPem = [regex]::Match($source, '(?s)-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----').Value
    $verifier.ImportFromPem($publicPem)
    $claims = [ordered]@{ SchemaVersion = 1; Product = 'HEIMDALL'; Edition = $Edition; LicenseId = [guid]::NewGuid().ToString(); Owner = $OwnerName }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress))
    $signature = $issuer.SignData($bytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)
    if (-not $verifier.VerifyData($bytes, $signature, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pss)) {
        throw 'Signing key does not match this application. Keep the original recovery key; key rotation requires a reviewed application update.'
    }
    $envelope = [ordered]@{ Payload = [Convert]::ToBase64String($bytes); Signature = [Convert]::ToBase64String($signature) }
    $destination = [System.IO.Path]::GetFullPath($OutputPath)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    [System.IO.File]::WriteAllText($destination, ($envelope | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Signed $Edition license written to $destination. Keep it private; install it through Settings and restart."
}
finally { $issuer.Dispose(); $verifier.Dispose() }
