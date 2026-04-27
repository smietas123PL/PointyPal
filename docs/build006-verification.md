# Build006 Verification Guide

This guide describes how to verify the Cloudflare Worker integration and the updated Windows app behavior.

## 1. Cloudflare Worker Deployment

### Prerequisites
- Node.js and npm installed.
- Cloudflare account.
- Anthropic API Key.

### Steps
1. Navigate to the `worker/` directory:
   ```bash
   cd worker
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Set your Anthropic API Key secret:
   ```bash
   npx wrangler secret put ANTHROPIC_API_KEY
   ```
4. (Optional) Update `CLAUDE_MODEL` in `wrangler.toml` if desired.
5. Deploy the worker:
   ```bash
   npm run deploy
   ```
6. Note the deployed URL (e.g., `https://pointypal-worker.your-subdomain.workers.dev`).

## 2. Windows App Configuration

1. Locate your configuration file at `%AppData%\PointyPal\config.json`.
2. Update `WorkerBaseUrl` with your deployed worker URL.
3. Set `AiProvider` to `"Claude"`.

Example `config.json`:
```json
{
  "AiProvider": "Claude",
  "WorkerBaseUrl": "https://pointypal-worker.your-subdomain.workers.dev",
  "ClaudeModel": "claude-3-5-sonnet-20240620",
  "MaxImageWidth": 1280,
  "JpegQuality": 80,
  "RequestTimeoutSeconds": 30
}
```

## 3. Verification Steps

### Health Check
- Open your browser or use `curl` to visit `{WorkerBaseUrl}/health`.
- Expected response: `{"ok":true,"service":"pointypal-worker"}`.

### End-to-End Chat (Alt+F12)
1. Run `PointyPal.exe`.
2. Press `Alt+F12`.
3. The avatar should enter "Processing" state.
4. A screenshot is captured and sent to the Worker.
5. The Worker proxies the request to Claude with a system prompt in Polish.
6. Claude should respond in Polish.
7. If Claude identifies a relevant point, it should append `[POINT:x,y:Label]`.
8. The avatar should fly to the indicated point on your screen.

### Debug Artifacts
Check `%AppData%\PointyPal\debug\`:
1. `latest-ai-request.json`: Verify that `screenshotBase64` is ABSENT and `ScreenshotBase64Length` is present with a non-zero value.
2. `latest-ai-response.json`: Verify the raw JSON response from the Worker.
3. `latest-capture.jpg`: Verify the captured screenshot used for the request.

## Common Errors & Fixes
- **Worker Error (401)**: Ensure `ANTHROPIC_API_KEY` is set correctly in Cloudflare secrets.
- **Worker Error (400)**: Check if the request body is valid (view `latest-ai-request.json` for hints).
- **Avatar stays in Error state**: Check the diagnostics overlay (if enabled) or the log files in `%AppData%\PointyPal\logs`.
