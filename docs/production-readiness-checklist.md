# Production Readiness Checklist

This checklist tracks the requirements for moving from Build021 to a production-ready release.

## 1. Build & Stability
- [x] All 79/79 tests passing.
- [x] `scripts/build.ps1` successfully packages portable build.
- [ ] Implement automated UI smoke tests.
- [ ] Verify build on Windows 10 and 11 clean installs.

## 2. Security & Privacy
- [x] Secret audit completed (no hardcoded keys).
- [x] Data flow map documented.
- [ ] Implement production-safe defaults (DebugArtifacts = false).
- [ ] Verify `AppLogService.Redact` covers all known PII/Secrets.
- [ ] Lockdown Cloudflare Worker CORS and Authentication.

## 3. Packaging & Distribution
- [x] Portable ZIP creation working.
- [ ] Decide on MSIX vs. Squirrel vs. Wix installer.
- [ ] Code signing certificate acquired and integrated into CI.
- [ ] App icon and assets finalized.

## 4. Resilience & Diagnostics
- [x] Crash loop detection implemented.
- [x] Provider fallback policy implemented; fake fallback is disabled by default in Normal Mode.
- [x] Local log rotation and retention implemented.
- [ ] Finalize "Diagnostics Bundle" generation (one-click ZIP for support).

## 5. Compliance & Legal
- [ ] Privacy Policy drafted.
- [ ] Terms of Service drafted.
- [ ] "About" box with licenses for NAudio, System.Drawing, etc.
- [ ] Onboarding flow clearly explains data sharing with AI providers.

## 6. Manual QA & Dogfooding
- [ ] 48-hour "soak test" (app running in background).
- [ ] Multi-monitor setup validation.
- [ ] High-DPI scaling validation.
- [ ] Offline behavior validation (graceful degradation).

## 7. Release Management
- [x] Freeze 001 baseline tagged.
- [ ] Production Track 002 (Hardening) started.
- [ ] Release Notes template created.
