$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

& dotnet build .\PointyPal.sln
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -ne 0) {
    exit $buildExitCode
}

& dotnet test .\PointyPal.sln
$testExitCode = $LASTEXITCODE

if ($testExitCode -ne 0) {
    exit $testExitCode
}

exit 0
