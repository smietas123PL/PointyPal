param(
    [switch]$SelfContained,
    [switch]$IncludeInstaller,
    [switch]$Sign,
    [switch]$VerifySignatures,
    [switch]$RequireSigned,
    [string]$BuildDate = "",
    [string]$RuntimeTarget = "win-x64"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "release-tools.ps1")

$repoRoot = Get-PointyPalRepoRoot
$metadata = Get-PointyPalReleaseMetadata -RepoRoot $repoRoot -BuildDate $BuildDate
$artifactFolder = Join-Path $repoRoot "artifacts\PointyPal-portable"
$manifestPath = Join-Path $artifactFolder "release-manifest.json"
$packageSuffix = if ($SelfContained) { "self-contained-portable" } else { "portable" }
$zipPath = Join-Path (Join-Path $repoRoot "artifacts") "$($metadata.AppName)-v$($metadata.Version)-$($metadata.ReleaseLabel)-$RuntimeTarget-$packageSuffix.zip"
$installerPath = Join-Path (Join-Path $repoRoot "artifacts\installer") (Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget)
$stepResults = New-Object System.Collections.Generic.List[object]
$testSummary = "not captured"
$shouldVerifySignatures = $VerifySignatures.IsPresent -or $RequireSigned.IsPresent

function Invoke-ReleaseStep {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "== $Name ==" -ForegroundColor Cyan
    $started = Get-Date
    try {
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            throw "$Name failed with exit code $exitCode"
        }

        $script:stepResults.Add([pscustomobject]@{ Name = $Name; Status = "PASS"; DurationSeconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) }) | Out-Null
        return @($output)
    }
    catch {
        $script:stepResults.Add([pscustomobject]@{ Name = $Name; Status = "FAIL"; DurationSeconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) }) | Out-Null
        Write-Host ""
        Write-Host "Release check FAILED at step: $Name" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        throw
    }
}

try {
    $buildOutput = Invoke-ReleaseStep -Name "Build and tests" -Command { & (Join-Path $PSScriptRoot "build.ps1") }
    $summaryLines = @($buildOutput | Where-Object { "$_" -match 'Passed!|Failed!|Total tests|Test summary|Passed:|Powodzenie|Niepowodzenie|powodzenie:|niepowodzenie:' } | Select-Object -Last 5)
    if ($summaryLines.Count -gt 0) {
        $testSummary = ($summaryLines -join " ")
    }

    Invoke-ReleaseStep -Name "Publish portable" -Command {
        & (Join-Path $PSScriptRoot "publish-portable.ps1") `
            -SelfContained:$SelfContained.IsPresent `
            -BuildDate $metadata.BuildDate `
            -RuntimeTarget $RuntimeTarget
    } | Out-Null

    if ($Sign) {
        Invoke-ReleaseStep -Name "Sign portable artifacts" -Command {
            & (Join-Path $PSScriptRoot "sign-artifacts.ps1") `
                -PortablePath $artifactFolder `
                -InstallerPath $installerPath `
                -SkipIfNoCertificate
        } | Out-Null
    }

    Invoke-ReleaseStep -Name "Create release manifest" -Command {
        & (Join-Path $PSScriptRoot "create-release-manifest.ps1") `
            -PortableDir $artifactFolder `
            -RuntimeTarget $RuntimeTarget `
            -BuildDate $metadata.BuildDate `
            -SelfContained:$SelfContained.IsPresent
    } | Out-Null

    if ($IncludeInstaller) {
        Invoke-ReleaseStep -Name "Build installer" -Command {
            & (Join-Path $PSScriptRoot "build-installer.ps1") `
                -BuildDate $metadata.BuildDate `
                -RuntimeTarget $RuntimeTarget
        } | Out-Null

        if ($Sign) {
            Invoke-ReleaseStep -Name "Sign installer artifacts" -Command {
                & (Join-Path $PSScriptRoot "sign-artifacts.ps1") `
                    -PortablePath $artifactFolder `
                    -InstallerPath $installerPath `
                    -SkipIfNoCertificate
            } | Out-Null
        }

        Invoke-ReleaseStep -Name "Refresh installer manifest" -Command {
            & (Join-Path $PSScriptRoot "create-installer-manifest.ps1") `
                -InstallerPath $installerPath `
                -RuntimeTarget $RuntimeTarget `
                -BuildDate $metadata.BuildDate
        } | Out-Null

        Invoke-ReleaseStep -Name "Refresh release manifest" -Command {
            & (Join-Path $PSScriptRoot "create-release-manifest.ps1") `
                -PortableDir $artifactFolder `
                -RuntimeTarget $RuntimeTarget `
                -BuildDate $metadata.BuildDate `
                -SelfContained:$SelfContained.IsPresent
        } | Out-Null
    }

    Invoke-ReleaseStep -Name "Validate portable" -Command {
        & (Join-Path $PSScriptRoot "validate-portable.ps1") -PortableDir $artifactFolder
    } | Out-Null

    Invoke-ReleaseStep -Name "Package portable ZIP" -Command {
        & (Join-Path $PSScriptRoot "package-portable-zip.ps1") `
            -UseExistingArtifacts `
            -SelfContained:$SelfContained.IsPresent `
            -BuildDate $metadata.BuildDate `
            -RuntimeTarget $RuntimeTarget
    } | Out-Null

    if ($IncludeInstaller) {
        Invoke-ReleaseStep -Name "Validate installer" -Command {
            & (Join-Path $PSScriptRoot "validate-installer.ps1") `
                -RuntimeTarget $RuntimeTarget
        } | Out-Null
    }

    if ($shouldVerifySignatures) {
        Invoke-ReleaseStep -Name "Verify signatures" -Command {
            & (Join-Path $PSScriptRoot "verify-signatures.ps1") `
                -PortablePath $artifactFolder `
                -InstallerPath $installerPath `
                -RequireSigned:$RequireSigned.IsPresent
        } | Out-Null
    }

    Write-Host ""
    Write-Host "Release check PASSED." -ForegroundColor Green
}
finally {
    Write-Host ""
    Write-Host "Summary" -ForegroundColor Cyan
    $stepResults | ForEach-Object { Write-Host "$($_.Status)  $($_.Name)  ($($_.DurationSeconds)s)" }
    Write-Host "Artifact folder: $artifactFolder"
    Write-Host "ZIP path: $zipPath"
    if ($IncludeInstaller) {
        Write-Host "Installer path: $installerPath"
    }
    Write-Host "Manifest path: $manifestPath"
    Write-Host "Test result summary: $testSummary"
}
