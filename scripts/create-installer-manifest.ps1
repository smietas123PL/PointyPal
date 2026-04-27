param(
    [string]$InstallerPath = "",
    [string]$RuntimeTarget = "win-x64",
    [string]$BuildDate = "",
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
$metadata = Get-PointyPalReleaseMetadata -RepoRoot $root -BuildDate $BuildDate

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $installerDir = Join-Path $root "artifacts\installer"
    $InstallerPath = Join-Path $installerDir (Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget)
}

$manifest = New-PointyPalInstallerManifest `
    -InstallerPath $InstallerPath `
    -RepoRoot $root `
    -BuildDate $metadata.BuildDate `
    -RuntimeTarget $RuntimeTarget

$installerDir = Split-Path -Parent (Resolve-Path -LiteralPath $InstallerPath).Path

Write-Host "Installer manifest generated:" -ForegroundColor Green
Write-Host (Join-Path $installerDir "installer-manifest.json")
Write-Host "Installer checksums generated:"
Write-Host (Join-Path $installerDir "installer-checksums.txt")
Write-Host "Installer file: $($manifest.installer.filename)"
Write-Host "SHA256: $($manifest.installer.sha256)"
Write-Host "Signed: $($manifest.installer.signed)"
Write-Host "Signature status: $($manifest.installer.signatureStatus)"
