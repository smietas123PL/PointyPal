param(
    [switch]$UseExistingArtifacts,
    [switch]$SelfContained,
    [string]$BuildDate = "",
    [string]$RuntimeTarget = "win-x64"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$portableDir = Join-Path $repoRoot "artifacts\PointyPal-portable"
$stageDir = Join-Path $repoRoot "artifacts\PointyPal-zip-stage"
$metadata = Get-PointyPalReleaseMetadata -RepoRoot $repoRoot -BuildDate $BuildDate

if (-not $UseExistingArtifacts) {
    & (Join-Path $PSScriptRoot "publish-portable.ps1") `
        -SelfContained:$SelfContained.IsPresent `
        -BuildDate $metadata.BuildDate `
        -RuntimeTarget $RuntimeTarget
}
elseif (-not (Test-Path -LiteralPath $portableDir)) {
    throw "Portable artifacts not found. Run scripts/publish-portable.ps1 or omit -UseExistingArtifacts."
}

& (Join-Path $PSScriptRoot "create-release-manifest.ps1") `
    -PortableDir $portableDir `
    -RuntimeTarget $RuntimeTarget `
    -BuildDate $metadata.BuildDate `
    -SelfContained:$SelfContained.IsPresent

Copy-PointyPalFilteredPortableFiles -SourceDir $portableDir -DestinationDir $stageDir

$packageSuffix = if ($SelfContained) { "self-contained-portable" } else { "portable" }
$zipName = "$($metadata.AppName)-v$($metadata.Version)-$($metadata.ReleaseLabel)-$RuntimeTarget-$packageSuffix.zip"
$zipPath = Join-Path (Join-Path $repoRoot "artifacts") $zipName

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $stageDir -Recurse -Force

Write-Host "Portable ZIP created:" -ForegroundColor Green
Write-Host $zipPath
