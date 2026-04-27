param(
    [switch]$SelfContained,
    [string]$BuildDate = "",
    [string]$RuntimeTarget = "win-x64"
)

$ErrorActionPreference = "Stop"

$solutionDir = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$projectPath = Join-Path $solutionDir "PointyPal.csproj"
$artifactsDir = Join-Path $solutionDir "artifacts\PointyPal-portable"
$docsDir = Join-Path $solutionDir "docs"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$metadata = Get-PointyPalReleaseMetadata -RepoRoot $solutionDir -BuildDate $BuildDate
$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()

Write-Host "Publishing PointyPal Portable Release ($RuntimeTarget, self-contained: $selfContainedValue)..." -ForegroundColor Cyan

# Remove old artifacts if they exist
if (Test-Path $artifactsDir) {
    Remove-Item -Recurse -Force $artifactsDir
}

$publishObjDir = Join-Path $solutionDir "obj\Release\net8.0-windows\$RuntimeTarget"
if (Test-Path -LiteralPath $publishObjDir) {
    Get-ChildItem -LiteralPath $publishObjDir -Filter "PublishOutputs.*.txt" -ErrorAction SilentlyContinue | Remove-Item -Force
}

# Run publish
dotnet publish $projectPath `
    -c Release `
    -r $RuntimeTarget `
    --self-contained $selfContainedValue `
    "-p:UseAppHost=true" `
    "-p:BuildDate=$($metadata.BuildDate)" `
    "-p:GitCommit=$($metadata.GitCommit)" `
    -o $artifactsDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$mainExe = Join-Path $artifactsDir "PointyPal.exe"
if (-not (Test-Path -LiteralPath $mainExe)) {
    $fallbackDir = Join-Path $solutionDir "bin\Release\net8.0-windows\$RuntimeTarget"
    if (Test-Path -LiteralPath (Join-Path $fallbackDir "PointyPal.exe")) {
        Write-Warning "Publish output was incomplete; copying built release files from $fallbackDir."
        Copy-Item -Path (Join-Path $fallbackDir "*") -Destination $artifactsDir -Recurse -Force
    }
    else {
        throw "PointyPal.exe was not produced by publish."
    }
}

Get-ChildItem -LiteralPath $artifactsDir -Recurse -File -Filter *.pdb | Remove-Item -Force

# Copy docs
$artifactsDocsDir = Join-Path $artifactsDir "docs"
if (-not (Test-Path $artifactsDocsDir)) {
    New-Item -ItemType Directory -Path $artifactsDocsDir | Out-Null
}

$docFiles = Get-ChildItem -LiteralPath $docsDir -File -Filter "*.md"
foreach ($doc in $docFiles) {
    Copy-Item -LiteralPath $doc.FullName -Destination $artifactsDocsDir -Force
}

$rootFiles = @("config.example.json", "NOTICE.txt", "README-FIRST-RUN.md")
foreach ($fileName in $rootFiles) {
    $source = Join-Path $solutionDir $fileName
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $artifactsDir -Force
    }
}

& (Join-Path $PSScriptRoot "create-release-manifest.ps1") `
    -PortableDir $artifactsDir `
    -RuntimeTarget $RuntimeTarget `
    -BuildDate $metadata.BuildDate `
    -SelfContained:$SelfContained.IsPresent

Write-Host ""
Write-Host "Done! Portable release is available at:" -ForegroundColor Green
Write-Host $artifactsDir
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Review docs/local-release.md"
Write-Host "2. Run scripts/package-portable-zip.ps1"
