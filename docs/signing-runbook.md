# Signing Runbook

This runbook covers local validation of the optional PT005 signing pipeline. It is not required for normal private RC builds.

## 1. Prerequisites

- Windows PowerShell
- Windows 10/11 SDK with `signtool.exe` and the Windows SDK Signing Tools component
- Optional for installer builds: Inno Setup 6 with `ISCC.exe`
- Optional local test only: a self-signed code signing certificate

`signtool.exe` can be on `PATH` or in a standard Windows Kits install path.

## 2. Verify Unsigned Artifacts

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1
```

Unsigned artifacts are reported but do not fail by default.

## 3. Create A Local Test Certificate

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-local-test-cert.ps1
```

The script prints a thumbprint for `CN=PointyPal Local Test Code Signing`.

This certificate is local-test only. It does not create public trust, does not solve SmartScreen reputation, and should not be used for public distribution.

## 4. Sign With Certificate Thumbprint

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\sign-artifacts.ps1 `
  -CertThumbprint "ABCDEF123456..." `
  -TimestampUrl "http://timestamp.digicert.com"
```

You can also set:

```powershell
$env:SIGN_CERT_THUMBPRINT = "ABCDEF123456..."
```

## 5. Sign With PFX

Keep the PFX outside the repository.

```powershell
$env:SIGN_CERT_PASSWORD = "<password from secure local input>"
powershell -ExecutionPolicy Bypass -File .\scripts\sign-artifacts.ps1 `
  -CertPath "C:\certs\pointypal-test.pfx" `
  -CertPassword $env:SIGN_CERT_PASSWORD
```

The password is passed to `signtool.exe` but is never printed by PointyPal scripts.

## 6. Verify Signatures

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1
```

Require valid signatures:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-signatures.ps1 -RequireSigned
```

`-RequireSigned` exits non-zero for unsigned or invalid required artifacts.

## 7. Release Check Modes

Unsigned-friendly default:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1
```

Report signature state:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -VerifySignatures
```

Fail if required artifacts are unsigned or invalid:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -VerifySignatures -RequireSigned
```

Sign if a certificate is configured, then verify:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -Sign -VerifySignatures
```

Include installer build and signature verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-check.ps1 -IncludeInstaller -VerifySignatures
```

## 8. Troubleshooting

`signtool.exe` not found: Install the Windows 10/11 SDK and include the Windows SDK Signing Tools for Desktop Apps component. Reopen PowerShell or add the Windows Kits `bin\x64` folder to `PATH`.

Certificate not found: Check that the PFX path exists, or that the thumbprint is in `Cert:\CurrentUser\My`.

Invalid PFX password: Re-enter the password through a secure local mechanism or set `SIGN_CERT_PASSWORD` only for the current shell session.

Timestamp server unavailable: Retry later or set `SIGN_TIMESTAMP_URL` to another trusted RFC 3161 timestamp server.

Self-signed certificate not trusted: This is expected unless the test machine trusts the certificate chain. Local signing pipeline validation is separate from public trust.

SmartScreen still warns: Signing and SmartScreen reputation are related but different. New signed apps can still warn until reputation builds.

Private key or PFX accidentally created in the repo: Move it outside the repository immediately. `.gitignore` excludes common certificate and private key formats, but ignore rules are not a secret-management system.
