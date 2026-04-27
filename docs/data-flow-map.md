# PointyPal Data Flow Map

This document maps every data flow within the PointyPal application, from capture to external services and local storage.

## 1. Microphone Audio Flow

| Stage | Action | Details |
| :--- | :--- | :--- |
| **Capture** | Microphone input | Captured by `MicrophoneCaptureService` using `NAudio` (16kHz Mono). |
| **Local Save** | Initial storage | Saved to `%AppData%\PointyPal\debug\latest-recording.wav`. |
| **Sent to Worker** | POST `/transcribe` | Read by `WorkerTranscriptProvider`, converted to Base64, and sent to the Cloudflare Worker. |
| **Sent to Provider** | AssemblyAI | Worker uploads audio to AssemblyAI, polls for transcription. |
| **Debug Control** | `SaveRecordings` | If `SaveDebugArtifacts` and `SaveRecordings` are false, GUID-prefixed temp files are used instead of `latest-recording.wav`. |

## 2. Screenshot Flow

| Stage | Action | Details |
| :--- | :--- | :--- |
| **Capture** | Screen grab | Captured by `ScreenCaptureService` via `Graphics.CopyFromScreen` (monitor under cursor). |
| **Local Save** | Initial storage | Saved to `%AppData%\PointyPal\debug\latest-capture.jpg`. |
| **Sent to Worker** | POST `/chat` | Read by `InteractionCoordinator`, converted to Base64, and included in the AI request. |
| **Sent to Provider** | Claude Vision | Worker sends the Base64 image to the Anthropic Messages API. |
| **Debug Control** | `SaveScreenshots` | If `SaveDebugArtifacts` and `SaveScreenshots` are false, image is not saved to disk (processed in memory). |

## 3. UI Automation Context Flow

| Stage | Action | Details |
| :--- | :--- | :--- |
| **Collection** | Win32 & UIA | `UiAutomationContextService` collects foreground window info, element under cursor, and nearby controls. |
| **Local Save** | Debug log | If `SaveUiAutomationDebug` is true, saved to `%AppData%\PointyPal\debug\latest-ui-context.json`. |
| **Sent to Worker** | POST `/chat` | Included as structured JSON in the payload sent to the Cloudflare Worker. |
| **Sent to Provider** | Claude Vision | Included in the system prompt or user message text sent to Anthropic. |

## 4. Transcript Flow

| Stage | Action | Details |
| :--- | :--- | :--- |
| **Source** | AssemblyAI | Returned from the Cloudflare Worker to the app. |
| **Local Save** | Debug log | Saved to `%AppData%\PointyPal\debug\latest-transcript-response.json`. |
| **History** | Persistent log | Added to `InteractionHistoryService` (JSONL format). |
| **Diagnostics** | UI Display | Included in `InteractionDiagnostics` and shown in the Debug Overlay (F9). |

## 5. TTS Flow

| Stage | Action | Details |
| :--- | :--- | :--- |
| **Input** | AI Response | Cleaned text (no tags) from `InteractionCoordinator`. |
| **Sent to Worker** | POST `/tts` | Sent to Cloudflare Worker with Voice/Model settings. |
| **Sent to Provider** | ElevenLabs | Worker calls ElevenLabs API, receives audio bytes. |
| **Local Save** | Audio cache | Saved to `%AppData%\PointyPal\debug\latest-tts.mp3`. |
| **Playback** | Audio out | Played via `AudioPlaybackService` (`NAudio`). |
| **Retention** | `SaveTtsAudio` | If false, GUID-prefixed temp files are used and not cached as "latest". |

## 6. Logs & Diagnostics Flow

| Type | Path | Retention | Redaction |
| :--- | :--- | :--- | :--- |
| **App Logs** | `logs/pointypal.log` | 7 days / Rotate 1MB | Bearer tokens, API keys, Base64 blobs. |
| **Crash Logs** | `logs/crash-*.log` | Persistent until manual delete | Full state dump (redacted via `AppLogService`). |
| **Debug JSON** | `debug/*.json` | 24 hours (if `AutoDelete` on) | Base64 blobs truncated. |
| **History** | `history.jsonl` | 7 days / 200 items | None (contains user text/responses). |
| **Performance** | `debug/performance.json` | Session | None. |
