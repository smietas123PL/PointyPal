# PointyPal Private RC Dogfood Checklist

This document provides a step-by-step manual testing guide for the PointyPal Private Release Candidate. Complete these steps before a final GO/NO-GO decision for daily use.

## 1. First Launch
- [ ] Start `PointyPal.exe` from the portable folder.
- [ ] Confirm the **Guided Setup Wizard** opens automatically on first run.
- [ ] Complete the wizard steps (Welcome, Privacy, Worker, Hardware, Hotkeys).
- [ ] Confirm a single tray icon appears after completion.
- [ ] Confirm the Control Center opens via tray menu.
- [ ] Confirm "Normal Mode" is the default mode.
- [ ] Confirm fake providers (Simulation/Fake) are **not visible** in the AI/STT/TTS selection dropdowns.

## 2. Worker Setup
- [ ] If not already done via the wizard:
    - [ ] Open Control Center > Connection.
    - [ ] Set `WorkerBaseUrl` to your deployed Cloudflare Worker URL.
    - [ ] Set `WorkerClientKey` to your secret client key.
    - [ ] Click "Check Worker" and confirm it reports "Reachable".
- [ ] Click "Run Preflight" and confirm core checks pass.
- [ ] Confirm Worker auth is reported as configured.

## 3. Normal Mode Real Flow
- [ ] Hold **Right Ctrl** and speak a command (e.g., "Where is the Start button?").
- [ ] Confirm real STT transcription through Worker.
- [ ] Confirm real Claude response through Worker.
- [ ] Confirm real TTS playback through Worker (if enabled).
- [ ] Confirm the pointer flies to the correct location if the response includes a point.
- [ ] **Critical**: Disconnect network or set an invalid Worker URL. Confirm no fake/simulated response appears in Normal Mode.

## 4. Quick Ask
- [ ] Press **Ctrl+Space** to open the Quick Ask window.
- [ ] Type a text-only question and confirm it works.
- [ ] Use the "Capture" button for a screenshot question and confirm it works.
- [ ] Toggle "NoPoint" mode and confirm the pointer does not fly.
- [ ] Toggle "Point" mode and confirm the pointer flies.

## 5. Privacy
- [ ] In Control Center > Privacy, ensure "Save Debug Artifacts" is **OFF** for production use (or apply "Privacy-Safe Defaults").
- [ ] Confirm screenshots are not saved in `AppData/Local/PointyPal/debug`.
- [ ] Confirm recordings are not saved.
- [ ] Confirm TTS audio is not saved.
- [ ] Confirm logs (`pointypal.log`) do not contain the `WorkerClientKey`.
- [ ] Confirm diagnostics do not show the full key (should be masked or redacted).

## 6. Developer Mode
- [ ] Enable "Developer Mode" in Control Center.
- [ ] Confirm developer hotkeys (F9, F10, etc.) become visible or active.
- [ ] Confirm "Fake" provider controls appear in AI/STT/TTS dropdowns.
- [ ] Run an offline Self-Test.
- [ ] Disable Developer Mode and confirm fake controls disappear.

## 7. Safe Mode
- [ ] Launch PointyPal with `--safe-mode`.
- [ ] Confirm the Safe Mode banner appears in the Control Center.
- [ ] Confirm real providers are disabled (forced to Fake).
- [ ] Confirm simulated responses are clearly marked as diagnostic/recovery only.

## 8. Installer (If testing installer)
- [ ] Run the setup EXE.
- [ ] Confirm the Start Menu shortcut is created.
- [ ] Launch the installed app.
- [ ] Run a quick self-test.
- [ ] Uninstall via Windows Settings.
- [ ] Confirm app files in `Program Files` are removed.
- [ ] Confirm AppData remains (standard behavior) unless manually cleared.

## 9. Resilience
- [ ] Set an invalid Worker URL.
- [ ] Confirm a clear Worker error message in the Control Center or status overlay.
- [ ] Confirm that fake fallback **does not** silently answer in Normal Mode (should report error).
- [ ] Restore valid settings and confirm recovery.

## 10. Release Artifacts Sanity
- [ ] Open the produced portable ZIP.
- [ ] Confirm **no** `config.json` exists.
- [ ] Confirm **no** `.env` exists.
- [ ] Confirm **no** `logs/`, `debug/`, `history/`, or `recordings/` folders exist.
- [ ] Confirm `release-manifest.json` and `checksums.txt` exist.

## 11. Daily Use Smoke
- [ ] Run the app for 30 minutes in the background.
- [ ] Perform at least 5 voice interactions.
- [ ] Perform at least 5 Quick Ask interactions.
- [ ] Lock and unlock Windows.
- [ ] Sleep and resume the machine.
- [ ] Confirm the app remains responsive and the tray icon works.

## 12. GO / NO-GO Decision
- [ ] **GO**: No critical issues found. App feels stable and privacy-safe.
- [ ] **GO with Warnings**: Only minor issues (e.g., unsigned artifacts, known UI glitches).
- [ ] **NO-GO**: Critical issues found (Fake appears in Normal Mode, Worker auth leaks, crashes, privacy failure).
