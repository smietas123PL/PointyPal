<# 
.SYNOPSIS
  Verifies a PointyPal Cloudflare Worker deployment.

.DESCRIPTION
  Checks public endpoints (/health, /version), protected endpoint authentication,
  and optionally runs a real provider-backed /chat request.

  The script never prints the client key. It accepts the key either via:
    -ClientKey "..."
  or environment variable:
    $env:POINTYPAL_CLIENT_KEY
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$WorkerUrl,

    [string]$ClientKey = $env:POINTYPAL_CLIENT_KEY,

    [switch]$RunRealChat,

    [switch]$CheckAllProtectedEndpoints,

    [string]$ReportPath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

try {
    Add-Type -AssemblyName System.Net.Http | Out-Null
} catch {
    Write-Host "Failed to load System.Net.Http: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$script:Results = New-Object System.Collections.Generic.List[object]

function Normalize-WorkerUrl {
    param([string]$Url)
    $u = $Url.Trim()
    while ($u.EndsWith("/")) {
        $u = $u.Substring(0, $u.Length - 1)
    }
    return $u
}

function New-JsonContent {
    param([string]$Json)
    return New-Object System.Net.Http.StringContent($Json, [System.Text.Encoding]::UTF8, "application/json")
}

function ConvertFrom-JsonSafe {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    try {
        return $Text | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Resolve-PathOrLiteral {
    param([string]$Path)

    $parent = Split-Path -Parent $Path
    $leaf = Split-Path -Leaf $Path

    if ([string]::IsNullOrWhiteSpace($parent)) {
        return (Join-Path (Get-Location) $leaf)
    }

    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    return (Join-Path (Resolve-Path $parent) $leaf)
}

function Invoke-WorkerRequest {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST")]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string]$JsonBody,

        [hashtable]$Headers
    )

    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(60)

    $uri = "$script:BaseUrl$Path"
    $httpMethod = if ($Method -eq "GET") { [System.Net.Http.HttpMethod]::Get } else { [System.Net.Http.HttpMethod]::Post }
    $request = New-Object System.Net.Http.HttpRequestMessage($httpMethod, $uri)

    if ($Headers) {
        foreach ($key in $Headers.Keys) {
            [void]$request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key])
        }
    }

    # Important: only attach a body for POST. GET requests with a content body fail on .NET/PowerShell.
    if ($Method -eq "POST" -and $null -ne $JsonBody) {
        $request.Content = New-JsonContent -Json $JsonBody
    }

    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Success    = $response.IsSuccessStatusCode
            Body       = $content
            Json       = ConvertFrom-JsonSafe -Text $content
            Error      = $null
        }
    } catch {
        return [pscustomobject]@{
            StatusCode = 0
            Success    = $false
            Body       = ""
            Json       = $null
            Error      = $_.Exception.Message
        }
    } finally {
        if ($request) { $request.Dispose() }
        if ($client) { $client.Dispose() }
    }
}

function Add-Result {
    param(
        [string]$Name,
        [ValidateSet("PASS", "FAIL", "SKIP", "WARN")]
        [string]$Status,
        [string]$Message,
        [int]$StatusCode = 0,
        [string]$RequestId = ""
    )

    $item = [pscustomobject]@{
        Name       = $Name
        Status     = $Status
        Message    = $Message
        StatusCode = $StatusCode
        RequestId  = $RequestId
    }

    $script:Results.Add($item) | Out-Null

    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "WARN" { "Yellow" }
        "SKIP" { "DarkYellow" }
        default { "White" }
    }

    $suffix = ""
    if (-not [string]::IsNullOrWhiteSpace($RequestId)) {
        $suffix = " (requestId: $RequestId)"
    }

    Write-Host "[$Status] $Name - $Message$suffix" -ForegroundColor $color
}

function Get-RequestId {
    param($Json)
    if ($null -ne $Json -and $Json.PSObject.Properties.Name -contains "requestId") {
        return [string]$Json.requestId
    }
    return ""
}

function Test-PublicEndpoint {
    param(
        [string]$Path,
        [string]$Name
    )

    $r = Invoke-WorkerRequest -Method GET -Path $Path

    if ($r.Error) {
        Add-Result -Name $Name -Status FAIL -Message $r.Error
        return
    }

    if ($r.StatusCode -ne 200) {
        Add-Result -Name $Name -Status FAIL -Message "Expected HTTP 200, got $($r.StatusCode). Body: $($r.Body)" -StatusCode $r.StatusCode -RequestId (Get-RequestId $r.Json)
        return
    }

    Add-Result -Name $Name -Status PASS -Message "HTTP 200 OK" -StatusCode $r.StatusCode -RequestId (Get-RequestId $r.Json)
}

function Test-UnauthorizedPost {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Body,
        [string]$HeaderValue
    )

    $headers = @{}
    if (-not [string]::IsNullOrEmpty($HeaderValue)) {
        $headers["X-PointyPal-Client-Key"] = $HeaderValue
    }

    $r = Invoke-WorkerRequest -Method POST -Path $Path -JsonBody $Body -Headers $headers

    if ($r.Error) {
        Add-Result -Name $Name -Status FAIL -Message $r.Error
        return
    }

    $requestId = Get-RequestId $r.Json

    if ($r.StatusCode -eq 401) {
        Add-Result -Name $Name -Status PASS -Message "401 unauthorized as expected" -StatusCode $r.StatusCode -RequestId $requestId
    } else {
        Add-Result -Name $Name -Status FAIL -Message "Expected 401 unauthorized, got $($r.StatusCode). Body: $($r.Body)" -StatusCode $r.StatusCode -RequestId $requestId
    }
}

function Test-RealChat {
    if ([string]::IsNullOrWhiteSpace($ClientKey)) {
        Add-Result -Name "POST /chat with real key" -Status SKIP -Message "POINTYPAL_CLIENT_KEY is not set. Set `$env:POINTYPAL_CLIENT_KEY or pass -ClientKey."
        return
    }

    $headers = @{
        "X-PointyPal-Client-Key" = $ClientKey
    }

    $body = '{"userText":"Hello from PointyPal auth test. Reply with one short sentence.","interactionMode":"NoPoint"}'
    $r = Invoke-WorkerRequest -Method POST -Path "/chat" -JsonBody $body -Headers $headers
    $requestId = Get-RequestId $r.Json

    if ($r.Error) {
        Add-Result -Name "POST /chat with real key" -Status FAIL -Message $r.Error -RequestId $requestId
        return
    }

    if ($r.StatusCode -eq 401) {
        Add-Result -Name "POST /chat with real key" -Status FAIL -Message "Got 401. WorkerClientKey is missing or does not match POINTYPAL_CLIENT_KEY secret." -StatusCode $r.StatusCode -RequestId $requestId
        return
    }

    if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
        Add-Result -Name "POST /chat with real key" -Status PASS -Message "Authorized and provider-backed chat returned HTTP $($r.StatusCode)" -StatusCode $r.StatusCode -RequestId $requestId
        return
    }

    Add-Result -Name "POST /chat with real key" -Status WARN -Message "Auth likely passed, but endpoint returned HTTP $($r.StatusCode). Body: $($r.Body)" -StatusCode $r.StatusCode -RequestId $requestId
}

function Get-Summary {
    $pass = @($script:Results | Where-Object { $_.Status -eq "PASS" }).Count
    $fail = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
    $skip = @($script:Results | Where-Object { $_.Status -eq "SKIP" }).Count
    $warn = @($script:Results | Where-Object { $_.Status -eq "WARN" }).Count

    return [pscustomobject]@{
        pass  = $pass
        fail  = $fail
        warn  = $warn
        skip  = $skip
        total = $script:Results.Count
    }
}

function Write-Report {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $summary = Get-Summary
    $report = [pscustomobject]@{
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        workerUrl   = $script:BaseUrl
        summary     = $summary
        results     = $script:Results
    }

    $outPath = Resolve-PathOrLiteral -Path $Path
    $json = $report | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($outPath, $json, [System.Text.UTF8Encoding]::new($false))
}

$script:BaseUrl = Normalize-WorkerUrl -Url $WorkerUrl

Write-Host ""
Write-Host "PointyPal Worker verification" -ForegroundColor Cyan
Write-Host "WorkerUrl: $script:BaseUrl"
Write-Host "Real /chat test: $RunRealChat"
Write-Host ""

Test-PublicEndpoint -Path "/health" -Name "GET /health"
Test-PublicEndpoint -Path "/version" -Name "GET /version"

$chatBody = '{"userText":"Hello from PointyPal auth test.","interactionMode":"NoPoint"}'
Test-UnauthorizedPost -Path "/chat" -Name "POST /chat without key" -Body $chatBody -HeaderValue ""
Test-UnauthorizedPost -Path "/chat" -Name "POST /chat with wrong key" -Body $chatBody -HeaderValue "WRONG_KEY"

if ($CheckAllProtectedEndpoints) {
    $ttsBody = '{"text":"PointyPal TTS auth test.","voiceId":"test","modelId":"eleven_flash_v2_5","outputFormat":"mp3_44100_128"}'
    $transcribeBody = '{"audioBase64":"AAAA","audioMimeType":"audio/wav","language":"pl"}'

    Test-UnauthorizedPost -Path "/tts" -Name "POST /tts without key" -Body $ttsBody -HeaderValue ""
    Test-UnauthorizedPost -Path "/tts" -Name "POST /tts with wrong key" -Body $ttsBody -HeaderValue "WRONG_KEY"

    Test-UnauthorizedPost -Path "/transcribe" -Name "POST /transcribe without key" -Body $transcribeBody -HeaderValue ""
    Test-UnauthorizedPost -Path "/transcribe" -Name "POST /transcribe with wrong key" -Body $transcribeBody -HeaderValue "WRONG_KEY"
} else {
    Add-Result -Name "Additional /tts and /transcribe auth checks" -Status SKIP -Message "Pass -CheckAllProtectedEndpoints to run."
}

if ($RunRealChat) {
    Test-RealChat
} else {
    Add-Result -Name "POST /chat with real key" -Status SKIP -Message "Pass -RunRealChat to run a real provider-backed chat test."
}

$summary = Get-Summary

Write-Host ""
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "PASS: $($summary.pass)  FAIL: $($summary.fail)  WARN: $($summary.warn)  SKIP: $($summary.skip)  TOTAL: $($summary.total)"

if ($ReportPath) {
    try {
        Write-Report -Path $ReportPath
        Write-Host "Report written to: $ReportPath" -ForegroundColor Cyan
    } catch {
        Write-Host "Failed to write report: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if ($summary.fail -gt 0) {
    exit 1
}

exit 0
