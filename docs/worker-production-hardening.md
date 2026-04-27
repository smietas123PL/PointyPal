# Worker Production Hardening

This document outlines the security and privacy hardening measures implemented in the PointyPal Cloudflare Worker (Production Track 002).

## Worker Authentication

The Worker now requires a private client key for protected endpoints (`/chat`, `/transcribe`, `/tts`).

### Configuration
1.  Add a secret named `POINTYPAL_CLIENT_KEY` to your Cloudflare Worker using Wrangler or the Cloudflare Dashboard.
2.  In the PointyPal Windows app, enter this same key in **Control Center > General > Worker Client Key**.

### Request Header
All protected requests must include:
`X-PointyPal-Client-Key: <your-secret-key>`

## CORS Hardening

Permissive CORS (`*`) is now restricted if `ALLOWED_ORIGINS` is configured.

### Configuration
- Set `ALLOWED_ORIGINS` as a comma-separated list of origins (e.g., `https://app.pointypal.com,https://dev.local`).
- If `ALLOWED_ORIGINS` is unset, the Worker allows any origin (dev mode).
- Requests without an `Origin` header (e.g., from the PointyPal desktop app) are always allowed.

## Model Allowlist

The Worker restricts which AI models can be requested to prevent unauthorized usage of expensive or unsupported models.

### Allowed Models
- `claude-3-5-sonnet-20240620`
- `claude-3-5-sonnet-latest`
- `claude-3-sonnet-20240229`
- `claude-3-haiku-20240307`
- `claude-sonnet-4-5`
- `claude-sonnet-4-6`

### Default Model
If no model is specified in the request, the Worker uses `DEFAULT_CLAUDE_MODEL` from the environment, or falls back to a safe internal default.

## Input Validation

Strict validation is applied to all incoming requests:
- **Chat**: Max text length (4000), max screenshot size (10MB), allowed image types (JPEG/PNG), valid dimensions (max 4096).
- **Transcribe**: Required audio data, max size (20MB), allowed languages (pl, en, auto).
- **TTS**: Max text length (700 or `MAX_TTS_CHARS`), allowed models (`eleven_flash_v2_5`, `eleven_turbo_v2_5`), fixed output format (`mp3_44100_128`).

## Observability

- **Request IDs**: Every response (success or error) includes a unique `requestId` (UUID).
- **Structured Errors**: Errors follow a standard JSON format:
    ```json
    {
      "error": "error_code",
      "message": "Human readable message",
      "status": 400,
      "requestId": "..."
    }
    ```

## Logging Redaction

Worker logs are metadata-only. The following are never logged:
- API Keys
- `POINTYPAL_CLIENT_KEY`
- Base64 payloads (screenshots, audio)
- Full user transcripts or TTS text

## Rate Limiting (Guardrails)

The following environment variables can be set to define daily limits (optional, currently documented as TODO for full KV-backed implementation):
- `MAX_CHAT_REQUESTS_PER_DAY`
- `MAX_TRANSCRIBE_REQUESTS_PER_DAY`
- `MAX_TTS_REQUESTS_PER_DAY`

> [!NOTE]
> For robust production usage, Cloudflare KV-backed rate limiting is recommended.
