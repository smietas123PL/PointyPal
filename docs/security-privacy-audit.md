# PointyPal Security & Privacy Audit

**Audit Date:** 2026-04-27
**Build Baseline:** 021
**Status:** Initial Production Audit

## 1. Asset Inventory
- **User Audio**: Recordings of user voice commands.
- **Screenshots**: Images of the user's primary monitor.
- **UI Context**: Window titles, process names, and UI element tree data.
- **API Keys**: Anthropic, AssemblyAI, ElevenLabs (stored in Cloudflare Worker).
- **Worker Base URL**: Endpoint for the proprietary backend.
- **Interaction History**: Local logs of what the user asked and what the AI responded.

## 2. Trust Boundaries
- **Local App <-> Windows OS**: Boundary for microphone, screen capture, and keyboard hooks (F-keys).
- **Local App <-> Cloudflare Worker**: HTTPS boundary. Trust is established via the `WorkerBaseUrl`.
- **Cloudflare Worker <-> AI Providers**: HTTPS boundary. Secrets are managed as Worker Secrets.

## 3. External Services (Data Outflow)
- **Anthropic (Claude)**: Receives screenshots, user text, and UI context.
- **AssemblyAI**: Receives raw audio recordings for transcription.
- **ElevenLabs**: Receives response text for speech generation.
- **Cloudflare**: Hosts the Worker; receives all application requests.

## 4. Local Storage & Privacy
- **`%AppData%\PointyPal\debug`**: Contains high-sensitivity artifacts (screenshots, audio).
  - *Risk*: Data persists for 24 hours by default.
  - *Mitigation*: `AutoDeleteDebugArtifacts` and short retention hours.
- **`%AppData%\PointyPal\logs`**: Contains application behavior and crash dumps.
  - *Mitigation*: `AppLogService.Redact` masks secrets and large blobs.
- **`%AppData%\PointyPal\history.jsonl`**: Contains interaction text.
  - *Mitigation*: Retention limited to 7 days / 200 items.

## 5. Secret Handling Audit
- **Hardcoded Secrets**: None found in repository via grep (`sk-`, `Bearer`, `key-`).
- **Configuration**: `config.json` contains `WorkerBaseUrl` but no provider API keys.
- **Worker Secrets**: Managed via `wrangler secret put` (not stored in source).
- **Redaction**: `AppLogService` implements regex-based redaction for common secret patterns.

## 6. Risk List & Severity

| Risk | Severity | Mitigation |
| :--- | :--- | :--- |
| **Leak of Screen/Audio** | High | Default to restricted debug artifact saving; auto-cleanup; PT002: Privacy-first defaults (false). |
| **Credential Theft (Worker)** | High | Use Cloudflare Environment Secrets; no local storage of provider keys. |
| **Unauthorized Worker Usage** | High | PT002: Implemented `X-PointyPal-Client-Key` authentication. |
| **Permissive CORS** | Medium | PT002: Restricted CORS via `ALLOWED_ORIGINS` environment variable. |
| **UI Context Exposure** | Medium | Redact window titles if possible; allow users to disable UIA collection. |
| **Man-in-the-Middle** | Medium | Enforce HTTPS for Worker communication; no fallback to HTTP. |
| **Excessive History Retention** | Low | Implement strict TTL and item count limits for local history; PT002: Defaulted to OFF. |

## 7. Audit Findings (PT002 Update)
- **Worker Auth**: Private client key authentication now enforced for all proxy endpoints.
- **Privacy Defaults**: All local artifact saving (Screenshots, Recordings, TTS, Interaction History, UI Debug) now defaults to `false`.
- **Input Validation**: Worker now enforces strict size and type limits on all inputs.
- **Request IDs**: Added for tracking and diagnostic correlation.
- **Redaction**: Added `X-PointyPal-Client-Key` to local log redaction patterns.

## 8. Production Recommendations (Status: IMPLEMENTED)
1. **Restrict Debug Saving**: [PT002] Disabled `SaveDebugArtifacts` and all individual save flags by default.
2. **Origin Lockdown**: [PT002] Restricted Cloudflare Worker CORS via `ALLOWED_ORIGINS`.
3. **Worker Hardening**: [PT002] Added authentication, input validation, and model allowlisting.
4. **User Opt-In**: [PT002] Onboarding and Privacy settings ensure users must explicitly enable history/artifact saving.

## 9. PT006 User Mode Update
- Normal Mode hides fake providers and developer controls.
- Normal Mode requires real Worker-backed providers and reports missing `WorkerBaseUrl` or `WorkerClientKey` as setup errors.
- Fake provider fallback is disabled by default so Worker failures are visible instead of silently simulated.
- Developer Mode exposes simulated providers, test hotkeys, calibration, and advanced diagnostics.
- Developer Mode may expose more local diagnostics and debug artifacts; it should be used only for testing or troubleshooting.
- Safe Mode and Self-Test mode may use fake providers for recovery and offline validation only.
