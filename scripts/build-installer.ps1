param(
    [string]$RuntimeTarget = "win-x64",
    [string]$BuildDate = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$metadata = Get-PointyPalReleaseMetadata -RepoRoot $repoRoot -BuildDate $BuildDate
$portableDir = Join-Path $repoRoot "artifacts\PointyPal-portable"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $repoRoot "installer\PointyPal.iss"
$installerFileName = Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget
$installerPath = Join-Path $installerDir $installerFileName
$outputBaseFilename = [System.IO.Path]::GetFileNameWithoutExtension($installerFileName)

function Test-PortableArtifactReady {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $requiredFiles = @(
        "PointyPal.exe",
        "PointyPal.dll",
        "config.example.json",
        "NOTICE.txt",
        "README-FIRST-RUN.md",
        "release-manifest.json",
        "checksums.txt",
        "docs\local-release.md",
        "docs\install-uninstall.md"
    )

    foreach ($relative in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $relative))) {
            return $false
        }
    }

    return $true
}

if (-not (Test-PortableArtifactReady -Path $portableDir)) {
    Write-Host "Portable artifact is missing or incomplete. Running portable release check first..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "release-check.ps1") `
        -RuntimeTarget $RuntimeTarget

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-PortableArtifactReady -Path $portableDir)) {
    Write-Error "Portable artifact is still missing or incomplete after release-check.ps1."
    exit 1
}

if (-not (Test-Path -LiteralPath $issPath)) {
    Write-Error "Inno Setup script not found: $issPath"
    exit 1
}

foreach ($file in Get-ChildItem -LiteralPath $portableDir -Recurse -File) {
    $relative = Get-PointyPalRelativePath -BasePath $portableDir -Path $file.FullName
    if (-not (Test-PointyPalPackageFileAllowed -RelativePath $relative)) {
        Write-Error "Disallowed file found in portable artifact before installer build: $relative"
        exit 1
    }
}

$iscc = Get-PointyPalInnoCompiler
if ([string]::IsNullOrWhiteSpace($iscc)) {
    Write-Host "Inno Setup compiler (ISCC.exe) was not found." -ForegroundColor Red
    Write-Host "Install Inno Setup 6 from https://jrsoftware.org/isinfo.php, or add ISCC.exe to PATH."
    Write-Host "Portable ZIP release is unaffected; scripts/release-check.ps1 remains portable-only by default."
    exit 1
}

if (-not (Test-Path -LiteralPath $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}

Write-Host "Building PointyPal installer with Inno Setup..." -ForegroundColor Cyan
Write-Host "ISCC: $iscc"
Write-Host "Output: $installerPath"

$isccArgs = @(
    "/DAppName=$($metadata.AppName)",
    "/DAppVersion=$($metadata.Version)",
    "/DReleaseLabel=$($metadata.ReleaseLabel)",
    "/DRuntimeTarget=$RuntimeTarget",
    "/DPortableDir=$portableDir",
    "/O$installerDir",
    "/F$outputBaseFilename",
    $issPath
)

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $installerPath)) {
    Write-Error "Expected installer was not created: $installerPath"
    exit 1
}

& (Join-Path $PSScriptRoot "create-installer-manifest.ps1") `
    -InstallerPath $installerPath `
    -RuntimeTarget $RuntimeTarget `
    -BuildDate $metadata.BuildDate | Out-Null

$hash = Get-PointyPalSha256 -Path $installerPath
$signature = Get-PointyPalSignatureInfo -Path $installerPath

Write-Host ""
Write-Host "Installer created:" -ForegroundColor Green
Write-Host $installerPath
Write-Host "SHA256: $hash"
Write-Host "Signed: $($signature.signed)"
Write-Host "Signature status: $($signature.signatureStatus)"
