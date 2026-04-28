# Release Candidate (RC) Readiness Checklist

Follow these steps to validate a build for RC readiness.

## 1. Automated Health Checks
- [ ] Run **Full RC Validation** from Release tab (sequences Self-Test, Preflight, and Readiness).
- [ ] Run the **Setup Wizard** and verify all tests pass.
- [ ] Run **Preflight Check** (Verify all checks are Green).
- [ ] Run **RC Readiness Check** (Verify score > 90).

## 2. Stability & Performance
- [ ] Run **Soak Test** for at least 10 minutes.
    - [ ] Check memory delta (should be near 0 MB).
    - [ ] Check max handles/threads (should be stable).
- [ ] Verify **P95 Latency** is within acceptable limits (< 2000ms for full loop).

## 2.5. Pointer Accuracy & Calibration (PT012)
- [ ] Run [Pointer Calibration Guide](pointer-calibration.md) steps.
- [ ] Verify accuracy on primary and secondary monitors.
- [ ] Run [Pointer QA Checklist](pointer-qa-checklist.md).
- [ ] Export and verify [Pointer QA Report](pointer-calibration.md#pointer-quality-score).
- [ ] Confirm snapping thresholds are stable.

## 3. Recovery Validation
- [ ] Force a crash loop (kill process 3 times during startup).
    - [ ] Verify app starts in Safe Mode.
    - [ ] Verify yellow banner appears in Control Center.
- [ ] Test `--reset-safe-mode` command.
- [ ] Test `--backup-config` and `--restore-latest-config`.

## 4. UI & UX
- [ ] Verify Tray Menu items work.
- [ ] Verify Normal Mode tray menu includes **Setup Wizard** and **Getting Started / Tutorials**.
- [ ] Verify Normal Mode tray menu is limited to Quick Ask, Control Center, Setup Wizard, Toggle Settings, Status, Help, and Exit.
- [ ] Verify F9 shows compact status diagnostics in Normal Mode.
- [ ] Enable Developer Mode and `EnableDeveloperHotkeys`, then verify full diagnostics and F8/F10/F11/F12 developer hotkeys.
- [ ] Verify simulated provider controls are hidden in Normal Mode and visible in Developer Mode.
- [ ] Verify an existing config with `Fake` providers shows `Fake/simulated providers are only available in Developer Mode.` and does not silently use simulated providers in Normal Mode.
- [ ] Verify Quick Ask window opens and functions.
- [ ] Verify missing `WorkerClientKey` shows a setup error in Normal Mode.

## 5. Artifact Review
- [ ] Review `pointypal.log` for unexpected errors.
- [ ] Verify `rc-readiness-report.json` is generated in `debug` folder.
- [ ] Run `powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1`.
- [ ] Verify portable ZIP is generated in `artifacts\`.
- [ ] Verify `release-manifest.json` exists in `artifacts\PointyPal-portable`.
- [ ] Verify `checksums.txt` includes `PointyPal.exe`.
- [ ] Run `powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1` and record whether artifacts are signed or unsigned.
- [ ] Verify `validate-portable.ps1` passes.
- [ ] Verify ZIP excludes `config.json`, logs, debug files, history/usage data, `.env` files, and secret/key files.
- [ ] Verify unsigned app warning is expected for private RC.
- [ ] For a signed RC, run `powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1 -RequireSigned`.
- [ ] Confirm no `.pfx`, `.p12`, `.cer`, `.pvk`, `.spc`, `.key`, `.pem`, `certs\`, `signing\`, or `secrets\` material is included or committed.
- [ ] Verify Start with Windows can be enabled and disabled.
- [ ] Verify uninstall cleanup: extracted folder can be removed and `%AppData%\PointyPal` can be preserved or deleted intentionally.

## 6. Optional Installer Prototype
- [ ] If Inno Setup is installed, run `powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -IncludeInstaller`.
- [ ] If Inno Setup is not installed, run `powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1` and confirm it fails gracefully with setup instructions.
- [ ] Verify `artifacts\installer\PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe` is generated when Inno Setup is available.
- [ ] Verify `artifacts\installer\installer-manifest.json` records filename, size, SHA256, generated time, signed, signature status, and signer.
- [ ] Run `powershell -ExecutionPolicy Bypass -File .\scripts\validate-installer.ps1`.
- [ ] For a signed installer RC, run `powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -IncludeInstaller -Sign -VerifySignatures -RequireSigned`.
- [ ] Complete `docs/installer-smoke-test.md` on a Windows test machine.
## 7. Final Private RC Validation (PT007)
- [ ] Run `powershell -ExecutionPolicy Bypass -File .\scripts\final-rc-validation.ps1`.
    - [ ] Verify `artifacts/final-rc-validation-report.json` is generated.
    - [ ] Verify `GoNoGoRecommendation` is `GO` or `GO (with warnings)`.
- [ ] Complete all steps in `docs/private-rc-dogfood-checklist.md`.
- [ ] Verify **Normal Mode isolation**: Ensure no simulated provider or simulation behavior is visible or active when in Normal Mode.
- [ ] Verify **Worker Auth**: Ensure `WorkerClientKey` is required and not leaked in logs/reports.
- [ ] Verify **Privacy-Safe Defaults**: Ensure no sensitive data (screenshots, recordings) is saved when privacy defaults are active.
- [ ] Verify **Installer Smoke Test**: Run the full install/test/uninstall cycle if building the installer.
- [ ] Verify **Signing Status**: Confirm if artifacts are signed or unsigned and that this matches expectations for the release label.
- [ ] Review `docs/private-rc-known-warnings.md` and ensure all warnings are still accurate and acceptable.
- [ ] Make the final **GO / NO-GO** decision for private manual dogfooding.
