param(
    [switch]$ExportPfx,
    [string]$OutputPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$subject = "CN=PointyPal Local Test Code Signing"

Write-Host "Creating a local PointyPal test code signing certificate..." -ForegroundColor Cyan
Write-Host ""
Write-Warning "This certificate is for local signing pipeline tests only."
Write-Warning "Do not use it for public distribution."
Write-Warning "Do not commit exported PFX files or private keys."
Write-Warning "This does not solve Windows SmartScreen reputation or public trust."
Write-Host ""

if ($null -eq (Get-Command "New-SelfSignedCertificate" -ErrorAction SilentlyContinue)) {
    Write-Error "New-SelfSignedCertificate is not available on this system. Run this script on Windows with the PKI module available."
    exit 1
}

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $subject `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable

Write-Host "Certificate created in Cert:\CurrentUser\My"
Write-Host "Subject: $($cert.Subject)"
Write-Host "Thumbprint: $($cert.Thumbprint)"

if ($ExportPfx) {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        Write-Error "-ExportPfx requires -OutputPath. Choose a path outside this repository, such as a private folder under your user profile."
        exit 1
    }

    $resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        [System.IO.Path]::GetFullPath($OutputPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
    }

    $repoFull = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    if ($resolvedOutputPath.StartsWith($repoFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Warning "The requested PFX path is inside the repository. Do not commit exported PFX files."
    }

    $outputDir = Split-Path -Parent $resolvedOutputPath
    if (-not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }

    $securePassword = $null
    if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
        $securePassword = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:SIGN_CERT_PASSWORD)) {
        $securePassword = ConvertTo-SecureString -String $env:SIGN_CERT_PASSWORD -AsPlainText -Force
    }
    else {
        $securePassword = Read-Host "Enter a password for the exported PFX" -AsSecureString
    }

    Export-PfxCertificate -Cert $cert -FilePath $resolvedOutputPath -Password $securePassword | Out-Null
    Write-Host "PFX exported to: $resolvedOutputPath"
    Write-Warning "Keep the exported PFX outside source control and private backups unless there is an explicit secure storage plan."
}

Write-Host ""
Write-Host "Use this thumbprint for a local signing test:"
Write-Host "powershell -ExecutionPolicy Bypass -File .\scripts\sign-artifacts.ps1 -CertThumbprint `"$($cert.Thumbprint)`""
