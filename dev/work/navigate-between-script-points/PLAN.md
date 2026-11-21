# Navigate Between Script Points — Plan

**Status:** Planned; implementation has not started.
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

## Proposed interaction contract

These bindings use currently unused keys in the inspected editor code; verify them against the full UI before implementation.

| Command | Proposed shortcut | Behavior |
| --- | --- | --- |
| Previous action | Ctrl+Up | Seek to the action with the greatest timestamp strictly before the playhead. |
| Next action | Ctrl+Down | Seek to the action with the least timestamp strictly after the playhead. |
| Selected action | Enter | Seek to the selected action; no selection is a no-op. |

- Do not wrap at the first or last action. If no action exists in the requested direction, leave the playhead and viewport unchanged.
- A navigation command with no script actions is a no-op.
- A selected-action command uses the current selection at invocation time. If selection is absent or stale, it is a no-op.
- Successful navigation updates the playhead and requests a seek to the action timestamp. It does not change action data, selection, history, or zoom.
- Keep the current Left/Right frame stepping and Shift/Ctrl+Left/Right time stepping unchanged.
- Accept the proposed shortcuts only while the timeline has focus. Do not consume them in text/numeric inputs, dialogs, or other controls.

## Milestones

### 1. Confirm semantics and routing

- [ ] Confirm current playback, editing, position-entry, and form shortcut routing; identify any additional conflicts.
- [ ] Confirm next/previous navigation is relative to the playhead and uses strict timestamp comparisons.
- [ ] Confirm no-wrap boundary behavior and no-selection behavior.
- [ ] Confirm the proposed Ctrl+Up, Ctrl+Down, and Enter bindings, or record an alternative before coding.

### 2. Implement navigation

- [ ] Add timeline navigation operations for previous, next, and selected actions.
- [ ] Update the playhead and raise the existing seek request without changing zoom or script history.
- [ ] Route the agreed shortcuts only when the timeline is focused; preserve text-field input and existing keyboard commands.
- [ ] Add focused tests for action ordering, selection, boundaries, empty scripts, seek requests, zoom preservation, and key routing.

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
