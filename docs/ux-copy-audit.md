# UX Copy Audit - PointyPal

This document tracks user-facing text improvements for PointyPal.

## Standardized Terminology

| Term | Recommended | Avoid in Normal Mode |
|------|-------------|----------------------|
| Setup Wizard | Setup Wizard | Onboarding |
| Control Center | Control Center | Settings / Dashboard |
| Normal Mode | Normal Mode | - |
| Developer Mode | Developer Mode | - |
| Safe Mode | Safe Mode | - |
| Worker Connection | Worker Connection | Cloudflare / Backend |
| Voice Input | Voice Input | STT / Recording |
| Voice Output | Voice Output | TTS / Speech |
| Screen Context | Screen Context | Screenshots / UIA |
| Pointer | Pointer | Red dot / Cursor |
| Privacy-Safe Defaults | Privacy-Safe Defaults | Security settings |
| Offline Self-Test | Offline Self-Test | Regression test |
| Release Validation | Release Validation | RC check |

## Part 1: Audit Findings

### Setup Wizard

| Location | Old Text | New Text | Reason |
|----------|----------|----------|--------|
| Welcome Header | Welcome to PointyPal | Welcome to PointyPal | Keep |
| Welcome Sub | PointyPal is your AI assistant that sees your screen and helps you interact with apps. | PointyPal is an AI assistant that sees your screen, listens to your voice, and points the way. | More concise and active. |
| Welcome Bullet 1 | Sees screen context to understand what you're doing. | Understands your screen context. | Simpler. |
| Welcome Bullet 2 | Listens to your requests via push-to-talk (Right Ctrl). | Listens to your voice (Right Ctrl). | Avoid jargon "push-to-talk". |
| Welcome Bullet 3 | Points at UI elements and answers via text or voice. | Points at apps and answers your questions. | More natural. |
| Worker Connection Sub | PointyPal needs a Cloudflare Worker to process AI requests. | PointyPal needs a Worker to connect to AI services. | "Cloudflare" is slightly technical. |
| Worker Connection Help | (None) | [How do I get a Worker?](https://github.com/example/pointypal#setup) | Add actionable help link. |
| Voice Output Sub | PointyPal can speak its responses using ElevenLabs. | PointyPal can speak responses using a digital voice (optional). | Emphasize it's optional. |
| Complete Bullet 5 | Developer Mode Disabled | - Developer Mode (Advanced) | Clarify what it is. |

### Control Center

| Location | Old Text | New Text | Reason |
|----------|----------|----------|--------|
| Header Banner | Configure Worker URL and WorkerClientKey... | Setup Required: Configure your Worker connection to enable AI features. | More professional. |
| Status Tab - Worker Auth | Configured | Ready / Not Set | Consistency. |
| Basic Tab - Group Header | Daily Settings | General Settings | Standardized. |
| Voice Tab - Providers | Fake / Worker | (Hidden in Normal Mode) | Do not expose "Fake" to regular users. |

### Tray Menu

| Location | Old Text | New Text | Reason |
|----------|----------|----------|--------|
| Menu Item | Status / Health | Status | Simpler. |
| Menu Item | Exit | Quit PointyPal | Clearer. |

## Part 2: Error Messages

| Error | Old Message | New Message |
|-------|-------------|-------------|
| Missing Worker URL | Worker request failed. | PointyPal could not reach your Worker. Check your Worker URL in Control Center -> Connection. | Actionable. |
| Worker Unreachable | Worker unreachable | Could not connect to Worker. Please check your internet connection. | User-friendly. |
| Unauthorized | unauthorized Worker request | Worker authentication failed. Please check your Client Key in Control Center -> Connection. | Actionable. |
| Recording too short | recording too short | Voice command was too short. Please hold the key a bit longer while speaking. | Instructive. |

## Remaining Text Debt

- [ ] Standardize all "Claude" mentions to "AI" in Normal Mode where possible.
- [ ] Ensure "Fake" is 100% hidden in Normal Mode across all tabs.
- [ ] Review logs/diagnostics for any remaining developer jargon that might leak to Normal Mode users.
