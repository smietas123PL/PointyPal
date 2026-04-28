# export-pointer-qa.ps1
# Summarizes and optionally copies the Pointer QA report.

$appData = [System.IO.Path]::Combine($env:APPDATA, "PointyPal")
$reportPath = [System.IO.Path]::Combine($appData, "debug", "pointer-qa-report.json")

if (-not (Test-Path $reportPath)) {
    Write-Host "No Pointer QA report found at $reportPath" -ForegroundColor Yellow
    Write-Host "Please run 'Export Pointer QA Report' from the Control Center first."
    exit 1
}

$report = Get-Content $reportPath | ConvertFrom-Json

Write-Host "--- PointyPal Pointer QA Report Summary ---" -ForegroundColor Cyan
Write-Host "Report ID: $($report.Report.ReportId)"
Write-Host "Created:   $($report.Report.CreatedAt)"
Write-Host "Version:   $($report.Report.AppVersion)"
Write-Host "Monitors:  $($report.Report.MonitorSummary)"
Write-Host ""
Write-Host "Accuracy Stats:" -ForegroundColor White
Write-Host "  Total Attempts: $($report.Report.TotalAttempts)"
Write-Host "  Correct:        $($report.Report.CorrectCount) ($($report.Report.CorrectPercent)%)"
Write-Host "  Close:          $($report.Report.CloseCount) ($($report.Report.ClosePercent)%)"
Write-Host "  Wrong:          $($report.Report.WrongCount) ($($report.Report.WrongPercent)%)"
Write-Host ""
Write-Host "Recommendation: $($report.Report.Recommendation)" -ForegroundColor ([System.ConsoleColor]::Green)

if ($args -contains "-Copy") {
    $artifactsDir = [System.IO.Path]::Combine((Get-Location).Path, "artifacts")
    if (-not (Test-Path $artifactsDir)) { New-Item -ItemType Directory -Path $artifactsDir | Out-Null }
    $destPath = [System.IO.Path]::Combine($artifactsDir, "pointer-qa-report.json")
    Copy-Item $reportPath $destPath
    Write-Host ""
    Write-Host "Report copied to artifacts/pointer-qa-report.json" -ForegroundColor Gray
}
