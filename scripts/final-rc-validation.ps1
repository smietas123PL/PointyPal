param(
    [string]$BuildDate = "",
    [string]$RuntimeTarget = "win-x64",
    [switch]$IncludeInstaller
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

. (Join-Path $scriptDir "release-tools.ps1")

$metadata = Get-PointyPalReleaseMetadata -RepoRoot $repoRoot -BuildDate $BuildDate
$startedAt = (Get-Date).ToUniversalTime()
$reportPath = Join-Path $repoRoot "artifacts\final-rc-validation-report.json"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "   PointyPal Final Private RC Validation" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "App Name:      $($metadata.AppName)"
Write-Host "Version:       $($metadata.Version)"
Write-Host "Build Channel: $($metadata.BuildChannel)"
Write-Host "Release Label: $($metadata.ReleaseLabel)"
Write-Host "Started At:    $($startedAt.ToString("yyyy-MM-dd HH:mm:ss")) UTC"
Write-Host ""

$results = @{
    AppName                = $metadata.AppName
    Version                = $metadata.Version
    ReleaseLabel           = $metadata.ReleaseLabel
    BuildChannel           = $metadata.BuildChannel
    WorkerContractVersion  = $metadata.WorkerContractVersion
    ValidationStartedAt    = $startedAt.ToString("o")
    TestsPassed            = $false
    TestCount              = 0
    BuildPassed            = $false
    PortableReleasePassed  = $false
    InstallerReleasePassed = "Skipped"
    SignatureStatus        = "Unchecked"
    DocsPresent            = $false
    KnownWarnings          = New-Object System.Collections.Generic.List[string]
    GoNoGoRecommendation   = "NO-GO"
}

function Invoke-Step {
    param([string]$Name, [scriptblock]$Command)
    Write-Host "Step: $Name..." -NoNewline
    try {
        $output = & $Command 2>&1
        Write-Host " PASS" -ForegroundColor Green
        return @($true, $output)
    }
    catch {
        Write-Host " FAIL" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Gray
        return @($false, $_.Exception.Message)
    }
}

# 1. dotnet test
$step1 = Invoke-Step "Unit Tests" { dotnet test (Join-Path $repoRoot "PointyPal.sln") --nologo -v m }
$results.BuildPassed = $step1[0]
if ($step1[0]) {
    $results.TestsPassed = $true
    # Try to extract test count (English or Polish)
    $testSummaryLine = $step1[1] | Where-Object { $_ -match "Total tests: (\d+)|łącznie: (\d+)" } | Select-Object -First 1
    if ($testSummaryLine -match "Total tests: (\d+)") { $results.TestCount = [int]$matches[1] }
    elseif ($testSummaryLine -match "łącznie: (\d+)") { $results.TestCount = [int]$matches[1] }
}

# 2. build.ps1
$step2 = Invoke-Step "Build Script" { & (Join-Path $scriptDir "build.ps1") }
$results.BuildPassed = $results.BuildPassed -and $step2[0]

# 3. release-check.ps1 (Portable)
$step3 = Invoke-Step "Portable Release Check" { & (Join-Path $scriptDir "release-check.ps1") -BuildDate $metadata.BuildDate }
$results.PortableReleasePassed = $step3[0]

# 4. release-check.ps1 (Installer & Signatures)
$innoCompiler = Get-PointyPalInnoCompiler
if ($IncludeInstaller) {
    if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
        Write-Host "Step: Installer Release Check... SKIPPED (Inno Setup missing)" -ForegroundColor Yellow
        $results.InstallerReleasePassed = "Skipped: Inno Setup missing"
        $results.KnownWarnings.Add("Inno Setup missing; installer validation skipped.")
    }
    else {
        $step4 = Invoke-Step "Installer Release Check" { & (Join-Path $scriptDir "release-check.ps1") -IncludeInstaller -VerifySignatures -BuildDate $metadata.BuildDate }
        $results.InstallerReleasePassed = if ($step4[0]) { "Passed" } else { "Failed" }
        if ($step4[0]) {
            $results.SignatureStatus = "Verified (may be unsigned if no cert)"
        }
    }
}
else {
    $results.InstallerReleasePassed = "Skipped (not requested)"
}

# Artifact Existence Checks
Write-Host "Verifying Artifacts..."
$artifactsDir = Join-Path $repoRoot "artifacts"
$portableDir = Join-Path $artifactsDir "PointyPal-portable"
$installerDir = Join-Path $artifactsDir "installer"

$checks = @{
    "Portable ZIP"        = (Get-ChildItem -Path $artifactsDir -Filter "*.zip" | Where-Object { $_.Name -match "portable" })
    "Release Manifest"    = (Test-Path (Join-Path $portableDir "release-manifest.json"))
    "Checksums"           = (Test-Path (Join-Path $portableDir "checksums.txt"))
    "Config Example"      = (Test-Path (Join-Path $portableDir "config.example.json"))
    "NOTICE.txt"          = (Test-Path (Join-Path $portableDir "NOTICE.txt"))
    "README-FIRST-RUN.md" = (Test-Path (Join-Path $portableDir "README-FIRST-RUN.md"))
}

if ($IncludeInstaller -and -not [string]::IsNullOrWhiteSpace($innoCompiler)) {
    $checks["Installer EXE"] = (Get-ChildItem -Path $installerDir -Filter "*.exe" | Where-Object { $_.Name -match "setup" })
}

$allChecksPass = $true
foreach ($key in $checks.Keys) {
    if (-not $checks[$key]) {
        Write-Host "  [!] Missing: $key" -ForegroundColor Red
        $allChecksPass = $false
    }
}

# Documentation Checks
$docsDir = Join-Path $repoRoot "docs"
$requiredDocs = @(
    "user-modes.md",
    "local-release.md",
    "recovery.md",
    "installer-smoke-test.md",
    "signing-runbook.md"
)

$docsMissing = @()
foreach ($doc in $requiredDocs) {
    if (-not (Test-Path (Join-Path $docsDir $doc))) {
        $docsMissing += $doc
    }
}

if ($docsMissing.Count -eq 0) {
    $results.DocsPresent = $true
}
else {
    Write-Host "  [!] Missing Docs: $($docsMissing -join ', ')" -ForegroundColor Red
}

# Final Assessment
if ($results.TestsPassed -and $results.BuildPassed -and $results.PortableReleasePassed -and $allChecksPass -and $results.DocsPresent) {
    $results.GoNoGoRecommendation = "GO"
    if ($results.SignatureStatus -match "unsigned" -or $results.InstallerReleasePassed -match "Skipped") {
        $results.GoNoGoRecommendation = "GO (with warnings)"
    }
}
else {
    $results.GoNoGoRecommendation = "NO-GO"
}

# Known Warnings
$results.KnownWarnings.Add("Artifacts may be unsigned for private RC.")
$results.KnownWarnings.Add("Framework-dependent package requires .NET 8 Desktop Runtime.")
$results.KnownWarnings.Add("Worker secrets must be configured outside repo.")

$results.ValidationCompletedAt = (Get-Date).ToUniversalTime().ToString("o")

# Save Report
if (-not (Test-Path (Join-Path $repoRoot "artifacts"))) { New-Item -ItemType Directory -Path (Join-Path $repoRoot "artifacts") | Out-Null }
$results | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host ""
$summaryColor = if ($results.GoNoGoRecommendation -eq "NO-GO") { "Red" } else { "Green" }
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "   Summary: $($results.GoNoGoRecommendation)" -ForegroundColor $summaryColor
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "Tests Passed:  $($results.TestsPassed) ($($results.TestCount) total)"
Write-Host "Build Passed:  $($results.BuildPassed)"
Write-Host "Portable Pass: $($results.PortableReleasePassed)"
Write-Host "Installer:     $($results.InstallerReleasePassed)"
Write-Host "Report Saved:  $reportPath"
Write-Host ""

if ($results.GoNoGoRecommendation -eq "NO-GO") {
    exit 1
}
exit 0
