# PointyPal Private RC - Known Warnings

This document lists known warnings and limitations for the current Private Release Candidate.

## Security & Trust
- **Unsigned Binaries**: Artifacts are currently unsigned. Windows SmartScreen or antivirus software may trigger warnings. You must manually allow the application to run.
- **No Public ToS/Privacy Policy**: This is a private RC. A formal ToS and Privacy Policy for public use are not yet included.

## System Requirements
- **.NET 8 Runtime**: The framework-dependent package requires the **.NET 8 Desktop Runtime** to be installed on the host system.
- **Microphone Access**: The app requires microphone permissions to capture voice input.

## Configuration
- **Worker Setup**: Real interactions require a deployed Cloudflare Worker. You must configure the `WorkerBaseUrl` and `WorkerClientKey` in the Control Center.
- **Secrets Management**: Worker secrets (Anthropic Key, ElevenLabs Key) must be configured in the Cloudflare Dashboard/Wrangler secrets, not in the app's local config.

## Modes & Behavior
- **Normal Mode**: Exclusively uses real Worker-backed providers. Fake fallback is disabled by default to ensure cost and privacy safety.
- **Developer Mode**: Exposes simulated/test providers for offline debugging. **Do not use for production tasks.**
- **Safe Mode**: A recovery-only mode that disables all real network calls.

## Distribution
- **No Auto-Update**: This version does not include an automatic updater. Subsequent versions must be downloaded and installed manually.
- **No Marketplace**: This version is for private dogfooding only and is not distributed through any app store or marketplace.

## Known Technical Issues
- **Inno Setup Warning**: Some architectures may trigger a deprecation warning during installer build; this is known and does not affect the final package.
- **UI Scaling**: On extremely high DPI settings, some tray menu elements or overlays may require manual calibration in the Control Center.
