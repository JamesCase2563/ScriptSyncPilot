# Navigate Between Script Points — Plan

**Status:** In Progress.
**Issue:** [#53 — Navigate between script points](https://github.com/JamesCase2563/ScriptSyncPilot/issues/53).
**Affected project:** FunscriptEditor.
See [VERIFICATION.md](VERIFICATION.md) for the current behavior and planned checks.

## Goal

Let an editor user move the playhead directly to the next or previous action, or to the currently selected action, while preserving timeline zoom and leaving the script unchanged.

## Scope

- Add next-action, previous-action, and selected-action navigation.
- Navigate relative to the current playhead for next/previous commands.
- Route navigation only when the timeline itself has keyboard focus; editable inputs and other controls retain their native behavior.
- Preserve existing playback stepping, numeric position entry, play/pause, delete, zoom, and manual navigation behavior.
- Reuse the timeline's existing seek event and the form's mpv seek path.
- Cover the command semantics and keyboard routing with automated tests.

Out of scope: multi-point selection, point nudging, marker/snap behavior, timeline zoom/follow redesign, or changing the existing playback stepping shortcuts.

## Confirmed interaction contract

M1.4 confirms these bindings. They are unused by the inspected main-editor shortcut handlers; implementation and tests must still verify WinForms routing and focus isolation.

| Command | Confirmed shortcut | Behavior |
| --- | --- | --- |
| Previous point | Ctrl+Up | Seek to the action with the greatest timestamp strictly before the playhead. |
| Next point | Ctrl+Down | Seek to the action with the least timestamp strictly after the playhead. |
| Selected point | Enter | Seek to the selected action; no selection is a no-op. |

Add three buttons—**Previous point**, **Selected point**, and **Next point**—in a compact row directly below the existing time/frame seek controls. Keep the existing seek row intact. Show a short shortcut hint beside the new buttons, following the current seek row's visible hint style (for example, `Shortcuts: Ctrl+Up = previous, Ctrl+Down = next, Enter = selected`). Button clicks and shortcuts invoke the same navigation commands.

- Disable Previous/Next when there is no eligible point in that direction; do not wrap at either end.
- Disable Selected when there is no selected point.
- Refresh button availability when the playhead, selection, or script changes.

- Do not wrap at the first or last action. If no action exists in the requested direction, leave the playhead and viewport unchanged.
- A navigation command with no script actions is a no-op.
- A selected-action command uses the current selection at invocation time. If selection is absent, it is a no-op.
- Successful navigation updates the playhead and requests a seek to the action timestamp. It does not change action data, selection, history, or zoom.
- Keep the current Left/Right frame stepping and Shift/Ctrl+Left/Right time stepping unchanged.
- Accept these shortcuts only while the timeline has focus. Do not consume them in text/numeric inputs, dialogs, or other controls.

## Milestones

### 1. Confirm semantics and routing

- [x] M1.1 Confirm current playback, editing, position-entry, and form shortcut routing; identify any additional conflicts (see [VERIFICATION.md](VERIFICATION.md)).
- [x] M1.2 Confirm next/previous navigation is relative to the playhead and uses strict timestamp comparisons (see [VERIFICATION.md](VERIFICATION.md)).
- [x] M1.3 Confirm no-wrap boundary behavior and no-selection behavior (see [VERIFICATION.md](VERIFICATION.md)).
- [x] M1.4 Confirm Ctrl+Up for Previous, Ctrl+Down for Next, and Enter for Selected, scoped to timeline focus (see [VERIFICATION.md](VERIFICATION.md)).
- [x] M1.5 Confirm the compact point-navigation row below the existing seek controls and visible shortcut hint (see [VERIFICATION.md](VERIFICATION.md)).

### 2. Implement navigation

- [ ] Add timeline navigation operations for previous, next, and selected actions.
- [ ] Update the playhead and raise the existing seek request without changing zoom or script history.
- [ ] Route the agreed shortcuts only when the timeline is focused; preserve text-field input and existing keyboard commands.
- [ ] Add Previous/Selected/Next point buttons below the seek row and keep each button in sync with its shortcut's enabled state.
- [ ] Add focused tests for action ordering, selection, boundaries, empty scripts, seek requests, zoom preservation, and key routing.
- [ ] Verify button clicks invoke the same navigation operations as their corresponding shortcuts and the shortcut hint is visible.

### 3. Verify and close

- [ ] Run the focused navigation tests and full solution Release suite; record exact commands and results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Build the solution in Release and check the final diff for formatting and scope.
- [ ] Smoke-check the shortcuts in the running Windows editor if available; record whether a live mpv seek was exercised.
- [ ] Map every issue #53 acceptance criterion to test or manual evidence and record any remaining limitation.

## Acceptance criteria

- Users can navigate to the next and previous points and to the currently selected point.
- The playhead seeks to the target timestamp without changing zoom.
- Shortcut bindings and whether next/previous follow the playhead or selection are explicitly recorded.
- Empty-script, no-selection, and first/last-action behavior are defined and verified.
- Navigation shortcuts do not conflict with existing controls and are not consumed by focused input fields.
- Automated verification results and any manual/hardware limits are recorded in [VERIFICATION.md](VERIFICATION.md).
