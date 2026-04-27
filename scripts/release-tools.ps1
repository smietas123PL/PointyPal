$ErrorActionPreference = "Stop"

function Get-PointyPalRepoRoot {
    param([string]$RepoRoot = "")

    if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
        return (Resolve-Path -LiteralPath $RepoRoot).Path
    }

    return (Split-Path -Parent $PSScriptRoot)
}

function Get-PointyPalBuildProp {
    param(
        [Parameter(Mandatory = $true)][string]$PropsPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not (Test-Path -LiteralPath $PropsPath)) {
        throw "Build props file not found: $PropsPath"
    }

    [xml]$xml = Get-Content -LiteralPath $PropsPath -Raw
    $node = $xml.SelectSingleNode("//*[local-name()='$Name']")
    if ($null -eq $node) {
        return ""
    }

    return $node.InnerText.Trim()
}

function Test-PointyPalVersionString {
    param([string]$Value)
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match '^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9][A-Za-z0-9.-]*)?$'
}

function Test-PointyPalReleaseLabel {
    param([string]$Value)
    return [string]::IsNullOrWhiteSpace($Value) -or $Value -match '^[A-Za-z0-9][A-Za-z0-9.-]*$'
}

function Test-PointyPalBuildChannel {
    param([string]$Value)
    return @("dev", "private-rc", "production-preview") -contains $Value
}

function Get-PointyPalGitCommit {
    param([string]$RepoRoot)

    if (-not [string]::IsNullOrWhiteSpace($env:POINTYPAL_GIT_COMMIT)) {
        return $env:POINTYPAL_GIT_COMMIT
    }

    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot ".git"))) {
        return ""
    }

    try {
        $commit = git -C $RepoRoot rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0) {
            return ($commit | Select-Object -First 1).Trim()
        }
    }
    catch {
        return ""
    }

    return ""
}

function Get-PointyPalReleaseMetadata {
    param(
        [string]$RepoRoot = "",
        [string]$BuildDate = "",
        [string]$GitCommit = ""
    )

    $root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
    $propsPath = Join-Path $root "PointyPal.Build.props"

    $version = Get-PointyPalBuildProp -PropsPath $propsPath -Name "Version"
    $channel = Get-PointyPalBuildProp -PropsPath $propsPath -Name "BuildChannel"
    $releaseLabel = Get-PointyPalBuildProp -PropsPath $propsPath -Name "ReleaseLabel"

    if (-not (Test-PointyPalVersionString -Value $version)) {
        throw "Invalid PointyPal Version in PointyPal.Build.props: $version"
    }

    if (-not (Test-PointyPalBuildChannel -Value $channel)) {
        throw "Invalid BuildChannel in PointyPal.Build.props: $channel"
    }

    if (-not (Test-PointyPalReleaseLabel -Value $releaseLabel)) {
        throw "Invalid ReleaseLabel in PointyPal.Build.props: $releaseLabel"
    }

    $resolvedBuildDate = $BuildDate
    if ([string]::IsNullOrWhiteSpace($resolvedBuildDate)) {
        $resolvedBuildDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
    }

    $resolvedGitCommit = $GitCommit
    if ([string]::IsNullOrWhiteSpace($resolvedGitCommit)) {
        $resolvedGitCommit = Get-PointyPalGitCommit -RepoRoot $root
    }

    return [pscustomobject][ordered]@{
        AppName = Get-PointyPalBuildProp -PropsPath $propsPath -Name "AppName"
        Version = $version
        BuildChannel = $channel
        ReleaseLabel = $releaseLabel
        BaselineDate = Get-PointyPalBuildProp -PropsPath $propsPath -Name "BaselineDate"
        BuildDate = $resolvedBuildDate
        GitCommit = $resolvedGitCommit
        WorkerContractVersion = Get-PointyPalBuildProp -PropsPath $propsPath -Name "WorkerContractVersion"
    }
}

function Get-PointyPalSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PointyPalInstallerFileName {
    param(
        [Parameter(Mandatory = $true)]$Metadata,
        [string]$RuntimeTarget = "win-x64"
    )

    $label = ""
    if (-not [string]::IsNullOrWhiteSpace($Metadata.ReleaseLabel)) {
        $label = "-$($Metadata.ReleaseLabel)"
    }

    return "$($Metadata.AppName)-v$($Metadata.Version)$label-$RuntimeTarget-setup.exe"
}

function Get-PointyPalInstallerExcludePatterns {
    return @(
        "config.json",
        "logs\*",
        "debug\*",
        "history\*",
        "usage\*",
        "secrets\*",
        "certs\*",
        "signing\*",
        ".env",
        ".env.*",
        "*.log",
        "*.tmp",
        "*.bak",
        "*.secret",
        "*.key",
        "*.pem",
        "*.pfx",
        "*.p12",
        "*.cer",
        "*.pvk",
        "*.spc",
        "*.pdb"
    )
}

function Get-PointyPalInnoCompiler {
    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")) | Out-Null
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Inno Setup 5\ISCC.exe")) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")) | Out-Null
        $candidates.Add((Join-Path $env:ProgramFiles "Inno Setup 5\ISCC.exe")) | Out-Null
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return ""
}

function Get-PointyPalSignTool {
    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $patterns = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $patterns.Add((Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin\*\x64\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin\*\x86\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path ${env:ProgramFiles(x86)} "Windows Kits\8.1\bin\x64\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path ${env:ProgramFiles(x86)} "Windows Kits\8.1\bin\x86\signtool.exe")) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $patterns.Add((Join-Path $env:ProgramFiles "Windows Kits\10\bin\*\x64\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path $env:ProgramFiles "Windows Kits\10\bin\*\x86\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path $env:ProgramFiles "Windows Kits\8.1\bin\x64\signtool.exe")) | Out-Null
        $patterns.Add((Join-Path $env:ProgramFiles "Windows Kits\8.1\bin\x86\signtool.exe")) | Out-Null
    }

    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($pattern in $patterns) {
        foreach ($candidate in @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue)) {
            $candidates.Add($candidate) | Out-Null
        }
    }

    $selected = $candidates |
        Sort-Object @{ Expression = { if ($_.FullName -match '\\x64\\') { 1 } else { 0 } }; Descending = $true }, FullName -Descending |
        Select-Object -First 1
    if ($null -ne $selected) {
        return $selected.FullName
    }

    return ""
}

function Resolve-PointyPalPortableExePath {
    param(
        [string]$RepoRoot = "",
        [string]$PortablePath = ""
    )

    $root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
    $resolved = $PortablePath
    if ([string]::IsNullOrWhiteSpace($resolved)) {
        $resolved = Join-Path $root "artifacts\PointyPal-portable"
    }

    if (-not [System.IO.Path]::IsPathRooted($resolved)) {
        $resolved = Join-Path $root $resolved
    }

    if ((Test-Path -LiteralPath $resolved -PathType Container) -or [string]::IsNullOrWhiteSpace([System.IO.Path]::GetExtension($resolved))) {
        return [System.IO.Path]::GetFullPath((Join-Path $resolved "PointyPal.exe"))
    }

    return [System.IO.Path]::GetFullPath($resolved)
}

function Resolve-PointyPalInstallerArtifactPath {
    param(
        [string]$RepoRoot = "",
        [string]$InstallerPath = "",
        [string]$RuntimeTarget = "win-x64"
    )

    $root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
    $metadata = Get-PointyPalReleaseMetadata -RepoRoot $root
    $resolved = $InstallerPath

    if ([string]::IsNullOrWhiteSpace($resolved)) {
        $resolved = Join-Path (Join-Path $root "artifacts\installer") (Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget)
    }
    elseif (-not [System.IO.Path]::IsPathRooted($resolved)) {
        $resolved = Join-Path $root $resolved
    }

    if (Test-Path -LiteralPath $resolved -PathType Container) {
        $expected = Join-Path $resolved (Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget)
        if (Test-Path -LiteralPath $expected) {
            return (Resolve-Path -LiteralPath $expected).Path
        }

        $setup = Get-ChildItem -LiteralPath $resolved -File -Filter "*setup.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -ne $setup) {
            return $setup.FullName
        }

        return [System.IO.Path]::GetFullPath($expected)
    }

    return [System.IO.Path]::GetFullPath($resolved)
}

function Get-PointyPalSignatureInfo {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $verifiedAt = (Get-Date).ToUniversalTime().ToString("o")

    if (-not (Test-Path -LiteralPath $fullPath)) {
        return [pscustomobject][ordered]@{
            path = $fullPath
            signed = $false
            signatureStatus = "Missing"
            signer = ""
            statusMessage = "File not found."
            timestamped = $false
            authenticodeStatus = "Missing"
            verifiedAt = $verifiedAt
        }
    }

    if ($null -eq (Get-Command "Get-AuthenticodeSignature" -ErrorAction SilentlyContinue)) {
        return [pscustomobject][ordered]@{
            path = $fullPath
            signed = $false
            signatureStatus = "Unknown"
            signer = ""
            statusMessage = "Get-AuthenticodeSignature is not available on this system."
            timestamped = $false
            authenticodeStatus = "Unknown"
            verifiedAt = $verifiedAt
        }
    }

    try {
        $signature = Get-AuthenticodeSignature -LiteralPath $fullPath -ErrorAction Stop
    }
    catch {
        return [pscustomobject][ordered]@{
            path = $fullPath
            signed = $false
            signatureStatus = "Unknown"
            signer = ""
            statusMessage = $_.Exception.Message
            timestamped = $false
            authenticodeStatus = "Unknown"
            verifiedAt = $verifiedAt
        }
    }

    $authStatus = "$($signature.Status)"
    $signer = ""
    if ($null -ne $signature.SignerCertificate) {
        $signer = $signature.SignerCertificate.Subject
    }

    $status = "Invalid"
    $signed = $false
    if ($authStatus -eq "Valid") {
        $status = "Valid"
        $signed = $true
    }
    elseif ($authStatus -eq "NotSigned") {
        $status = "Unsigned"
    }

    return [pscustomobject][ordered]@{
        path = $fullPath
        signed = $signed
        signatureStatus = $status
        signer = $signer
        statusMessage = $signature.StatusMessage
        timestamped = ($null -ne $signature.TimeStamperCertificate)
        authenticodeStatus = $authStatus
        verifiedAt = $verifiedAt
    }
}

function New-PointyPalSigningManifestSection {
    param(
        [Parameter(Mandatory = $true)][string]$PortableExePath,
        [string]$InstallerPath = ""
    )

    $portableSignature = Get-PointyPalSignatureInfo -Path $PortableExePath
    $installerSignature = if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
        [pscustomobject][ordered]@{
            signed = $false
            signatureStatus = "Missing"
            signer = ""
            timestamped = $false
        }
    }
    else {
        Get-PointyPalSignatureInfo -Path $InstallerPath
    }

    return [pscustomobject][ordered]@{
        portableExeSigned = [bool]$portableSignature.signed
        portableExeSignatureStatus = $portableSignature.signatureStatus
        portableExeSigner = $portableSignature.signer
        installerSigned = [bool]$installerSignature.signed
        installerSignatureStatus = $installerSignature.signatureStatus
        installerSigner = $installerSignature.signer
        timestamped = [bool]($portableSignature.timestamped -or $installerSignature.timestamped)
        verifiedAt = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function New-PointyPalSignToolArguments {
    param(
        [Parameter(Mandatory = $true)][ValidateSet("PFX", "Thumbprint")][string]$Mode,
        [string]$CertPath = "",
        [string]$CertPassword = "",
        [string]$CertThumbprint = "",
        [string]$TimestampUrl = "http://timestamp.digicert.com",
        [Parameter(Mandatory = $true)][string]$FilePath
    )

    $args = New-Object System.Collections.Generic.List[string]
    $args.Add("sign") | Out-Null
    $args.Add("/fd") | Out-Null
    $args.Add("SHA256") | Out-Null
    $args.Add("/tr") | Out-Null
    $args.Add($TimestampUrl) | Out-Null
    $args.Add("/td") | Out-Null
    $args.Add("SHA256") | Out-Null

    if ($Mode -eq "PFX") {
        $args.Add("/f") | Out-Null
        $args.Add($CertPath) | Out-Null
        $args.Add("/p") | Out-Null
        $args.Add($CertPassword) | Out-Null
    }
    else {
        $args.Add("/s") | Out-Null
        $args.Add("My") | Out-Null
        $args.Add("/sha1") | Out-Null
        $args.Add($CertThumbprint) | Out-Null
    }

    $args.Add($FilePath) | Out-Null
    return @($args)
}

function Get-PointyPalRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $baseFull = [System.IO.Path]::GetFullPath($BasePath).TrimEnd([char[]]@('\', '/'))
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    return $pathFull.Substring($baseFull.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
}

function Test-PointyPalPackageFileAllowed {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $path = $RelativePath.Replace('\', '/').TrimStart('/')
    $segments = @($path.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.ToLowerInvariant() })
    $leaf = [System.IO.Path]::GetFileName($path).ToLowerInvariant()

    if ($segments -contains ".git" -or $segments -contains "bin" -or $segments -contains "obj") { return $false }
    if ($segments -contains "logs" -or $segments -contains "debug" -or $segments -contains "history" -or $segments -contains "usage" -or $segments -contains "secrets" -or $segments -contains "certs" -or $segments -contains "signing" -or $segments -contains "recordings" -or $segments -contains "screenshots") { return $false }
    if ($leaf -eq "config.json" -or $leaf -eq ".env" -or $leaf.StartsWith(".env.") -or $leaf.StartsWith("latest-")) { return $false }
    if ($leaf.EndsWith(".pdb") -or $leaf.EndsWith(".log") -or $leaf.EndsWith(".tmp") -or $leaf.EndsWith(".bak")) { return $false }
    if ($leaf.EndsWith(".secret") -or $leaf.EndsWith(".key") -or $leaf.EndsWith(".pem") -or $leaf.EndsWith(".pfx")) { return $false }
    if ($leaf.EndsWith(".p12") -or $leaf.EndsWith(".cer") -or $leaf.EndsWith(".pvk") -or $leaf.EndsWith(".spc")) { return $false }

    return $true
}

function Test-PointyPalTextFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $extension = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
    return @(".txt", ".md", ".json", ".xml", ".config", ".ps1", ".cmd", ".bat") -contains $extension
}

function Find-PointyPalObviousSecrets {
    param([Parameter(Mandatory = $true)][string]$PortableDir)

    $patterns = @(
        '(?i)\b(?:sk|pk|rk|ak)-[A-Za-z0-9_\-]{16,}',
        '(?i)(?:api[_-]?key|x-api-key|x-pointypal-client-key|authorization|token|secret|password)\s*[:=]\s*["'']?(?!YOUR_|PLACEHOLDER|REDACTED|not available|<)[A-Za-z0-9_\-/.+=]{16,}'
    )

    $findings = @()
    foreach ($file in Get-ChildItem -LiteralPath $PortableDir -Recurse -File) {
        if (-not (Test-PointyPalTextFile -Path $file.FullName)) {
            continue
        }

        $relative = Get-PointyPalRelativePath -BasePath $PortableDir -Path $file.FullName
        try {
            $text = Get-Content -LiteralPath $file.FullName -Raw
            foreach ($pattern in $patterns) {
                if ($text -match $pattern) {
                    $findings += [pscustomobject]@{ Path = $relative; Pattern = $pattern }
                }
            }
        }
        catch {
            $findings += [pscustomobject]@{ Path = $relative; Pattern = "read-failed" }
        }
    }

    return @($findings)
}

function Get-PointyPalPortableManifestFiles {
    param([Parameter(Mandatory = $true)][string]$PortableDir)

    $files = New-Object System.Collections.Generic.List[object]
    foreach ($file in Get-ChildItem -LiteralPath $PortableDir -Recurse -File) {
        $relative = Get-PointyPalRelativePath -BasePath $PortableDir -Path $file.FullName
        if ($relative -eq "release-manifest.json" -or $relative -eq "checksums.txt") {
            continue
        }

        if (Test-PointyPalPackageFileAllowed -RelativePath $relative) {
            $files.Add([pscustomobject]@{ File = $file; RelativePath = $relative }) | Out-Null
        }
    }

    return @($files | Sort-Object RelativePath)
}

function New-PointyPalChecksums {
    param([Parameter(Mandatory = $true)][string]$PortableDir)

    $targets = @(
        "PointyPal.exe",
        "PointyPal.dll",
        "config.example.json",
        "release-manifest.json",
        "README-FIRST-RUN.md",
        "NOTICE.txt"
    )

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($target in $targets) {
        $path = Join-Path $PortableDir $target
        if (Test-Path -LiteralPath $path) {
            $lines.Add("SHA256  $target  $(Get-PointyPalSha256 -Path $path)") | Out-Null
        }
    }

    $checksumsPath = Join-Path $PortableDir "checksums.txt"
    $lines | Set-Content -LiteralPath $checksumsPath -Encoding UTF8
    return $checksumsPath
}

function New-PointyPalReleaseManifest {
    param(
        [Parameter(Mandatory = $true)][string]$PortableDir,
        [string]$RepoRoot = "",
        [string]$BuildDate = "",
        [string]$RuntimeTarget = "win-x64",
        [bool]$SelfContained = $false
    )

    if (-not (Test-Path -LiteralPath $PortableDir)) {
        throw "Portable directory not found: $PortableDir"
    }

    $root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
    $metadata = Get-PointyPalReleaseMetadata -RepoRoot $root -BuildDate $BuildDate
    $important = @(
        "PointyPal.exe",
        "PointyPal.dll",
        "PointyPal.deps.json",
        "PointyPal.runtimeconfig.json",
        "config.example.json",
        "NOTICE.txt",
        "README-FIRST-RUN.md"
    )

    $files = foreach ($item in Get-PointyPalPortableManifestFiles -PortableDir $PortableDir) {
        [pscustomobject][ordered]@{
            path = $item.RelativePath
            sizeBytes = $item.File.Length
            sha256 = Get-PointyPalSha256 -Path $item.File.FullName
            important = $important -contains $item.RelativePath -or $item.RelativePath.StartsWith("docs/")
        }
    }

    $docsDir = Join-Path $PortableDir "docs"
    $portableExePath = Join-Path $PortableDir "PointyPal.exe"
    $installerPath = Join-Path (Join-Path $root "artifacts\installer") (Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget $RuntimeTarget)
    $manifest = [pscustomobject][ordered]@{
        schemaVersion = 1
        appName = $metadata.AppName
        version = $metadata.Version
        releaseLabel = $metadata.ReleaseLabel
        buildChannel = $metadata.BuildChannel
        baselineDate = $metadata.BaselineDate
        buildDate = $metadata.BuildDate
        gitCommit = $metadata.GitCommit
        workerContractVersion = $metadata.WorkerContractVersion
        runtimeTarget = $RuntimeTarget
        selfContained = [bool]$SelfContained
        selfContainedText = if ($SelfContained) { "yes" } else { "no" }
        docsIncluded = (Test-Path -LiteralPath $docsDir) -and @((Get-ChildItem -LiteralPath $docsDir -File -Recurse -ErrorAction SilentlyContinue)).Count -gt 0
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        signing = New-PointyPalSigningManifestSection -PortableExePath $portableExePath -InstallerPath $installerPath
        files = @($files)
    }

    $manifestPath = Join-Path $PortableDir "release-manifest.json"
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    New-PointyPalChecksums -PortableDir $PortableDir | Out-Null
    return $manifest
}

function New-PointyPalInstallerManifest {
    param(
        [Parameter(Mandatory = $true)][string]$InstallerPath,
        [string]$RepoRoot = "",
        [string]$BuildDate = "",
        [string]$RuntimeTarget = "win-x64"
    )

    if (-not (Test-Path -LiteralPath $InstallerPath)) {
        throw "Installer not found: $InstallerPath"
    }

    $root = Get-PointyPalRepoRoot -RepoRoot $RepoRoot
    $metadata = Get-PointyPalReleaseMetadata -RepoRoot $root -BuildDate $BuildDate
    $installer = Get-Item -LiteralPath $InstallerPath
    $sha256 = Get-PointyPalSha256 -Path $installer.FullName
    $signature = Get-PointyPalSignatureInfo -Path $installer.FullName

    $manifest = [pscustomobject][ordered]@{
        schemaVersion = 1
        appName = $metadata.AppName
        version = $metadata.Version
        releaseLabel = $metadata.ReleaseLabel
        buildChannel = $metadata.BuildChannel
        buildDate = $metadata.BuildDate
        runtimeTarget = $RuntimeTarget
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        installer = [pscustomobject][ordered]@{
            filename = $installer.Name
            sizeBytes = $installer.Length
            sha256 = $sha256
            signed = [bool]$signature.signed
            signatureStatus = $signature.signatureStatus
            signer = $signature.signer
            timestamped = [bool]$signature.timestamped
            signatureVerifiedAt = $signature.verifiedAt
        }
    }

    $installerDir = Split-Path -Parent $installer.FullName
    $manifestPath = Join-Path $installerDir "installer-manifest.json"
    $checksumsPath = Join-Path $installerDir "installer-checksums.txt"

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    "SHA256  $($installer.Name)  $sha256" | Set-Content -LiteralPath $checksumsPath -Encoding UTF8

    return $manifest
}

function Copy-PointyPalFilteredPortableFiles {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$DestinationDir
    )

    if (Test-Path -LiteralPath $DestinationDir) {
        Remove-Item -LiteralPath $DestinationDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestinationDir | Out-Null

    foreach ($file in Get-ChildItem -LiteralPath $SourceDir -Recurse -File) {
        $relative = Get-PointyPalRelativePath -BasePath $SourceDir -Path $file.FullName
        if (-not (Test-PointyPalPackageFileAllowed -RelativePath $relative)) {
            continue
        }

        $dest = Join-Path $DestinationDir $relative
        $destParent = Split-Path -Parent $dest
        if (-not (Test-Path -LiteralPath $destParent)) {
            New-Item -ItemType Directory -Path $destParent | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
    }
}

function Test-PointyPalDesktopRuntimeAvailable {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        return $false
    }

    try {
        $runtimes = & dotnet --list-runtimes
        return @($runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App\s+8\.' }).Count -gt 0
    }
    catch {
        return $false
    }
}
