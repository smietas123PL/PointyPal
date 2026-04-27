# Install and Uninstall

## Portable Install

1. Extract the portable ZIP to a local folder such as `%LocalAppData%\Programs\PointyPal` or another user-owned folder.
2. Run `PointyPal.exe`.
3. Open Control Center from the tray icon.
4. Configure `WorkerBaseUrl` and `WorkerClientKey`.
5. Run Self-Test and Preflight.

Framework-dependent packages require the .NET 8 Desktop Runtime. If the app does not start, install or repair the Microsoft .NET 8 Desktop Runtime.

## Optional Inno Installer Install

The PT004 Inno Setup installer is a private RC prototype. The portable ZIP remains the primary artifact.

1. Run `artifacts\installer\PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe`.
2. Install to the default `{autopf}\PointyPal` path unless a test case requires another location.
3. Confirm the Start Menu shortcut is created.
4. Select the optional Desktop shortcut only if desired.
5. Use the optional launch checkbox only if you want to start PointyPal immediately after setup.
6. Open Control Center from the tray icon.
7. Configure `WorkerBaseUrl` and `WorkerClientKey`.
8. Run Self-Test and Preflight.

The installer includes app files, docs, `NOTICE.txt`, `README-FIRST-RUN.md`, and `config.example.json`. It must not include local `config.json`, logs, debug artifacts, history/usage data, `.env`, PFX files, private keys, exported certificates, or secret/key files.

Private RC installers may be unsigned. Windows warnings are expected unless a trusted signing certificate is used, and SmartScreen can still warn for new signed builds until reputation is established.

## Local Data

- Config: `%AppData%\PointyPal\config.json`
- Backups: `%AppData%\PointyPal\backups`
- Logs: `%AppData%\PointyPal\logs`
- Debug artifacts: `%AppData%\PointyPal\debug`
- History and usage data: `%AppData%\PointyPal\history`, `%AppData%\PointyPal\usage`, and related local folders

## Start With Windows

PointyPal registers Start with Windows only when enabled by the user in Control Center. Registration uses:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

Value name:

`PointyPal`

To disable it, uncheck Start with Windows in Control Center and save settings. You can also remove the `PointyPal` value from the HKCU Run key.

## Uninstall

### Portable Uninstall

1. Exit PointyPal from the tray icon.
2. Disable Start with Windows if enabled.
3. Delete the extracted app folder.
4. Optionally delete `%AppData%\PointyPal` to remove local config, logs, backups, history, usage data, and debug data.

### Inno Installer Uninstall

1. Exit PointyPal from the tray icon.
2. Disable Start with Windows if enabled.
3. Open Windows Apps, find PointyPal, and choose Uninstall.
4. Confirm the install directory under `{autopf}\PointyPal` is removed.
5. Confirm `%AppData%\PointyPal` remains unless manually removed.

The installer does not add an updater and does not require a signing certificate for private RC validation. For a signed release candidate, sign `PointyPal.exe` and the setup EXE, then run `scripts\verify-signatures.ps1 -RequireSigned`.

## Preserve, Backup, And Restore Config

To preserve settings during an update, keep `%AppData%\PointyPal\config.json` and replace only the extracted app files.

To back up manually:

1. Exit PointyPal.
2. Copy `%AppData%\PointyPal\config.json` to a safe location.

To restore:

1. Exit PointyPal.
2. Copy the saved `config.json` back to `%AppData%\PointyPal\config.json`.
3. Start PointyPal.

PointyPal also supports local config backup and restore commands documented in `docs/recovery.md`.
