# Fine Horizontal Navigation at High Zoom — Plan

**Status:** Complete. Navigation/follow implementation, automated coverage, and Milestone 3 verification are recorded in `VERIFICATION.md`.
**Issue:** [#54 — Restore fine horizontal navigation at high zoom](https://github.com/JamesCase2563/ScriptSyncPilot/issues/54).
**Affected project:** FunscriptEditor.
See the [design](DESIGN.md) and [verification plan](VERIFICATION.md).

## Goal

Restore reliable fine horizontal navigation at high zoom, provide a dependable pan gesture that does not require a working mouse wheel or middle button, and stop playback-follow from immediately snapping the view back after a manual pan.

## Milestones

### 1. Reproduce and characterize

- [x] M1.1 Reproduce and characterize the reported middle-button pan failure. The reported failure was caused by mouse hardware, not an application software failure; see the finding in `VERIFICATION.md`.
- [x] Trace the actual WinForms middle-button events and capture changes; compare them with the synthetic-handler coverage in `MiddleDrag_PansAndClampsWithoutSeekingOrEditing`.
- [x] Characterize playback-follow behavior when panning away from the playhead. Follow is paused during the drag but recenters immediately after release when the playhead is outside the viewport; implementation must keep the manually panned view until the playhead re-enters.

### 2. Implement navigation and follow behavior

- [x] No middle-button code fix was needed: characterization attributed the reported failure to mouse hardware, not an application defect (see `VERIFICATION.md`).
- [x] Add Shift+left-drag horizontal panning on empty track space while preserving middle-drag as an additional gesture.
- [x] Convert pointer movement to time using the current pixels-per-second value and clamp both ends; ensure button/modifier release and capture loss end panning cleanly.
- [x] Suspend follow recentering after manual panning only while the playhead is outside the viewport; resume once it re-enters.
- [x] Add a visible, restrained focus indicator to the timeline so users can tell when its keyboard shortcuts are active; ensure it follows focus changes and remains perceivable with high-contrast settings.
- [x] Add focused automated coverage for Shift+left panning and follow suppression, reusing the existing middle-drag test where appropriate.

### 3. Verify and close

- [x] Confirm WinForms delivers Shift+left down/move/up on empty track space; trace-only handling bypasses empty-space scrubbing during this diagnostic gesture.
- [x] Verify action-point editing remains available with Shift held; verify unmodified empty-track dragging still scrubs.
- [x] Verify unmodified left-drag on empty timeline space still scrubs in the running application.
- [x] Verify ordinary wheel zoom still works in the running application.
- [x] Run the focused FunscriptEditor tests and the full solution test suite; record exact commands and results in `VERIFICATION.md`.
- [x] Manually verify middle-drag and Shift+left-drag navigation in the running Windows application at high zoom, including while playback is active and with the wheel unavailable. User confirmed both gestures work.
- [x] Verify start/end clamping, unchanged zoom and script state, pan cleanup, and follow resumption in automated coverage.
- [x] Verify the focus border appears in the regular theme, changes when focus leaves the timeline, and stays clear of timeline content.
- [x] Verify focus-border contrast in Windows high-contrast mode. User confirmed it works.
- [x] Record remaining manual-verification limitations and link the results from issue #54 in `VERIFICATION.md`.

## Acceptance criteria

- A user can move both earlier and later through the timeline in small increments at high zoom without changing zoom.
- The reported middle-button behavior is either restored or replaced with a documented, dependable interaction.
- Manual panning does not immediately jump back to the playhead; normal follow resumes when the playhead returns to the visible range.
- Pan does not seek, edit actions, change selection, or add history entries.
- Existing zoom, scrub, point-edit, value-bar insertion, and scrollbar behavior remains functional.
- Automated and manual evidence is recorded in `VERIFICATION.md`.

## Constraints

- Keep the work within `TimelineControl` and `TimelineWheelFilter` unless reproduction identifies a necessary narrowly scoped input-routing change elsewhere.
- Reuse the existing viewport, clamp, and scrollbar logic.
- Do not add packages, a new navigation mode, or a follow-mode UI toggle.
- Keep the Shift+left-drag interaction usable with a mouse whose wheel or middle button is unavailable.
- Keep the focus cue visually clear without obscuring timeline data or relying on color alone where possible.
