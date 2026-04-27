param(
    [string]$PortableDir = "artifacts/PointyPal-portable",
    [switch]$SkipSelfTest
)

# validate-portable.ps1
# Validates the PointyPal portable release package.

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
if (-not [System.IO.Path]::IsPathRooted($PortableDir)) {
    $PortableDir = Join-Path $repoRoot $PortableDir
}

$mainExe = Join-Path $PortableDir "PointyPal.exe"
$manifestPath = Join-Path $PortableDir "release-manifest.json"
$checksumsPath = Join-Path $PortableDir "checksums.txt"

Write-Host "Checking portable release in $PortableDir..." -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $PortableDir)) {
    Write-Error "Portable directory not found!"
    exit 1
}

$requiredFiles = @(
    "PointyPal.exe",
    "config.example.json",
    "release-manifest.json",
    "checksums.txt",
    "NOTICE.txt",
    "README-FIRST-RUN.md",
    "docs/local-release.md",
    "docs/install-uninstall.md",
    "docs/code-signing-plan.md"
)

foreach ($relative in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $PortableDir $relative))) {
        Write-Error "Required package file missing: $relative"
        exit 1
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $PortableDir "docs"))) {
    Write-Error "Docs folder missing."
    exit 1
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if (-not $manifest.selfContained) {
    Write-Host "Framework-dependent package requires .NET 8 Desktop Runtime." -ForegroundColor Yellow
    if (-not (Test-PointyPalDesktopRuntimeAvailable)) {
        Write-Error ".NET 8 Desktop Runtime was not found on this machine."
        exit 1
    }
}

foreach ($file in Get-ChildItem -LiteralPath $PortableDir -Recurse -File) {
    $relative = Get-PointyPalRelativePath -BasePath $PortableDir -Path $file.FullName
    if (-not (Test-PointyPalPackageFileAllowed -RelativePath $relative)) {
        Write-Error "Disallowed file found in portable package: $relative"
        exit 1
    }
}

$secrets = @(Find-PointyPalObviousSecrets -PortableDir $PortableDir)
if ($secrets.Count -gt 0) {
    foreach ($finding in $secrets) {
        Write-Error "Possible secret found in package file: $($finding.Path)"
    }
    exit 1
}

$exeEntry = @($manifest.files | Where-Object { $_.path -eq "PointyPal.exe" }) | Select-Object -First 1
if ($null -eq $exeEntry) {
    Write-Error "Manifest does not contain PointyPal.exe."
    exit 1
}

$actualExeHash = Get-PointyPalSha256 -Path $mainExe
if ($actualExeHash -ne $exeEntry.sha256) {
    Write-Error "PointyPal.exe hash mismatch. Manifest=$($exeEntry.sha256) Actual=$actualExeHash"
    exit 1
}

if (-not (Select-String -LiteralPath $checksumsPath -Pattern "PointyPal.exe" -Quiet)) {
    Write-Error "checksums.txt does not contain PointyPal.exe."
    exit 1
}

# Run self-test if possible
if (-not $SkipSelfTest) {
    Write-Host "Running published offline self-test..." -ForegroundColor Cyan
    try {
        $process = Start-Process -FilePath $mainExe -ArgumentList "--self-test", "--safe-mode" -Wait -NoNewWindow -PassThru
        if ($process.ExitCode -eq 0) {
            Write-Host "Self-test PASSED." -ForegroundColor Green
        } else {
            Write-Error "Self-test FAILED with exit code $($process.ExitCode)."
            exit 1
        }
    } catch {
        Write-Error "Failed to run self-test: $($_.Exception.Message)"
        exit 1
    }
}

Write-Host "Portable validation SUCCESSFUL." -ForegroundColor Green
exit 0
