# Build015 Worker Smoke Tests

These are manual smoke-test examples for a deployed PointyPal Worker. Replace `https://YOUR-WORKER.workers.dev` with your Worker URL. Do not put API keys in these requests.

## 1. GET /health

```powershell
curl.exe https://YOUR-WORKER.workers.dev/health
```

Expected shape:

```json
{
  "ok": true,
  "service": "pointypal-worker"
}
```

## 2. POST /chat text-only

```powershell
$body = @{
    userText = "Say hello and do not point."
    interactionMode = "NoPoint"
    promptProfileInstructions = "Always use [POINT:none]."
} | ConvertTo-Json

curl.exe -X POST https://YOUR-WORKER.workers.dev/chat `
    -H "Content-Type: application/json" `
    --data $body
```

Expected response shape:

```json
{
  "text": "..."
}
```

The returned text should end with exactly one point tag, usually `[POINT:none]` for this request.

## 3. POST /chat with screenshot guidance

Do not paste a giant base64 sample into docs or logs. Put the screenshot JPEG or PNG base64 string in `screenshotBase64`.

```powershell
$body = @{
    userText = "What should I click next?"
    interactionMode = "Point"
    promptProfileInstructions = "Point at the exact center of the intended clickable control."
    screenshotBase64 = "<BASE64_SCREENSHOT_GOES_HERE>"
    screenshotMimeType = "image/jpeg"
    screenshotWidth = 1280
    screenshotHeight = 720
    cursorImagePosition = @{
        x = 640
        y = 360
    }
    monitorBounds = @{
        left = 0
        top = 0
        width = 1920
        height = 1080
    }
} | ConvertTo-Json -Depth 5

curl.exe -X POST https://YOUR-WORKER.workers.dev/chat `
    -H "Content-Type: application/json" `
    --data $body
```

Expected response shape:

```json
{
  "text": "..."
}
```

The returned text should end with `[POINT:x,y:label]` or `[POINT:none]`.

## 4. POST /transcribe expected body shape

Use a small audio clip encoded as base64. The Worker expects `audioBase64`; `language` is optional and defaults to `pl`.

```powershell
$body = @{
    audioBase64 = "<BASE64_AUDIO_GOES_HERE>"
    language = "pl"
} | ConvertTo-Json

curl.exe -X POST https://YOUR-WORKER.workers.dev/transcribe `
    -H "Content-Type: application/json" `
    --data $body
```

Expected response shape:

```json
{
  "text": "...",
  "provider": "assemblyai",
  "durationMs": 1234
}
```

## 5. POST /tts short text

Use a configured ElevenLabs voice ID from your own Worker environment or test configuration. Do not include provider API keys in the request.

```powershell
$body = @{
    text = "Short PointyPal test."
    voiceId = "YOUR_VOICE_ID"
    modelId = "eleven_flash_v2_5"
    outputFormat = "mp3_44100_128"
} | ConvertTo-Json

curl.exe -X POST https://YOUR-WORKER.workers.dev/tts `
    -H "Content-Type: application/json" `
    --data $body
```

Expected response shape:

```json
{
  "audioBase64": "...",
  "audioMimeType": "audio/mpeg",
  "provider": "elevenlabs",
  "durationMs": 0
}
```
