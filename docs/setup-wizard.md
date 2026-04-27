# PointyPal Setup Wizard & Tutorial

PointyPal includes a guided setup wizard and in-app tutorials to help you get started quickly and safely.

## First-Run Experience

On your first launch, PointyPal will automatically open the **Setup Wizard**. This wizard guides you through:

1.  **Welcome**: An overview of what PointyPal does (screen context, voice commands, pointing).
2.  **Privacy**: A clear explanation of what data is sent to your Worker and what is stored locally. You can apply privacy-safe defaults here.
3.  **Worker Connection**: Configuration of your `WorkerBaseUrl` and `WorkerClientKey`. You can test reachability directly.
4.  **Voice Input**: Testing your microphone locally to ensure PointyPal can hear you.
5.  **Voice Output**: Testing text-to-speech (TTS) through your Worker (optional).
6.  **Hotkeys**: Learning the essential hotkeys for daily use (Right Ctrl, Ctrl+Space, Escape).
7.  **Real Flow Test**: Running an end-to-end test with Claude through your Worker.
8.  **Complete**: Final status overview and startup options.

## In-App Tutorials

You can access the **Tutorials** at any time from the **Control Center > Getting Started** tab.

Available tutorials:
*   **Ask by Voice**: Using Push-to-Talk with Right Ctrl.
*   **Quick Ask**: Typing requests with Ctrl + Space.
*   **Cancel with Escape**: Instantly stopping any operation.
*   **Pointer Explanation**: How PointyPal points at things on your screen.
*   **Privacy & Data**: Detailed information about data handling.
*   **Developer vs Normal Mode**: Understanding the different operating modes.

## Re-running the Wizard

If you need to re-configure your setup, you can reopen the wizard from:
*   **Control Center**: Click "Run Setup Wizard" in the Getting Started tab.
*   **Tray Menu**: Right-click the PointyPal icon and select "Setup Wizard".

## Safe Mode Behavior

If PointyPal starts in **Safe Mode** (e.g., after multiple crashes), the Setup Wizard will show a prominent warning banner. Real AI, STT, and TTS calls are disabled in Safe Mode to prevent further issues during recovery.

## Troubleshooting

If the Worker Connection test fails:
1.  Verify your Internet connection.
2.  Ensure your Worker URL is correct and starts with `https://`.
3.  Check your Worker logs in the Cloudflare dashboard.
4.  Verify your `WorkerClientKey` matches the one configured on your Worker.
