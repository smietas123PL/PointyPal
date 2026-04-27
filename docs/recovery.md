# PointyPal Recovery Guide

If PointyPal fails to start or behaves unexpectedly, use the following recovery procedures.

## 1. Safe Mode
Safe Mode disables real AI, Speech-to-Text, and Text-to-Speech providers to allow configuration fixes.

### Triggering Safe Mode
- **Manual:** Run `PointyPal.exe --safe-mode`
- **Automatic:** PointyPal enters Safe Mode automatically if it detects 3 consecutive crashes within 10 minutes.

### Exiting Safe Mode
1. Open the **Control Center**.
2. Click **Exit Safe Mode on Next Launch** in the yellow banner.
3. Restart the application.

## 2. Command Line Recovery
Run these commands from a terminal (PowerShell or CMD) in the application directory.

| Command | Description |
|---------|-------------|
| `--reset-safe-mode` | Clears Safe Mode flags and crash-loop counters. |
| `--backup-config` | Creates a manual backup of current settings. |
| `--restore-latest-config` | Restores the most recent settings backup. |
| `--factory-reset-local-state --confirm` | Deletes all local settings, logs, and history. |

## 3. Manual Reset
If the app won't start even in Safe Mode:
1. Delete `%AppData%\PointyPal\config.json`.
2. Delete `%AppData%\PointyPal\state\startup-state.json`.
3. Restart PointyPal.
