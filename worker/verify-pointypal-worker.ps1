<#
.SYNOPSIS
  PointyPal Worker verification script.

.DESCRIPTION
  Verifies public Worker endpoints, protected endpoint authentication, and optionally a real /chat call.

  The script never writes or prints your Worker Client Key.
  Use an environment variable for the key when possible:
    $env:POINTYPAL_CLIENT_KEY = "..."

.EXAMPLE
  .\verify-pointypal-worker.ps1 -WorkerUrl "https://pointypal-worker-production.example.workers.dev"

.EXAMPLE
  $env:POINTYPAL_CLIENT_KEY = "YOUR_POINTYPAL_CLIENT_KEY"
  .\verify-pointypal-worker.ps1 -WorkerUrl "https://pointypal-worker-production.example.workers.dev" -RunRealChat

.EXAMPLE
  .\verify-pointypal-worker.ps1 -WorkerUrl "https://pointypal-worker-production.example.workers.dev" -PromptForClientKey -RunRealChat
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$WorkerUrl,

    # Prefer $env:POINTYPAL_CLIENT_KEY or -PromptForClientKey over passing secrets in command history.
    [string]$ClientKey = $env:POINTYPAL_CLIENT_KEY,

    [switch]$PromptForClientKey,

    # Runs a real Claude-backed /chat request. This may create a small provider cost.
    [switch]$RunRealChat,

    # Also checks /tts and /transcribe reject missing/wrong keys. No real provider calls are made by these auth checks.
    [switch]$CheckAllProtectedEndpoints,

    # Optional path for JSON report. The report never contains ClientKey.
    [string]$ReportPath = ""
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Convert-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][System.Security.SecureString]$SecureString)
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Normalize-WorkerUrl {
    param([string]$Url)
    $trimmed = $Url.Trim().TrimEnd('/')
    if (-not ($trimmed.StartsWith("https://") -or $trimmed.StartsWith("http://"))) {
        throw "WorkerUrl must start with https:// or http://"
    }
    return $trimmed
}

function Invoke-WorkerRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Body = $null,
        [hashtable]$Headers = @{}
    )

    Add-Type -AssemblyName System.Net.Http | Out-Null

    $url = "$script:NormalizedWorkerUrl$Path"
    $httpClient = New-Object System.Net.Http.HttpClient
    $request = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method.ToUpperInvariant())), $url

    try {
        foreach ($key in $Headers.Keys) {
            [void]$request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key])
        }

        if ($null -ne $Body) {
            # StringContent uses UTF-8 without BOM; avoids PowerShell file BOM issues.
            $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, "application/json")
        }

        $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
        $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        $json = $null
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            try { $json = $raw | ConvertFrom-Json } catch { $json = $null }
        }

        return [pscustomobject]@{
            Method      = $Method.ToUpperInvariant()
            Path        = $Path
            StatusCode  = [int]$response.StatusCode
            IsSuccess   = $response.IsSuccessStatusCode
            RawBody     = $raw
            Json        = $json
            RequestId   = if ($json -and $json.PSObject.Properties.Name -contains "requestId") { [string]$json.requestId } else { "" }
        }
    }
    finally {
        if ($request) { $request.Dispose() }
        if ($httpClient) { $httpClient.Dispose() }
    }
}

function Add-TestResult {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$Details = "",
        [string]$RequestId = ""
    )

    $script:Results += [pscustomobject]@{
        Name      = $Name
        Status    = $Status
        Details   = $Details
        RequestId = $RequestId
    }

    $prefix = switch ($Status) {
        "PASS" { "[PASS]" }
        "FAIL" { "[FAIL]" }
        "SKIP" { "[SKIP]" }
        default { "[INFO]" }
    }

    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Gray" }
    }

    $line = "$prefix $Name"
    if ($Details) { $line += " - $Details" }
    if ($RequestId) { $line += " (requestId: $RequestId)" }
    Write-Host $line -ForegroundColor $color
}

function Test-ExpectedUnauthorized {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Body,
        [hashtable]$Headers = @{}
    )

    $response = Invoke-WorkerRequest -Method "POST" -Path $Path -Body $Body -Headers $Headers
    $errorName = if ($response.Json -and $response.Json.PSObject.Properties.Name -contains "error") { [string]$response.Json.error } else { "" }

    if ($response.StatusCode -eq 401 -and $errorName -eq "unauthorized") {
        Add-TestResult -Name $Name -Status "PASS" -Details "401 unauthorized as expected" -RequestId $response.RequestId
    }
    else {
        Add-TestResult -Name $Name -Status "FAIL" -Details "Expected 401 unauthorized, got HTTP $($response.StatusCode): $($response.RawBody)" -RequestId $response.RequestId
    }
}

$script:Results = @()
$script:NormalizedWorkerUrl = Normalize-WorkerUrl -Url $WorkerUrl

if ($PromptForClientKey) {
    $secure = Read-Host "Enter PointyPal Worker Client Key" -AsSecureString
    $ClientKey = Convert-SecureStringToPlainText -SecureString $secure
}

Write-Host ""
Write-Host "PointyPal Worker verification" -ForegroundColor Cyan
Write-Host "WorkerUrl: $script:NormalizedWorkerUrl" -ForegroundColor Cyan
Write-Host "Real /chat test: $($RunRealChat.IsPresent)" -ForegroundColor Cyan
Write-Host ""

# Bodies are compact JSON strings to avoid PowerShell multiline/BOM issues.
$chatBody = '{"userText":"Hello from PointyPal auth test.","interactionMode":"NoPoint"}'
$ttsBody = '{"text":"PointyPal TTS auth test.","voiceId":"test-voice-id","modelId":"eleven_flash_v2_5","outputFormat":"mp3_44100_128"}'
$transcribeBody = '{"audioBase64":"AAAA","audioMimeType":"audio/wav","language":"auto"}'

# 1. Public /health
try {
    $health = Invoke-WorkerRequest -Method "GET" -Path "/health"
    if ($health.StatusCode -eq 200 -and $health.Json -and $health.Json.ok -eq $true) {
        $version = if ($health.Json.PSObject.Properties.Name -contains "version") { $health.Json.version } else { "unknown" }
        Add-TestResult -Name "GET /health" -Status "PASS" -Details "ok=true, version=$version"
    }
    else {
        Add-TestResult -Name "GET /health" -Status "FAIL" -Details "Expected 200 ok=true, got HTTP $($health.StatusCode): $($health.RawBody)"
    }
}
catch {
    Add-TestResult -Name "GET /health" -Status "FAIL" -Details $_.Exception.Message
}

# 2. Public /version
try {
    $versionResponse = Invoke-WorkerRequest -Method "GET" -Path "/version"
    if ($versionResponse.StatusCode -eq 200 -and $versionResponse.Json -and ($versionResponse.Json.PSObject.Properties.Name -contains "version")) {
        Add-TestResult -Name "GET /version" -Status "PASS" -Details "version=$($versionResponse.Json.version)" -RequestId $versionResponse.RequestId
    }
    else {
        Add-TestResult -Name "GET /version" -Status "FAIL" -Details "Expected 200 with version, got HTTP $($versionResponse.StatusCode): $($versionResponse.RawBody)" -RequestId $versionResponse.RequestId
    }
}
catch {
    Add-TestResult -Name "GET /version" -Status "FAIL" -Details $_.Exception.Message
}

# 3. Protected /chat auth checks
try {
    Test-ExpectedUnauthorized -Name "POST /chat without key" -Path "/chat" -Body $chatBody
    Test-ExpectedUnauthorized -Name "POST /chat with wrong key" -Path "/chat" -Body $chatBody -Headers @{ "X-PointyPal-Client-Key" = "WRONG_KEY" }
}
catch {
    Add-TestResult -Name "POST /chat auth checks" -Status "FAIL" -Details $_.Exception.Message
}

# 4. Optional protected endpoint negative auth checks
if ($CheckAllProtectedEndpoints) {
    try {
        Test-ExpectedUnauthorized -Name "POST /tts without key" -Path "/tts" -Body $ttsBody
        Test-ExpectedUnauthorized -Name "POST /tts with wrong key" -Path "/tts" -Body $ttsBody -Headers @{ "X-PointyPal-Client-Key" = "WRONG_KEY" }
        Test-ExpectedUnauthorized -Name "POST /transcribe without key" -Path "/transcribe" -Body $transcribeBody
        Test-ExpectedUnauthorized -Name "POST /transcribe with wrong key" -Path "/transcribe" -Body $transcribeBody -Headers @{ "X-PointyPal-Client-Key" = "WRONG_KEY" }
    }
    catch {
        Add-TestResult -Name "Protected endpoint auth checks" -Status "FAIL" -Details $_.Exception.Message
    }
}
else {
    Add-TestResult -Name "Additional /tts and /transcribe auth checks" -Status "SKIP" -Details "Pass -CheckAllProtectedEndpoints to run."
}

# 5. Optional real /chat test with correct key
if ($RunRealChat) {
    if ([string]::IsNullOrWhiteSpace($ClientKey)) {
        Add-TestResult -Name "POST /chat with real key" -Status "FAIL" -Details "Client key missing. Set `$env:POINTYPAL_CLIENT_KEY or pass -PromptForClientKey."
    }
    else {
        try {
            $realChat = Invoke-WorkerRequest -Method "POST" -Path "/chat" -Body $chatBody -Headers @{ "X-PointyPal-Client-Key" = $ClientKey }
            if ($realChat.StatusCode -eq 401) {
                Add-TestResult -Name "POST /chat with real key" -Status "FAIL" -Details "Got 401. Worker Client Key was rejected." -RequestId $realChat.RequestId
            }
            elseif ($realChat.StatusCode -ge 200 -and $realChat.StatusCode -lt 300) {
                Add-TestResult -Name "POST /chat with real key" -Status "PASS" -Details "HTTP $($realChat.StatusCode). Auth and provider call succeeded." -RequestId $realChat.RequestId
            }
            else {
                # Auth worked if status is not 401, but provider/validation may have failed.
                Add-TestResult -Name "POST /chat with real key" -Status "FAIL" -Details "Auth likely passed, but Worker returned HTTP $($realChat.StatusCode): $($realChat.RawBody)" -RequestId $realChat.RequestId
            }
        }
        catch {
            Add-TestResult -Name "POST /chat with real key" -Status "FAIL" -Details $_.Exception.Message
        }
    }
}
else {
    Add-TestResult -Name "POST /chat with real key" -Status "SKIP" -Details "Pass -RunRealChat to run a real provider-backed chat test."
}

# Summary
$passed = @($script:Results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = @($script:Results | Where-Object { $_.Status -eq "SKIP" }).Count
$total = $script:Results.Count

Write-Host ""
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "PASS: $passed  FAIL: $failed  SKIP: $skipped  TOTAL: $total" -ForegroundColor Cyan

if ($ReportPath) {
    $report = [pscustomobject]@{
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        workerUrl   = $script:NormalizedWorkerUrl
        runRealChat = [bool]$RunRealChat
        results     = $script:Results
        summary     = [pscustomobject]@{
            passed  = $passed
            failed  = $failed
            skipped = $skipped
            total   = $total
        }
    }
    $reportJson = $report | ConvertTo-Json -Depth 6
    $dir = Split-Path -Parent $ReportPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath (Split-Path -Parent $ReportPath) -ErrorAction SilentlyContinue), "") 2>$null
    $reportJson | Set-Content -Path $ReportPath -Encoding UTF8
    Write-Host "Report written: $ReportPath" -ForegroundColor Cyan
}

if ($failed -gt 0) {
    exit 1
}

exit 0
