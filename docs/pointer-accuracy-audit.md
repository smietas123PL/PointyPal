# Pointer Accuracy Audit (PT011/PT012)

> [!TIP]
> For instructions on how to calibrate and test accuracy yourself, see the [Pointer Calibration Guide](pointer-calibration.md) and [Pointer QA Checklist](pointer-qa-checklist.md).

## Coordinate Spaces

1.  **Screenshot Image Coordinates**:
    *   Origin (0,0) is the top-left of the captured image.
    *   Maximum values are `CaptureImageWidth - 1` and `CaptureImageHeight - 1`.
    *   Used by Claude to return `[POINT:x,y:label]`.
2.  **Physical Screen Pixels**:
    *   Raw pixels on the monitor as reported by Win32 API (`GetCursorPos`, `GetMonitorInfo`).
    *   Primary monitor top-left is usually (0,0), but multi-monitor setups can have negative coordinates.
    *   UI Automation (`BoundingRectangle`) uses this space.
3.  **WPF Device-Independent Pixels (DIPs)**:
    *   WPF units where 96 units = 1 inch.
    *   Calculated as `PhysicalPixels / (DPI / 96)`.
    *   Used by WPF `Window.Left`, `Window.Top`, `Canvas.Left`, etc.
    *   Currently, the pointer overlay (WPF window) and target ring are positioned using these units.
    *   The triangle pointer is offset from the target ring center by `PointerTargetOffsetPx`.
4.  **Monitor Bounds**:
    *   The rectangle defining a specific monitor in physical pixels.
5.  **Virtual Desktop Bounds**:
    *   The union of all monitor bounds in physical pixels.

## Transformations & Logic Flow

1.  **Capture**: `ScreenCaptureService` captures a monitor in physical pixels.
2.  **Downscaling**: If the monitor is wider than `maxWidth` (e.g., 1280px), the image is scaled down.
    *   `DownscaleFactor = maxWidth / OriginalWidth`.
3.  **Claude Logic**: Claude sees the downscaled image and returns coordinates in **Screenshot Image Coordinates**.
4.  **Mapping**: `CoordinateMapper` converts **Image Coordinates** back to **Physical Screen Pixels**.
    *   `PhysicalX = MonitorBounds.X + (ImageX / DownscaleFactor)`.
5.  **Snapping**: `PointValidationService` uses **Physical Screen Pixels** to find nearest UI elements and snaps to their centers.
6.  **Flight**: `CursorOverlayWindow` receives the **Physical Screen Target** and animates the avatar.
    *   **BUG IDENTIFIED**: The avatar (WPF window) uses DIPs, but the flight target is currently physical pixels. This causes offset on High-DPI screens.

## DPI & Downscaling

*   **Downscaling** occurs in `ScreenCaptureService` to fit Claude's input constraints.
*   **DPI Conversion** occurs (implicitly or explicitly) in WPF windows. WPF handles scaling of the window content, but `Left` and `Top` must be set in DIPs.
*   **Mixed DPI**: In multi-monitor setups with different scaling factors, coordinate mapping must account for the specific DPI of the monitor where the point is located.

## Known Failure Modes & Risks

1.  **DPI Mismatch**: As noted, the avatar position is currently incorrect if DPI != 100% because it treats physical pixels as DIPs.
2.  **Integer Rounding**: Converting between scaled spaces can introduce small offsets (1-2 pixels) if rounding is done too early.
3.  **Multi-Monitor Offsets**: Negative coordinates on secondary monitors can be tricky if not handled explicitly.
4.  **Scaling Jumps**: If an app is scaled by the OS (DPI virtualization), UIA coordinates might not perfectly align with the visual screenshot.
5.  **Taskbar/DPI awareness**: If PointyPal is not "Per-Monitor DPI Aware V2", it might receive virtualized coordinates instead of physical ones.

## Current Mitigations

*   `CoordinateMapper` uses the `OriginalWidth/Height` from the capture to reverse scaling.
*   `ScreenUtilities.GetMonitorBounds` uses `MonitorFromPoint` to stay within the correct monitor.
*   Snapping helps "correct" small AI errors by landing on the center of a button.

## Remaining Risks

*   High-DPI accuracy is the primary risk.
*   Complex multi-monitor topologies (e.g., vertical arrangements).
*   Coordinate ambiguity in the code (mixing types without explicit labels).
