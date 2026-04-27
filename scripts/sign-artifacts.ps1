[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Low")]
param(
    [string]$CertPath = "",
    [string]$CertPassword = "",
    [string]$CertThumbprint = "",
    [string]$TimestampUrl = "",
    [string]$PortablePath = "",
    [string]$InstallerPath = "",
    [switch]$SkipIfNoCertificate
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

function Resolve-ConfiguredValue {
    param(
        [string]$ParameterValue,
        [string]$EnvironmentValue
    )

    if (-not [string]::IsNullOrWhiteSpace($ParameterValue)) {
        return $ParameterValue
    }

    if (-not [string]::IsNullOrWhiteSpace($EnvironmentValue)) {
        return $EnvironmentValue
    }

    return ""
}

$repoRoot = Get-PointyPalRepoRoot
$resolvedCertPath = Resolve-ConfiguredValue -ParameterValue $CertPath -EnvironmentValue $env:SIGN_CERT_PATH
$resolvedCertPassword = Resolve-ConfiguredValue -ParameterValue $CertPassword -EnvironmentValue $env:SIGN_CERT_PASSWORD
$resolvedCertThumbprint = Resolve-ConfiguredValue -ParameterValue $CertThumbprint -EnvironmentValue $env:SIGN_CERT_THUMBPRINT
$resolvedTimestampUrl = Resolve-ConfiguredValue -ParameterValue $TimestampUrl -EnvironmentValue $env:SIGN_TIMESTAMP_URL

if ([string]::IsNullOrWhiteSpace($resolvedTimestampUrl)) {
    $resolvedTimestampUrl = "http://timestamp.digicert.com"
}

$hasCertPath = -not [string]::IsNullOrWhiteSpace($resolvedCertPath)
$hasThumbprint = -not [string]::IsNullOrWhiteSpace($resolvedCertThumbprint)

Write-Host "PointyPal artifact signing" -ForegroundColor Cyan
Write-Host "Timestamp URL: $resolvedTimestampUrl"

if ($hasCertPath -and $hasThumbprint) {
    Write-Error "Provide either CertPath or CertThumbprint, not both. Environment variables SIGN_CERT_PATH and SIGN_CERT_THUMBPRINT count as configured inputs."
    exit 1
}

if (-not $hasCertPath -and -not $hasThumbprint) {
    Write-Warning "No signing certificate was configured."
    Write-Host "Configure one of:"
    Write-Host "  SIGN_CERT_PATH and SIGN_CERT_PASSWORD, or -CertPath and -CertPassword"
    Write-Host "  SIGN_CERT_THUMBPRINT, or -CertThumbprint"
    Write-Host "Signing is optional for normal private RC builds."

    if ($SkipIfNoCertificate) {
        Write-Warning "Skipping signing because -SkipIfNoCertificate was supplied."
        exit 0
    }

    exit 1
}

$mode = if ($hasCertPath) { "PFX" } else { "Thumbprint" }
Write-Host "Signer mode: $mode"

if ($mode -eq "PFX") {
    if (-not (Test-Path -LiteralPath $resolvedCertPath -PathType Leaf)) {
        Write-Error "Certificate PFX file was not found: $resolvedCertPath"
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($resolvedCertPassword)) {
        Write-Error "A PFX signing certificate requires a password. Provide -CertPassword or SIGN_CERT_PASSWORD. The password is never printed."
        exit 1
    }
}

$signTool = Get-PointyPalSignTool
if ([string]::IsNullOrWhiteSpace($signTool)) {
    Write-Host "signtool.exe was not found." -ForegroundColor Red
    Write-Host "Install the Windows 10/11 SDK and include the 'Windows SDK Signing Tools for Desktop Apps' component."
    Write-Host "After installation, reopen PowerShell or add the Windows Kits bin x64 folder to PATH."
    exit 1
}

Write-Host "SignTool: $signTool"

$portableExePath = Resolve-PointyPalPortableExePath -RepoRoot $repoRoot -PortablePath $PortablePath
$installerArtifactPath = Resolve-PointyPalInstallerArtifactPath -RepoRoot $repoRoot -InstallerPath $InstallerPath

$targets = @(
    [pscustomobject][ordered]@{ Kind = "Portable EXE"; Path = $portableExePath },
    [pscustomobject][ordered]@{ Kind = "Installer setup EXE"; Path = $installerArtifactPath }
)

Write-Host ""
Write-Host "Files considered:"
foreach ($target in $targets) {
    Write-Host "  [$($target.Kind)] $($target.Path)"
}

$signed = New-Object System.Collections.Generic.List[string]
$skipped = New-Object System.Collections.Generic.List[string]

foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target.Path -PathType Leaf)) {
        $skipped.Add("$($target.Kind): missing - $($target.Path)") | Out-Null
        continue
    }

    $signature = Get-PointyPalSignatureInfo -Path $target.Path
    if ($signature.signatureStatus -eq "Valid" -or -not [string]::IsNullOrWhiteSpace($signature.signer)) {
        $skipped.Add("$($target.Kind): already has signature status '$($signature.signatureStatus)' - $($target.Path)") | Out-Null
        continue
    }

    $arguments = New-PointyPalSignToolArguments `
        -Mode $mode `
        -CertPath $resolvedCertPath `
        -CertPassword $resolvedCertPassword `
        -CertThumbprint $resolvedCertThumbprint `
        -TimestampUrl $resolvedTimestampUrl `
        -FilePath $target.Path

    if ($PSCmdlet.ShouldProcess($target.Path, "Sign $($target.Kind)")) {
        Write-Host ""
        Write-Host "Signing $($target.Kind): $($target.Path)" -ForegroundColor Cyan
        $output = & $signTool @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }

        if ($exitCode -ne 0) {
            Write-Error "signtool.exe failed for $($target.Path) with exit code $exitCode."
            exit $exitCode
        }

        $postSignature = Get-PointyPalSignatureInfo -Path $target.Path
        $signed.Add("$($target.Kind): $($target.Path) (status: $($postSignature.signatureStatus))") | Out-Null
    }
    else {
        $skipped.Add("$($target.Kind): WhatIf - $($target.Path)") | Out-Null
    }
}

Write-Host ""
Write-Host "Files signed:"
if ($signed.Count -eq 0) {
    Write-Host "  none"
}
else {
    $signed | ForEach-Object { Write-Host "  $_" }
}

Write-Host "Files skipped:"
if ($skipped.Count -eq 0) {
    Write-Host "  none"
}
else {
    $skipped | ForEach-Object { Write-Host "  $_" }
}

exit 0
