# Pointer QA Checklist

Use this checklist to verify pointer accuracy and UX in different environments.

## 1. Baseline Environment
- [ ] Number of monitors: _______
- [ ] DPI scale per monitor (e.g., 100%, 150%): _______
- [ ] Primary monitor identified: [ ] Yes [ ] No
- [ ] Virtual desktop layout (e.g., side-by-side): _______
- [ ] Windows Scale settings verified: [ ] Yes [ ] No
- [ ] App Version: _______
- [ ] Developer Mode enabled: [ ] Yes [ ] No

## 2. Calibration Tests
- [ ] **Center**: Pointing to center is accurate.
- [ ] **Top-Left**: Pointing to (0,0) of the monitor.
- [ ] **Top-Right**: Pointing to top-right corner.
- [ ] **Bottom-Left**: Pointing to bottom-left corner.
- [ ] **Bottom-Right**: Pointing to bottom-right corner.
- [ ] **Replay**: "Replay Last Point" works correctly.
- [ ] **Cancel**: Pressing `Escape` cancels the pointer flight.

## 3. Real UI Target Tests
- [ ] **Small Button**: Target < 32x32 px.
- [ ] **Large Button**: Standard UI button.
- [ ] **Text Field**: Clicks/Points to input area.
- [ ] **Menu Item**: Points to item in a dropdown.
- [ ] **Checkbox/Radio**: Points to the toggle element.
- [ ] **Tab**: Points to the tab header.
- [ ] **Hyperlink**: Points to inline text link.
- [ ] **List Item**: Points to item in a list or grid.
- [ ] **Non-clickable Label**: Points to static text.
- [ ] **Large Panel**: Points to a large container or background.

## 4. Multi-Monitor Tests
- [ ] **Primary Monitor**: Works correctly.
- [ ] **Secondary Monitor**: Works correctly.
- [ ] **Monitor to the Left/Above**: Works across virtual boundaries.
- [ ] **Different DPI**: Works when moving from 100% to 150% monitor.

## 5. Snapping Tests
- [ ] **Snapping Off**: Pointer goes to exact AI coordinates.
- [ ] **Snapping On**: Pointer snaps to the center of a nearby button.
- [ ] **Near Button**: Snaps correctly when within 60px.
- [ ] **Near Large Container**: Does not snap to huge backgrounds if a button is near.
- [ ] **Overlapping Controls**: Snaps to the top-most or most relevant control.
- [ ] **Far from Control**: No snapping occurs (stays at raw coordinates).

## 6. Claude Pointing Tests
- [ ] Ask: "What should I click?" -> Claude points to a valid action.
- [ ] Ask: "Point to the save button" -> Claude identifies and points to Save.
- [ ] Ask: "Explain but do not point" -> Claude responds with text only.
- [ ] Mode: **NoPoint** -> Verify no visual pointer appears.

## 7. UX Tests
- [ ] **Ring Visible**: The pointer ring is clearly visible on different backgrounds.
- [ ] **Marker Position**: Marker does not cover the exact center of the target (uses offset).
- [ ] **Label Readable**: Text label is legible.
- [ ] **Label Length**: Long labels are truncated gracefully.
- [ ] **Flight Speed**: Movement feels smooth (350-500ms).
- [ ] **Return Transition**: Returning to cursor feels natural (250-400ms).

## 8. Rating & Reporting
- [ ] Mark **Correct** -> Verify stats update.
- [ ] Mark **Close** -> Verify stats update.
- [ ] Mark **Wrong** -> Verify stats update.
- [ ] **Feedback Persistence**: Data is saved and reloaded on restart.
- [ ] **QA Export**: "Export Pointer QA Report" produces a valid redacted JSON.
