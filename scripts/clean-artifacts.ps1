$ErrorActionPreference = "Stop"

$solutionDir = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$artifactsDir = Join-Path $solutionDir "artifacts"

Write-Host "Cleaning artifacts folder..." -ForegroundColor Cyan

if (Test-Path $artifactsDir) {
    Remove-Item -Recurse -Force $artifactsDir
    Write-Host "Deleted $artifactsDir" -ForegroundColor Green
} else {
    Write-Host "Artifacts folder does not exist." -ForegroundColor Yellow
}

Write-Host "Note: User AppData is not deleted by this script."
