# make-release-notes.ps1
# Generates release-notes.md for the RC.

$version = "RC1"
$date = Get-Date -Format "yyyy-MM-dd"
$portablePath = "artifacts/PointyPal-portable"
$notesPath = "artifacts/release-notes.md"

$content = @"
# PointyPal Release Candidate ($version)
**Release Date:** $date

## Overview
This is the first Release Candidate for PointyPal. It includes recovery tools, preflight checks, and local configuration hardening.

## Key Features
- **Safe Mode**: Launch with `--safe-mode` to bypass real AI providers if config is broken.
- **Config Backups**: Automatic backups created before saving settings.
- **Preflight Check**: Run environment validation from the Control Center.
- **Factory Reset**: Easily clear local state from the Recovery tab.
- **CLI Self-Test**: Run `--self-test` for quick diagnostic validation.

## How to Install
1. Extract the portable folder to a local directory.
2. Run `PointyPal.exe`.
3. Configure your `WorkerBaseUrl` in the Control Center.

## Known Limitations
- Requires a compatible Cloudflare Worker for Claude Vision and ElevenLabs TTS.
- No auto-updater (manual replacement of portable folder required).
- Microphone must be configured as default in Windows.

## Support
Refer to `docs/recovery.md` if you encounter startup issues.
Logs are located in `%AppData%/PointyPal/logs/`.
"@

$content | Out-File -FilePath $notesPath -Encoding utf8
Write-Host "Release notes generated at $notesPath" -ForegroundColor Green
