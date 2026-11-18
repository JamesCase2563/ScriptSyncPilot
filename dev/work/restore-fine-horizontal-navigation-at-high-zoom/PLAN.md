# Fine Horizontal Navigation at High Zoom — Plan

**Status:** In progress; design and verification criteria are recorded, and work has started. The navigation/follow implementation and its verification remain outstanding.
**Issue:** [#54 — Restore fine horizontal navigation at high zoom](https://github.com/JamesCase2563/ScriptSyncPilot/issues/54).
**Affected project:** FunscriptEditor.
See the [design](DESIGN.md) and [verification plan](VERIFICATION.md).

## Goal

Restore reliable fine horizontal navigation at high zoom, provide a dependable pan gesture that does not require a working mouse wheel or middle button, and stop playback-follow from immediately snapping the view back after a manual pan.

## Milestones

### 1. Reproduce and characterize

- [ ] Reproduce the reported middle-button pan failure in the running application and record the exact steps, zoom level, focus, and mouse behavior.
- [ ] Trace the actual WinForms middle-button events and capture changes; compare them with the synthetic-handler coverage in `MiddleDrag_PansAndClampsWithoutSeekingOrEditing`.
- [ ] Characterize playback-follow behavior when panning away from the playhead and identify the smallest state change needed to hold the viewport until the playhead returns.
- [ ] Confirm Shift+left-drag can be routed on empty track space without taking over point editing or unmodified scrubbing; ordinary wheel zoom remains unchanged.

### 2. Implement navigation and follow behavior

- [ ] Fix the middle-button regression if the characterization identifies a code defect; preserve existing mouse capture cleanup.
- [ ] Add Shift+left-drag horizontal panning on empty track space while preserving middle-drag as an additional gesture.
- [ ] Convert pointer movement to time using the current pixels-per-second value and clamp both ends; ensure button/modifier release and capture loss end panning cleanly.
- [ ] Suspend follow recentering after manual panning only while the playhead is outside the viewport; resume once it re-enters.
- [ ] Add a visible, restrained focus indicator to the timeline so users can tell when its keyboard shortcuts are active; ensure it follows focus changes and remains perceivable with high-contrast settings.
- [ ] Add focused automated coverage for the new wheel-pan behavior and follow suppression, reusing the existing middle-drag test where appropriate.

### 3. Verify and close

- [ ] Run focused FunscriptEditor tests and the full solution test suite; record exact commands and results in `VERIFICATION.md`.
- [ ] Manually verify middle-drag and Shift+left-drag navigation in the running Windows application at high zoom, including while playback is active and with the wheel unavailable.
- [ ] Check start/end clamping, unchanged zoom and script state, capture loss, and follow resumption when the playhead returns to the viewport.
- [ ] Verify the timeline focus cue appears when the timeline receives focus, clears when another control receives focus, and does not obscure timeline content or action points.
- [ ] Record any remaining limitations and link the results from issue #54.

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
