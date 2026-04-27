$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

& dotnet test .\PointyPal.sln
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    exit $exitCode
}

exit 0
