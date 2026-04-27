# PointyPal RC Validation Script
# Automates the collection of health and readiness metrics.

$ErrorActionPreference = "Stop"

Write-Host "--- PointyPal RC Readiness Check ---" -ForegroundColor Cyan

# 1. Run Self-Test (CommandLine)
Write-Host "[1/4] Running Self-Test..."
$selfTestResult = Start-Process -FilePath ".\bin\Debug\net8.0-windows\PointyPal.exe" -ArgumentList "--self-test" -Wait -PassThru
if ($selfTestResult.ExitCode -ne 0) {
    Write-Error "Self-test failed with exit code $($selfTestResult.ExitCode)"
}
Write-Host "Self-test completed." -ForegroundColor Green

# 2. Backup Current Config
Write-Host "[2/4] Backing up config..."
Start-Process -FilePath ".\bin\Debug\net8.0-windows\PointyPal.exe" -ArgumentList "--backup-config" -Wait
Write-Host "Backup created." -ForegroundColor Green

# 3. Check for Readiness Report
Write-Host "[3/4] Locating Readiness Report..."
$reportPath = "$env:APPDATA\PointyPal\debug\rc-readiness-report.json"
if (Test-Path $reportPath) {
    $report = Get-Content $reportPath | ConvertFrom-Json
    Write-Host "Readiness Score: $($report.Score)" -ForegroundColor Yellow
    Write-Host "Status: $($report.Status)"
    if ($report.BlockingIssues.Count -gt 0) {
        Write-Host "Blocking Issues:" -ForegroundColor Red
        $report.BlockingIssues | ForEach-Object { Write-Host " - $_" }
    }
} else {
    Write-Host "Readiness report not found. Start the app normally and run 'Run RC Readiness Check' in Control Center." -ForegroundColor Gray
}

# 4. Final Instructions
Write-Host "`n[4/4] Final Validation Steps:" -ForegroundColor Cyan
Write-Host "1. Review docs/rc-checklist.md"
Write-Host "2. Run a 10-minute soak test from Control Center"
Write-Host "3. Verify F9 overlay shows consistent metrics"

Write-Host "`n--- Readiness Check Finished ---"
