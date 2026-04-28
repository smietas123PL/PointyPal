# PointyPal Pointer Overlay — Final v2

This document describes the final v2 pointer overlay implemented in PT013.

## Visual Principles

- **Triangle-only**: The circular avatar background has been removed in favor of a sleek, modern triangle pointer.
- **Shared Status Slot**: Feedback for all active states (Listening, Processing, Speaking, Error, Safe Mode, Developer Mode) is integrated into a single status slot below the pointer.
- **Visual-only**: The pointer remains a visual guide and does not replace the system cursor or perform auto-clicking.
- **True Hit Point**: The target ring represents the exact hit point. The pointer triangle stays offset from the target.

## State Mapping

| App State | Visual State | Status Slot |
|-----------|--------------|-------------|
| Following | Following    | None        |
| Listening | Listening    | Audio Bars  |
| Processing| Processing   | Spinner     |
| Speaking  | Speaking     | Voice Bars  |
| Pointing  | Pointing     | Target Ring |
| Error     | Error        | Red Badge   |
| Safe Mode | Safe         | Amber Badge |
| Developer | Developer    | DEV Badge   |

## Safe Mode Styling

Safe Mode now uses a subtle amber exclamation badge instead of a heavy orange background, keeping the interface lightweight while still providing clear feedback.

## Developer Mode

When Developer Mode is active and the app is idle, a small "DEV" badge is visible in the status slot.

## Configuration

The following settings in `AppConfig.json` control the overlay behavior:

- `PointerVisualStyle`: "TriangleV2"
- `PointerAuraEnabled`: Enables the subtle glow for active states.
- `PointerStatusSlotEnabled`: Enables the feedback slot below the pointer.
- `PointerVisualSizeDip`: The rendered size of the triangle pointer (default: 22).
- `PointerVisualGlowScale`: The scale multiplier for the active state aura (default: 1.10).
- `PointerStatusSlotScale`: The scale multiplier for the status slot (default: 0.65).
- `PointerTargetOffsetPx`: Controls the distance between the pointer tip and the target hit point.
- `PointerLabelMaxLength`: Limits the length of the target label.

## Relation to Calibration

The pointer overlay respects all existing calibration and accuracy logic. The target ring center aligns exactly with the `FinalScreenPhysicalPoint` calculated by the `PointValidationService`.
