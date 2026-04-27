param(
    [string]$InstallerPath = "",
    [string]$ManifestPath = "",
    [string]$RuntimeTarget = "win-x64",
    [int]$MaxSizeMB = 250,
    [switch]$InstallTest
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$metadata = Get-PointyPalReleaseMetadata -RepoRoot $repoRoot
$expectedFileName = Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget
$installerDir = Join-Path $repoRoot "artifacts\installer"

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $installerDir $expectedFileName
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path (Split-Path -Parent $InstallerPath) "installer-manifest.json"
}

$checksumsPath = Join-Path (Split-Path -Parent $InstallerPath) "installer-checksums.txt"

Write-Host "Checking installer in $(Split-Path -Parent $InstallerPath)..." -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    Write-Error "Setup EXE not found: $InstallerPath"
    exit 1
}

$installerItem = Get-Item -LiteralPath $InstallerPath
if ($installerItem.Name -ne $expectedFileName) {
    Write-Error "Setup EXE filename mismatch. Expected '$expectedFileName' but found '$($installerItem.Name)'."
    exit 1
}

if ($MaxSizeMB -le 0) {
    Write-Error "MaxSizeMB must be greater than zero."
    exit 1
}

$maxBytes = $MaxSizeMB * 1MB
if ($installerItem.Length -gt $maxBytes) {
    Write-Error "Installer is larger than expected. Size=$($installerItem.Length) bytes Limit=$maxBytes bytes"
    exit 1
}

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    Write-Error "Installer manifest not found: $ManifestPath"
    exit 1
}

if (-not (Test-Path -LiteralPath $checksumsPath)) {
    Write-Error "Installer checksums file not found: $checksumsPath"
    exit 1
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$actualHash = Get-PointyPalSha256 -Path $InstallerPath

if ($manifest.installer.filename -ne $expectedFileName) {
    Write-Error "Installer manifest filename mismatch. Expected '$expectedFileName' but found '$($manifest.installer.filename)'."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($manifest.installer.sha256)) {
    Write-Error "Installer manifest does not include a SHA256 hash."
    exit 1
}

if ($manifest.installer.sha256 -ne $actualHash) {
    Write-Error "Installer hash mismatch. Manifest=$($manifest.installer.sha256) Actual=$actualHash"
    exit 1
}

$signature = Get-PointyPalSignatureInfo -Path $InstallerPath
if ($null -eq $manifest.installer.PSObject.Properties["signed"]) {
    Write-Error "Installer manifest does not include signed metadata."
    exit 1
}

if ($null -eq $manifest.installer.PSObject.Properties["signatureStatus"]) {
    Write-Error "Installer manifest does not include signatureStatus metadata."
    exit 1
}

if ($null -eq $manifest.installer.PSObject.Properties["signer"]) {
    Write-Error "Installer manifest does not include signer metadata."
    exit 1
}

if ([bool]$manifest.installer.signed -ne [bool]$signature.signed) {
    Write-Error "Installer manifest signed value mismatch. Manifest=$($manifest.installer.signed) Actual=$($signature.signed)"
    exit 1
}

if ($manifest.installer.signatureStatus -ne $signature.signatureStatus) {
    Write-Error "Installer manifest signature status mismatch. Manifest=$($manifest.installer.signatureStatus) Actual=$($signature.signatureStatus)"
    exit 1
}

$checksumText = Get-Content -LiteralPath $checksumsPath -Raw
if ($checksumText -notmatch [regex]::Escape($expectedFileName) -or $checksumText -notmatch $actualHash) {
    Write-Error "Installer checksums file does not contain the expected filename and SHA256 hash."
    exit 1
}

$mzHeader = New-Object byte[] 2
$stream = [System.IO.File]::OpenRead($installerItem.FullName)
try {
    $null = $stream.Read($mzHeader, 0, 2)
}
finally {
    $stream.Dispose()
}

if ($mzHeader[0] -ne 0x4D -or $mzHeader[1] -ne 0x5A) {
    Write-Error "Setup EXE does not have a valid Windows executable header."
    exit 1
}

$secrets = @(Find-PointyPalObviousSecrets -PortableDir (Split-Path -Parent $InstallerPath))
if ($secrets.Count -gt 0) {
    foreach ($finding in $secrets) {
        Write-Error "Possible secret found in installer output file: $($finding.Path)"
    }
    exit 1
}

$iscc = Get-PointyPalInnoCompiler
if (-not [string]::IsNullOrWhiteSpace($iscc)) {
    Write-Host "Inno Setup compiler available for follow-up verification: $iscc"
}

if ($InstallTest) {
    $logPath = Join-Path (Split-Path -Parent $InstallerPath) "install-test.log"
    Write-Host "Launching installer for explicit install test..." -ForegroundColor Yellow
    $process = Start-Process -FilePath $InstallerPath -ArgumentList "/LOG=""$logPath"" /NORESTART" -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Error "Installer exited with code $($process.ExitCode)."
        exit 1
    }
}
else {
    Write-Host "Installer execution skipped. Pass -InstallTest to launch the setup EXE."
}

Write-Host "Installer validation SUCCESSFUL." -ForegroundColor Green
exit 0
