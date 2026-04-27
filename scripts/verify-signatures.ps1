param(
    [string]$PortablePath = "",
    [string]$InstallerPath = "",
    [switch]$RequireSigned
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$portableExePath = Resolve-PointyPalPortableExePath -RepoRoot $repoRoot -PortablePath $PortablePath
$installerArtifactPath = Resolve-PointyPalInstallerArtifactPath -RepoRoot $repoRoot -InstallerPath $InstallerPath
$installerWasExplicit = -not [string]::IsNullOrWhiteSpace($InstallerPath)

$targets = @(
    [pscustomobject][ordered]@{ Kind = "Portable EXE"; Path = $portableExePath; Required = $true },
    [pscustomobject][ordered]@{ Kind = "Installer setup EXE"; Path = $installerArtifactPath; Required = $installerWasExplicit }
)

Write-Host "PointyPal signature verification" -ForegroundColor Cyan
Write-Host ""

$failed = $false

foreach ($target in $targets) {
    $signature = Get-PointyPalSignatureInfo -Path $target.Path

    $displayStatus = switch ($signature.signatureStatus) {
        "Valid" { "Signed" }
        "Unsigned" { "Unsigned" }
        "Missing" { "Missing" }
        "Unknown" { "Invalid" }
        default { "Invalid" }
    }

    Write-Host "File: $($target.Path)"
    Write-Host "Kind: $($target.Kind)"
    Write-Host "Status: $displayStatus"
    if (-not [string]::IsNullOrWhiteSpace($signature.signer)) {
        Write-Host "Signer: $($signature.signer)"
    }
    else {
        Write-Host "Signer:"
    }
    Write-Host "Status message: $($signature.statusMessage)"
    Write-Host ""

    if ($RequireSigned) {
        if ($signature.signatureStatus -eq "Missing" -and -not $target.Required) {
            continue
        }

        if ($signature.signatureStatus -ne "Valid") {
            $failed = $true
        }
    }
}

if ($RequireSigned -and $failed) {
    Write-Host "Signature verification failed because -RequireSigned was supplied." -ForegroundColor Red
    exit 1
}

Write-Host "Signature verification completed." -ForegroundColor Green
exit 0
