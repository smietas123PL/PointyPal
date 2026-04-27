# Local Release Procedure

This document describes the PT004 private RC packaging flow for PointyPal.

## Prerequisites

- .NET 8 SDK
- .NET 8 Desktop Runtime for framework-dependent packages
- PowerShell
- Cloudflare Wrangler for Worker deployment
- Optional for PT004 installer prototype: Inno Setup 6 with `ISCC.exe` installed or available on `PATH`
- Optional for PT005 signing tests: Windows 10/11 SDK with `signtool.exe`

## Worker Deployment

1. Go to `worker`.
2. Set Worker secrets with Wrangler.
3. Deploy the Worker.
4. Run the **Setup Wizard** in PointyPal to configure your **Worker Connection** (`WorkerBaseUrl` and `WorkerClientKey`).
5. Alternatively, configure manually in PointyPal **Control Center**.
6. Keep **Normal Mode** enabled for daily validation. Normal Mode uses real Worker-backed AI, Voice Input, and Voice Output providers and does not fall back to simulated responses.

Simulated providers are available only in Developer Mode, Safe Mode, and offline self-tests. Use `docs/user-modes.md` and `docs/setup-wizard.md` when validating mode-specific behavior.

## Build And Release Check

Run the full local release validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1
```

This runs:

- `scripts/build.ps1`
- `scripts/publish-portable.ps1`
- `scripts/create-release-manifest.ps1`
- `scripts/validate-portable.ps1`
- `scripts/package-portable-zip.ps1`

Release checks and self-tests are offline-safe where designed and must not require real API calls unless a Worker health/preflight check is explicitly selected.

Portable mode is the default and does not require Inno Setup.

## Portable Publish

Default framework-dependent publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1
```

Self-contained publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1 -SelfContained
```

Portable output is written to:

`artifacts\PointyPal-portable`

## Manifest And Checksums

The publish flow generates:

- `release-manifest.json`
- `checksums.txt`

The manifest records app name, version, release label, build channel, build date, runtime target, self-contained mode, docs inclusion, files, and SHA256 hashes.
It also records signing metadata for `PointyPal.exe` and the setup EXE when present. Unsigned private RC artifacts are reported as unsigned but are still valid private RC outputs.

Validate hashes and package contents with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-portable.ps1
```

## ZIP Packaging

Create the private RC ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable-zip.ps1 -UseExistingArtifacts
```

Default ZIP:

`artifacts\PointyPal-v0.21.0-private-rc.1-win-x64-portable.zip`

The ZIP includes app files, docs, `config.example.json`, `NOTICE.txt`, `README-FIRST-RUN.md`, `release-manifest.json`, and `checksums.txt`. It excludes local config, logs, debug files, history/usage data, `.env`, secret/key files, `.git`, and `bin`/`obj` leftovers.

## Optional Installer Prototype

Build the private RC Inno Setup installer after the portable artifact exists:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Output:

`artifacts\installer\PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe`

The build script locates `ISCC.exe` in common Inno Setup install paths or on `PATH`. If Inno Setup is missing, the script exits non-zero with installation instructions; portable release validation is unaffected.

Validate the installer artifact without running it:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-installer.ps1
```

To include installer build and validation in the release check, opt in explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -IncludeInstaller
```

The installer manifest and hash are written to `artifacts\installer\installer-manifest.json` and `artifacts\installer\installer-checksums.txt`. The installer remains unsigned unless the optional PT005 signing flow is used.

## Optional Signing

Signing is optional for private RC builds. Unsigned Windows warnings are expected for the portable EXE and setup EXE.

Report signature state without failing:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1
```

Run release-check with signature reporting:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -VerifySignatures
```

Sign only when a certificate is configured:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -Sign -VerifySignatures
```

Certificate inputs can come from `SIGN_CERT_PATH` plus `SIGN_CERT_PASSWORD`, or from `SIGN_CERT_THUMBPRINT`. Do not commit PFX files, private keys, exported certificates, or certificate passwords.

## Install And Signing References

- Install/uninstall: `docs/install-uninstall.md`
- Installer smoke test: `docs/installer-smoke-test.md`
- Code signing: `docs/code-signing-plan.md`
- Signing runbook: `docs/signing-runbook.md`
- Installer strategy: `docs/installer-strategy.md`

## Final RC Validation (PT007)

Before dogfooding a private RC, run the final validation script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\final-rc-validation.ps1
```

This script:
1. Runs `dotnet test` on the solution.
2. Runs `scripts/build.ps1`.
3. Runs `scripts/release-check.ps1` for portable artifacts.
4. Optionally runs `release-check.ps1` with `-IncludeInstaller -VerifySignatures`.
5. Verifies all required artifacts and documentation exist.
6. Produces a structured JSON report at `artifacts/final-rc-validation-report.json`.

Interpret the `GoNoGoRecommendation` as follows:
- **GO**: All automated checks passed. Ready for manual dogfooding.
- **GO (with warnings)**: Minor issues like unsigned artifacts or missing Inno Setup (if optional) were detected, but core functionality is validated.
- **NO-GO**: Critical failures (test failures, missing core artifacts). Do not dogfood this build.

After a successful validation script run, complete the manual steps in `docs/private-rc-dogfood-checklist.md`.

Refer to `docs/private-rc-known-warnings.md` for information on unsigned binaries, SmartScreen warnings, and Worker configuration requirements.
