# Pointer Calibration Guide

This guide explains how to calibrate and test the pointing accuracy of PointyPal.

## Core Product Rules
- **PointyPal is visual-only**: It can point to UI elements with a visual triangle/ring marker.
- **No circular avatar**: The final v2 visual is triangle-only without a circular background.
- **Target ring is the true hit point**: The yellow/amber ring center is the exact target.
- **No auto-clicking**: PointyPal will NEVER click, type, or manipulate your UI.
- **No data collection**: All calibration data and feedback stay local to your machine.

## How to Enable Calibration Tools

1. Open the **Control Center**.
2. Go to **Settings** or **Setup**.
3. Enable **Developer Mode** (this exposes advanced diagnostics).
4. Go to the **Advanced** tab and select **Pointing**.

## Calibration Workflow

### 1. Run Calibration Overlay
Click **"Run Calibration"** in the Advanced/Pointing section. This will show a grid of points on your screen.

### 2. Center & Corner Tests
Test the following points to ensure the coordinate mapping is accurate:
- **Center**: Move your cursor to the center of the screen.
- **Corners**: Top-left, Top-right, Bottom-left, Bottom-right.

### 3. High DPI & Multi-Monitor
If you have multiple monitors or High DPI (scaling > 100%) displays:
- Move the cursor to each monitor and trigger a pointing action.
- Verify the ring appears exactly where intended.
- **Mixed DPI Support**: PointyPal handles monitors with different scaling factors automatically.

### 4. Visual State Verification
Observe the **Status Slot** below the triangle pointer:
- **Listening**: Audio bars animate.
- **Processing**: Spinner rotates.
- **Speaking**: Voice bars animate.
- **Error**: Red exclamation badge appears.
- **Safe Mode**: Subtle amber badge appears.
- **Developer**: "DEV" badge appears when idle.
- **Pointing**: Pointer tilts slightly and a target ring appears at the destination.

## Accuracy Feedback

When PointyPal points to a target, you may see a feedback prompt:
- **Correct**: The ring was perfectly centered on the target.
- **Close**: The ring was slightly off but the intent was clear.
- **Wrong**: The ring was far from the target or on the wrong element.
- **Dismiss**: Ignore the feedback prompt.

Feedback is stored locally and used to calculate your **Pointer Quality Score**.

## Pointer Quality Score
- **Good**: Accuracy is stable.
- **Needs Calibration**: Sample size is too low (< 5 attempts).
- **Needs Threshold Tuning**: Many "Close" results.
- **Needs Investigation**: Many "Wrong" results.

## Default Thresholds (Production)

Current recommended defaults for optimal stability:

| Setting | Value | Description |
|---------|-------|-------------|
| `PointSnappingEnabled` | `false` | Conservative off (on only if tests prove stable) |
| `PointSnappingMaxDistancePx` | `60–80` | Max distance to snap to a UI element |
| `PointerMarkerDurationMs` | `1200–1600` | How long the marker stays visible |
| `PointerFlightDurationMs` | `350–500` | Speed of the pointer movement |
| `PointerReturnDurationMs` | `250–400` | Speed of return to user cursor |
| `PointerTargetOffsetPx` | `16–24` | Offset to avoid covering the exact target pixel |
| `PointerFeedbackPromptEnabled` | `false` | Disabled in Normal Mode |
| `PointerFeedbackPromptDeveloperOnly` | `true` | Only show prompts when in Developer Mode |

## Known Limitations
- **Mixed DPI**: Some extreme configurations might require manual verification.
- **Fullscreen Apps**: Exclusive fullscreen apps might block the overlay.
- **Protected Windows**: Some system windows or DRM-protected apps may hide the pointer.
- **UI Automation**: Some legacy apps do not expose UI elements, making "Snapping" impossible.
- **Downscaled Screenshots**: Screenshots are downscaled for AI processing, which may impact precision on very small targets.
- **Display Topology**: Changing monitor layout while the app is running may require an overlay refresh.
