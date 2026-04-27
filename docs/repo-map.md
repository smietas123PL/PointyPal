# PointyPal Repository Map

This document provides a concise overview of the project structure and the purpose of each major component.

## Folder Overview

### [Core/](file:///c:/Users/Greg/Downloads/PointyPal/Core)
Contains the heart of the application, including the interaction state machine (`InteractionCoordinator`), core interaction modes, and shared types.

### [Overlay/](file:///c:/Users/Greg/Downloads/PointyPal/Overlay)
Manages the WPF transparent overlay system. Includes the `AvatarOverlayWindow`, flight animation engine, and coordinate mapping logic.

### [Input/](file:///c:/Users/Greg/Downloads/PointyPal/Input)
Handles low-level input hooks, including the keyboard hook for hotkeys (F-keys) and the mouse hook for cursor tracking.

### [Voice/](file:///c:/Users/Greg/Downloads/PointyPal/Voice)
Contains the voice interaction pipeline:
- `PushToTalkService`: Handles microphone capture via NAudio.
- `TranscriptionService`: Interface for STT (Speech-to-Text).
- `VoicePlaybackService`: Manages TTS (Text-to-Speech) playback.

### [AI/](file:///c:/Users/Greg/Downloads/PointyPal/AI)
Implementations of AI service providers.
- `ClaudeAiProvider`: Integration with the Cloudflare Worker adapter for Claude Vision.
- `FakeAiProvider`: Simulation provider for offline testing and development.

### [Capture/](file:///c:/Users/Greg/Downloads/PointyPal/Capture)
Handles screen and UI capture.
- `ScreenshotService`: Captures screen regions.
- `UiAutomationService`: Extracts UI tree context for AI enrichment.

### [Infrastructure/](file:///c:/Users/Greg/Downloads/PointyPal/Infrastructure)
Shared plumbing and cross-cutting concerns:
- `ConfigService`: Management of `config.json`.
- `AppLogService`: Structured logging system.
- `RcValidationService`: Health checks and release candidate readiness.

### [UI/](file:///c:/Users/Greg/Downloads/PointyPal/UI)
WPF Windows and Controls:
- `ControlCenterWindow`: The main configuration and dashboard interface.
- `QuickAskWindow`: A minimal UI for text-based interaction.
- `TextBubble`: The avatar's speech bubble component.

### [Tray/](file:///c:/Users/Greg/Downloads/PointyPal/Tray)
Manages the system tray icon, context menu, and application lifecycle from the tray.

### [tests/](file:///c:/Users/Greg/Downloads/PointyPal/tests)
The test suite, primarily located in `PointyPal.Tests`. Includes unit tests, resilience tests, and the interaction simulation harness.

### [worker/](file:///c:/Users/Greg/Downloads/PointyPal/worker)
Source code for the Cloudflare Worker that acts as a secure proxy for AI (Anthropic), STT (AssemblyAI), and TTS (ElevenLabs) APIs.

### [scripts/](file:///c:/Users/Greg/Downloads/PointyPal/scripts)
Utility scripts for building, testing, publishing, and validating the application.

### [docs/](file:///c:/Users/Greg/Downloads/PointyPal/docs)
Project documentation, including build logs, verification reports, and architectural notes.
