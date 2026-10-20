param(
  [Parameter(Mandatory = $true)][string]$Version
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$pkgName = 'DCS-UH-1H-Components'
$zip = Join-Path $dist "$pkgName-$Version-OVGME.zip"
if (-not (Test-Path $zip)) { throw "Missing package $zip" }
$sums = Get-Content (Join-Path $dist 'SHA256SUMS.txt')
$leaf = Split-Path $zip -Leaf
$hashLine = $sums | Where-Object { $_ -match [regex]::Escape($leaf) } | Select-Object -First 1
if (-not $hashLine) { throw 'SHA256SUMS.txt does not list the package archive.' }
$expected = ($hashLine -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expected -ne $actual) { throw 'SHA256SUMS.txt does not match the package archive.' }

$configPath = Join-Path $root 'config/kneeboard.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$hasEffectiveAssignments = $false
foreach ($profilePath in @($config.profiles.PSObject.Properties.Value)) {
  $absoluteProfile = Join-Path $root $profilePath
  if ((Test-Path $absoluteProfile) -and ((Get-Content $absoluteProfile -Raw) -match '\["(?:added|changed)"\]\s*=')) {
    $hasEffectiveAssignments = $true
    break
  }
}
$generatedKneeboardPath = Join-Path $root 'kneeboard/UH-1H'
$hasGeneratedKneeboards = (Test-Path $generatedKneeboardPath) -and
  (@(Get-ChildItem $generatedKneeboardPath -Filter '*.png' -File -ErrorAction SilentlyContinue).Count -gt 0)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archiveBase = [System.IO.Path]::GetFileNameWithoutExtension($leaf)
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
  $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
  $payloadPrefix = "$archiveBase/"
  $unexpected = @($entries | Where-Object {
    $_ -ne 'README.TXT' -and
    $_ -ne 'VERSION.TXT' -and
    -not $_.StartsWith($payloadPrefix, [System.StringComparison]::Ordinal)
  })
  if ($unexpected.Count -gt 0) {
    throw "Invalid OVGME archive root. Expected '$archiveBase/' but found '$($unexpected[0])'."
  }
  if (-not ($entries | Where-Object { $_.StartsWith("${payloadPrefix}Config/Input/UH-1H/joystick/", [System.StringComparison]::Ordinal) })) {
    throw 'OVGME archive is missing the joystick profile payload.'
  }
  $uiLayerEntries = @($entries | Where-Object {
    $_.StartsWith("${payloadPrefix}Config/Input/UiLayer/", [System.StringComparison]::Ordinal)
  })
  if ($uiLayerEntries.Count -gt 0 -and $entries -notcontains "${payloadPrefix}Config/Input/UiLayer/modifiers.lua") {
    throw 'OVGME archive contains UI Layer profiles but is missing modifiers.lua.'
  }
  if (-not $hasEffectiveAssignments -and $uiLayerEntries.Count -gt 0) {
    throw 'Blank scaffold unexpectedly packaged UI Layer profiles without an effective module assignment.'
  }
  if ($hasGeneratedKneeboards -and -not ($entries | Where-Object {
    $_.StartsWith("${payloadPrefix}KNEEBOARD/UH-1H/", [System.StringComparison]::Ordinal)
  })) {
    throw 'OVGME archive is missing generated kneeboard payload.'
  }
  if (-not $hasEffectiveAssignments) {
    Write-Host 'Blank scaffold has no effective assignments; UI Layer and kneeboard payloads are intentionally optional.'
  }
  if ($entries -notcontains 'README.TXT') { throw 'OVGME archive is missing README.TXT.' }
  if ($entries -notcontains 'VERSION.TXT') { throw 'OVGME archive is missing VERSION.TXT.' }
}
finally {
  $archive.Dispose()
}

Write-Host "Package checksum and OVGME structure OK for $leaf"
