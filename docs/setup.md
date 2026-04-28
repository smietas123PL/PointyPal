# PointyPal Setup & Onboarding

PointyPal is designed to be production-ready and user-friendly. This guide covers the initial setup and the simplified Control Center navigation.

## 1. Guided Setup Wizard (7 Steps)

On first launch, PointyPal opens a 7-step wizard to ensure your environment is correctly configured:

1.  **Welcome**: Introduction to PointyPal's core capabilities.
2.  **Voice Check**: Interactive microphone detection with a level meter.
3.  **Worker Link**: Configuration of your `WorkerBaseUrl` and `WorkerClientKey`.
4.  **Provider Setup**: Simple toggles for Voice Input and Voice Output (TTS).
5.  **Privacy Control**: One-click application of privacy-safe defaults (no local saving of screenshots/audio).
6.  **Hotkey Mastery**: Visual guide to the primary interaction keys.
7.  **Finish & Test**: An end-to-end verification test before you start.

## 2. Control Center Navigation

The Control Center has been simplified into a sidebar-driven interface with clear categories:

*   **Home (Dashboard)**: At-a-glance status of PointyPal (Ready, Offline, or Setup Required).
*   **Setup**: Quick access to re-run the Setup Wizard or reset settings.
*   **Usage**: Daily statistics on interactions and AI usage.
*   **Privacy**: Centralized control over what data stays on your computer.
*   **Help (Tutorials)**: Interactive cards to practice commands and learn features.
*   **Advanced**: Hidden by default. Contains technical health checks, performance benchmarks, and logs. (Visible in Developer or Safe Mode).

## 3. Mode Isolation

*   **Normal Mode**: Uses real Worker-backed AI providers. Technical tabs are hidden for a clean experience.
*   **Developer Mode**: Enables advanced diagnostics, simulation tools, and full logging.
*   **Safe Mode**: Activated after multiple crashes. Forces simulation mode to allow recovery without further failures.

## 4. Troubleshooting Connection

If the Home dashboard shows "Connection Problem":
1.  Go to the **Setup** tab and verify your Worker URL.
2.  Ensure your **Worker Client Key** is entered correctly in the Advanced > Worker settings.
3.  Run a **Preflight Check** from the Advanced > Health tab to identify the specific failure point.
