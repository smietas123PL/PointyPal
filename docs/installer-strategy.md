# Installer Strategy

PointyPal PT004 keeps private RC distribution portable-first while adding an optional Inno Setup installer prototype for install/uninstall validation.

## Options

| Option | Pros | Cons | Signing | Updates | Uninstall | Startup registration |
| --- | --- | --- | --- | --- | --- | --- |
| Portable ZIP | Simple, transparent, no installer state, easy to inspect. | Unsigned EXE warnings likely, manual updates, user must remove files manually. | Optional for private RC; recommended before wider distribution. | Replace extracted files, preserve `%AppData%\PointyPal`. | Delete extracted folder; optionally delete `%AppData%\PointyPal`. | App writes HKCU Run only when user enables Start with Windows. |
| Inno Setup | Familiar Windows installer, shortcuts, uninstall entry, custom tasks. | Installer script maintenance, signing needed for trust, manual update channel unless added. | Sign EXE and installer for wider distribution. | Replace install directory, optional custom updater later. | Standard Programs and Features uninstall. | Installer can offer startup task, but PointyPal should still require user consent. |
| WiX/MSI | Enterprise-friendly, Group Policy deployment, repair semantics. | More ceremony, MSI upgrade rules, less friendly for fast private RCs. | Sign MSI and binaries for production. | MSI major/minor upgrade flow. | Standard MSI uninstall. | Registry entries can be component-managed if explicitly chosen. |
| MSIX | Clean install/uninstall, package identity, integrity model. | Requires package signing and trusted certificate for sideloading. More packaging constraints. | Required. Microsoft notes MSIX packages must be signed and trusted on-device. See https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview. | Package update model, Store/private feed possible. | Clean package uninstall; app data handling follows package model. | Startup behavior must fit packaged app capabilities and user consent. |
| Microsoft Store | Store signing, distribution, reputation, update channel. | Certification requirements, Store account/process, less appropriate for early private RC. | Store signs on submission. | Store-managed updates. | Store-managed uninstall. | Store policy and packaged app capabilities apply. |

## SmartScreen

Unsigned private builds can trigger Windows warnings. Signing improves publisher identity and tamper resistance, but new signed apps can still need reputation over time. Portable ZIP and the Inno setup EXE are acceptable for known private testers who expect the warning.

## Recommendation

- Now: Portable ZIP with manifest, checksums, first-run docs, and no bundled secrets remains the primary release artifact.
- PT004 prototype: Inno Setup is selected for private RC installer validation. It installs to `{autopf}\PointyPal`, creates a Start Menu shortcut, offers an optional Desktop shortcut, offers an optional launch checkbox, and produces an uninstall entry.
- PT005: Use optional signing scripts to sign `PointyPal.exe` and the setup EXE when a certificate is available. Normal private RC release checks do not require signing.
- Later: Use a real code-signing certificate or Microsoft Trusted Signing before wider distribution; consider Microsoft Store release if PointyPal becomes public.

The installer does not require a signing certificate for private RC validation, does not implement auto-update, and must not bundle `config.json`, logs, debug artifacts, history/usage data, `.env`, PFX files, private keys, or secret/key files. Installer outputs are written to `artifacts\installer`.

For signed release candidates, verify:

- `PointyPal.exe` is signed after publish and before checksums are finalized.
- the setup EXE is signed after installer build.
- `installer-manifest.json` records SHA256, `signed`, `signatureStatus`, and `signer`.
- no PFX, private key, or exported certificate files are committed.

The Windows App Certification Kit is useful before Store/MSIX work because Microsoft documents tests for launch, crash/hang behavior, package metadata, and security checks: https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit-tests.
