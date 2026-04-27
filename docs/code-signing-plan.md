# Code Signing Plan

PT005 adds optional code signing preparation for PointyPal release artifacts. Normal private RC builds remain unsigned-friendly and do not require a certificate.

## Why Windows Warns

Unsigned `PointyPal.exe` and setup EXE files can trigger Windows Defender, SmartScreen, browser, or installer warnings because Windows cannot show a verified publisher identity. That is expected for a private RC.

Code signing provides publisher identity and tamper evidence. It does not instantly create SmartScreen reputation. New apps, new certificates, and new publishers can still warn until enough reputation is built.

## Private RC Options

- Unsigned portable ZIP or setup EXE: acceptable for a small known tester group when warnings are documented.
- Local self-signed test certificate: useful only for validating the signing pipeline on a developer machine.
- Real code-signing certificate or Microsoft Trusted Signing: recommended before wider distribution.

For MSIX, signing is required and the certificate must be trusted on the device. Microsoft documents that requirement here: https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview.

## Timestamping

Signed releases should use timestamping. Timestamping lets Windows verify that an artifact was signed while the certificate was valid, even after the certificate later expires.

The default timestamp URL used by `scripts/sign-artifacts.ps1` is:

```text
http://timestamp.digicert.com
```

Override it with `-TimestampUrl` or `SIGN_TIMESTAMP_URL` if needed.

## Files To Sign

Sign these release artifacts:

- `artifacts\PointyPal-portable\PointyPal.exe`
- `artifacts\installer\PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe`

Do not sign every DLL by default. Only expand signing coverage if a later distribution policy or platform requirement calls for it.

## What Not To Sign

Do not sign:

- `config.example.json`
- `release-manifest.json`
- `checksums.txt`
- Markdown docs
- user-local `config.json`
- logs, debug artifacts, backups, history, or usage data
- random generated files
- private keys, PFX files, or exported certificates

## Supported Inputs

Signing is opt-in and supports either:

- PFX path plus password: `SIGN_CERT_PATH` and `SIGN_CERT_PASSWORD`, or `-CertPath` and `-CertPassword`
- Certificate store thumbprint: `SIGN_CERT_THUMBPRINT`, or `-CertThumbprint`

Never store certificate passwords in files. Never commit PFX files, private keys, or exported certificate material.

## Release Process Fit

Normal release check:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1
```

Signature reporting without requiring signatures:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -VerifySignatures
```

Opt-in signing and verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -Sign -VerifySignatures
```

When `-Sign` is used, release-check calls `scripts/sign-artifacts.ps1` with `-SkipIfNoCertificate` so a missing cert warns and the private RC flow can continue. When signing succeeds, manifests and checksums are regenerated after signing because Authenticode signing changes file hashes.
