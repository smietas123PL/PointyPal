# PointyPal User Modes

PointyPal has four runtime modes. Normal Mode is recommended for daily use. A **Guided Setup Wizard** is provided to help configure these modes on first run.

## Normal Mode

Normal Mode is the production mode.

- AI uses the real Worker-backed Claude provider.
- Voice input uses the real Worker-backed STT provider.
- Voice output uses the real Worker-backed TTS provider when TTS is enabled.
- Fake providers are hidden and are not selectable.
- Fake fallback is disabled by default.
- If `WorkerBaseUrl` or `WorkerClientKey` is missing, PointyPal shows a setup error instead of using fake responses.
- If the Worker is unavailable, PointyPal shows a provider error instead of silently simulating a response.

Daily hotkeys:

- Right Ctrl hold/release: voice interaction.
- Ctrl+Space or Ctrl+Shift+Space: Quick Ask.
- Escape: cancel the active operation.
- F9: compact status diagnostics.

## Developer Mode

Developer Mode is for local development, diagnostics, and controlled provider testing.

- Fake, Worker, and Claude provider controls are visible.
- Simulated provider interactions can be used when `AllowFakeProvidersInDeveloperMode` is true.
- Developer hotkeys work only when both `DeveloperModeEnabled` and `EnableDeveloperHotkeys` are true.
- Advanced diagnostics, calibration, replay, self-test, preflight, and release-readiness controls are visible.

The Control Center shows: `Developer Mode - simulated providers may be used.`

Developer hotkeys include F8, F10, F11, F12 variants, Ctrl+F9, Ctrl+Shift+F9, Ctrl+Alt+1/2/3, Alt+F12, Ctrl+Alt+F12, and Ctrl+Shift+F12.

## Safe Mode

Safe Mode is for recovery.

- Real AI/STT/TTS calls are disabled.
- Fake providers are forced for diagnostics and recovery only.
- The Control Center shows: `Safe Mode active. Real AI/STT/TTS calls are disabled. Simulated responses are for diagnostics only.`

Safe Mode can be triggered by recovery flags, crash-loop protection, or the `--safe-mode` command-line argument.

## Self-Test Mode

Self-Test mode is offline-only validation.

- Fake providers are allowed.
- Real Worker calls are not required.
- Release validation can run without secrets or live API calls.

## Existing Fake Provider Configs

PT006 keeps old config values intact for compatibility. If an existing config contains `AiProvider`, `TranscriptProvider`, or `TtsProvider` set to `Fake`, PointyPal does not silently use fake providers in Normal Mode. The effective Normal Mode providers become Worker-backed Claude/STT/TTS, and the Control Center warns: `Fake providers are only available in Developer Mode.`

To intentionally use fake providers, enable Developer Mode or use Safe Mode/Self-Test recovery paths.
