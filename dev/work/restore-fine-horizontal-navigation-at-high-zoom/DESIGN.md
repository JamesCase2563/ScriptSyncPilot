# Fine Horizontal Navigation at High Zoom — Design

**Status:** In progress; design is recorded and work has started. The pan/follow implementation and manual reproduction check are still pending.
**Affected project:** FunscriptEditor.
**Issue:** [#54 — Restore fine horizontal navigation at high zoom](https://github.com/JamesCase2563/ScriptSyncPilot/issues/54).
See the [implementation plan](PLAN.md) and [verification criteria](VERIFICATION.md).

## Goal

Make the timeline reliably navigable in small horizontal increments while highly zoomed in. Preserve the existing editing and scrubbing gestures, keep the current zoom, and prevent playback-follow from immediately undoing a user's manual pan.

## Current behavior

- [`TimelineControl`](../../../src/ScriptSyncPilot.FunscriptEditor/TimelineControl.cs) already implements middle-button drag-pan by changing `_viewStartSeconds`, clamping it to the available duration, and updating the horizontal scrollbar.
- The timeline's ordinary mouse wheel zooms around the pointer. [`TimelineWheelFilter`](../../../src/ScriptSyncPilot.FunscriptEditor/TimelineWheelFilter.cs) forwards wheel input to the timeline when the pointer is over it, even if another control has focus.
- Left-click on empty timeline space scrubs; point dragging edits an action; the right-side value bar inserts an action at the playhead. These gestures must remain unchanged.
- Playback follow recenters the view when the playhead leaves the viewport. It currently resumes on the next playhead update after a pan ends, which can make the view jump back immediately.
- `MiddleDrag_PansAndClampsWithoutSeekingOrEditing` in [`NumericPositionEntryTests`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) invokes protected handlers directly. It checks timeline state but does not establish that a physical middle-button drag is delivered reliably by the running WinForms application.

## Proposed interaction

1. Keep middle-button drag as a supported pan gesture and first reproduce the reported failure in the running app. Check the native mouse event and capture lifecycle before changing the existing pan path.
2. Add **Shift + left-drag** as the wheel-independent pan gesture, following the temporary modifier-drag pattern used by canvas applications such as Photoshop. Start the pan only when the drag begins on empty track space; left-drag on an action point continues editing, and an unmodified left-drag on empty track space continues scrubbing. Keep middle-button drag as an additional supported gesture.
3. Shift+left-drag changes only `_viewStartSeconds`, then uses the existing clamp, scrollbar-update, and repaint path. Convert horizontal pointer movement to timeline time using the current pixels-per-second scale so movement remains precise at high zoom. Pan must not modify zoom, playhead time, actions, or selection. The ordinary mouse wheel continues to zoom at the cursor.
4. Use the same pan state and cleanup path for middle-button and Shift+left-drag. Handle mouse capture loss and modifier/button release so a pan always ends cleanly.
5. Give the timeline a clear, restrained visual focus cue while it has keyboard focus, such as a focus-colored border or outline. Choose a treatment that fits the existing UI and remains visible with Windows high-contrast settings. Remove the cue when focus moves to another control. This helps users discover when timeline-specific keyboard shortcuts are active.

## Playback-follow behavior after manual pan

When a user manually pans while playback follow is active, suspend automatic recentering while the playhead is outside the newly chosen viewport. Keep the manually selected view in place rather than snapping back on the next playback update. Resume normal follow once the playhead re-enters the visible time range; subsequent playback can then follow normally. A manual seek that brings the playhead into view naturally satisfies the same rule.

Do not add a new follow-mode toggle as part of this issue. Paused playback does not issue follow updates; when playback resumes, normal follow behavior applies unless the playhead has already re-entered the view.

## Invariants

- Horizontal pan preserves `_pixelsPerSecond`; only the viewport start changes.
- Pan never seeks, edits, inserts, deletes, or changes selection/action history.
- Pan position is clamped to `[0, DurationSeconds - ViewportSeconds()]` and kept synchronized with the existing scrollbar.
- Left-click scrubbing, point editing, value-bar insertion, wheel zoom, keyboard behavior, and resize/zoom behavior remain intact.
- Follow suppression applies only after manual navigation displaced the playhead from the viewport and ends when the playhead is visible again.
- Mouse capture loss ends middle-button drag cleanly.
- Shift+left-drag on empty track space pans without requiring a working wheel or middle button; ordinary left-drag still scrubs and action-point dragging still edits.
- Keyboard focus on the timeline is visibly distinguishable from focus on other controls, and the cue clears when focus leaves.

## Out of scope

Redesigning the timeline, changing the zoom model or scrollbar, adding a permanent hand-tool mode or follow toggle, changing point-selection semantics, and changing playback synchronization are not part of this work.
