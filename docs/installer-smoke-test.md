# Installer Smoke Test

Use this checklist only for the optional Inno Setup private RC installer. The portable ZIP remains the primary release artifact.

1. Run `artifacts\installer\PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe`.
2. Confirm the install path is `C:\Program Files\PointyPal` or the equivalent `{autopf}\PointyPal` location on the test machine.
3. Confirm the Start Menu shortcut is created.
4. Select the Desktop shortcut option during setup and confirm the shortcut is created.
5. Launch PointyPal from the setup completion checkbox or shortcut.
6. Open Control Center from the tray icon.
7. Run Self-Test.
8. Configure Worker URL and client key.
9. Confirm config is written under `%AppData%\PointyPal\config.json`.
10. Enable and disable Start with Windows from Control Center.
11. Uninstall PointyPal from Windows Apps.
12. Confirm program files are removed from the install directory.
13. Confirm `%AppData%\PointyPal` remains unless manually removed.
14. Confirm the portable ZIP still extracts and runs independently.
