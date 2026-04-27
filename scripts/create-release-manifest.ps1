param(
    [string]$PortableDir = "",
    [string]$RuntimeTarget = "win-x64",
    [switch]$SelfContained,
    [string]$BuildDate = "",
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($PortableDir)) {
    $PortableDir = Join-Path $root "artifacts\PointyPal-portable"
}

$manifest = New-PointyPalReleaseManifest `
    -PortableDir $PortableDir `
    -RepoRoot $root `
    -BuildDate $BuildDate `
    -RuntimeTarget $RuntimeTarget `
    -SelfContained $SelfContained.IsPresent

Write-Host "Release manifest generated:" -ForegroundColor Green
Write-Host (Join-Path $PortableDir "release-manifest.json")
Write-Host "Checksums generated:"
Write-Host (Join-Path $PortableDir "checksums.txt")
Write-Host "Files listed: $(@($manifest.files).Count)"
Write-Host "Portable EXE signature: $($manifest.signing.portableExeSignatureStatus)"
Write-Host "Installer signature: $($manifest.signing.installerSignatureStatus)"
