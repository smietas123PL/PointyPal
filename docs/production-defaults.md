# Production Default Recommendations

This document outlines the recommended configuration defaults for the PointyPal production release to ensure user privacy and security while maintaining core functionality.

## Privacy & Debug Defaults

| Setting | Current (Dev) | Recommended (Prod) | Rationale |
| :--- | :--- | :--- | :--- |
| `SaveDebugArtifacts` | `false` | `false` | Prevents local accumulation of sensitive audio/image files. |
| `SaveScreenshots` | `false` | `false` | Minimizes storage of monitor captures. |
| `SaveRecordings` | `false` | `false` | Minimizes storage of voice captures. |
| `SaveTtsAudio` | `false` | `false` | Minimizes storage of generated speech. |
| `SaveInteractionHistory` | `false` | `false` | PT002: Now disabled by default for privacy. |
| `SaveUiAutomationDebug` | `false` | `false` | PT002: Now disabled by default. |
| `RedactDebugPayloads` | `true` | `true` | Mandatory protection for logs. |

## Feature Defaults

| Setting | Current (Dev) | Recommended (Prod) | Rationale |
| :--- | :--- | :--- | :--- |
| `DeveloperModeEnabled` | `false` | `false` | Normal Mode is recommended for daily use. |
| `EnableDeveloperHotkeys` | `false` | `false` | Test/fake hotkeys must not run accidentally in production. |
| `ShowDeveloperTrayItems` | `false` | `false` | Keep the tray menu focused on daily actions. |
| `ShowAdvancedDiagnostics` | `false` | `false` | Normal Mode shows compact status diagnostics only. |
| `AiProvider` | `Claude` | `Claude` | Normal Mode uses the real Worker-backed Claude provider. |
| `TranscriptProvider` | `Worker` | `Worker` | Normal Mode uses the real Worker-backed STT provider. |
| `TtsProvider` | `Worker` | `Worker` | TTS uses the Worker when voice output is enabled. |
| `EnableProviderFallback` | `false` | `false` | Worker failures should surface clearly instead of silently simulating a response. |
| `FallbackToFakeOnWorkerFailure` | `false` | `false` | Fake fallback is reserved for explicit developer testing. |
| `AllowFakeProviderFallbackInNormalMode` | `false` | `false` | Fake provider fallback remains blocked in Normal Mode. |
| `TtsEnabled` | `false` | `false` | Voice output should be an opt-in user choice. |
| `VoiceInputEnabled` | `true` | `true` | Core functionality for hands-free use. |
| `ScreenshotEnabled` | `true` | `true` | Core functionality for vision-based assistance. |
| `UiAutomationEnabled` | `true` | `true` | Core functionality for high-accuracy pointing. |
| `StartWithWindows` | `false` | `false` | Application should not autostart without user consent. |

## Logging & Diagnostics Defaults

| Setting | Current (Dev) | Recommended (Prod) | Rationale |
| :--- | :--- | :--- | :--- |
| `AppLoggingEnabled` | `true` | `true` | Critical for remote troubleshooting. |
| `CrashLoggingEnabled` | `true` | `true` | Critical for fixing production stability issues. |
| `DiagnosticsBundleIncludeLogs` | N/A | `true` | Proposed: Include text logs in diagnostics. |
| `DiagnosticsBundleIncludeScreenshots` | N/A | `false` | Proposed: Exclude images from auto-generated diagnostics. |
| `DiagnosticsBundleIncludeAudio` | N/A | `false` | Proposed: Exclude audio from auto-generated diagnostics. |

## Recommendation Summary
For the Production Track 006 release, use Normal Mode for daily operation. Fake providers remain in the codebase for Developer Mode, Safe Mode, and offline self-tests, but they are hidden from Normal Mode and are not used as silent fallback.

Privacy-first local storage remains the default: debugging artifacts, screenshots, recordings, TTS audio, and interaction history are disabled unless a user explicitly enables them. Developer Mode may expose more diagnostics and local debug artifacts, so it should be enabled only when actively testing or troubleshooting.
