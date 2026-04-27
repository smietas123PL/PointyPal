# Production Risk Register

This document tracks identified risks to the PointyPal production release, their severity, and mitigation status.

| Risk | Area | Severity | Likelihood | Mitigation | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Worker Credential Leak** | Security | Critical | Low | Move to Cloudflare Secrets; Rotate keys. | **In Progress** |
| **Unauthorized Worker Usage** | Cost | High | Medium | Add shared secret header to Worker requests. | **Planned** |
| **User Privacy Breach (Local)** | Privacy | High | Low | Disable debug artifacts by default; implement TTL. | **Planned** |
| **Anthropic API Latency** | Performance | Medium | High | Implement local provider fallback; UI spinner. | **Done** |
| **AssemblyAI Polling Timeout** | Reliability | Medium | Medium | Increase timeout; Add retry logic in Worker. | **In Progress** |
| **Startup Crash Loop** | Stability | High | Low | Implemented `StartupCrashLoopGuard`. | **Done** |
| **Unsigned Binary Flags** | Distro | Medium | High | Acquire Code Signing Certificate. | **Planned** |
| **UI Automation Performance** | Performance | Medium | Medium | Added timeout and depth limits to UIA scan. | **Done** |
| **Memory Leak in NAudio** | Stability | Medium | Low | `AudioPlaybackService` uses proper disposal patterns. | **Done** |
| **Prompt Injection** | Security | Low | Medium | Strict system instructions in Worker. | **Done** |
| **Screen Capture Fail (DRM)** | UX | Low | Low | Detect empty captures; notify user of black screen. | **Planned** |
| **Keyboard Hook Conflict** | Stability | Medium | Low | Use standard Windows Hotkey API; detect conflicts. | **Done** |

## Risk Level Definitions
- **Critical**: Could lead to full system compromise or massive financial loss.
- **High**: Significant impact on privacy or primary functionality.
- **Medium**: Noticeable impact on performance or secondary features.
- **Low**: Minor inconvenience or cosmetic issue.
