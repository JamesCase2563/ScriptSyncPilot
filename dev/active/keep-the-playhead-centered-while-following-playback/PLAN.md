# Keep the Playhead Centered While Following Playback

- **Issue:** [#52 — Keep the playhead centered while following playback](https://github.com/JamesCase2563/ScriptSyncPilot/issues/52)
- **Affected project:** `ScriptSyncPilot.FunscriptEditor`
- **Status:** Planning; implementation has not started.

## Goal

Keep the playhead marker at the horizontal center of the timeline while active playback advances, moving the timeline beneath it. Preserve the current zoom and keep the view anchored to the playhead during follow updates and seeks.

## Current behavior

`TimelineControl.SetPlayhead(seconds, followPlayback: true)` currently recenters only when the playhead leaves the visible time range. While it remains visible, its screen position changes. The current recenter preserves pixels per second and clamps the viewport to the timeline bounds. Manual pan suppresses follow until the playhead re-enters the viewport.

## Scope

- Keep the playhead centered on active follow updates whenever timeline bounds allow.
- Preserve the selected pixels-per-second value; following and seeking must not zoom.
- Define how direct seeks, playback pause, local timeline scrubbing, point navigation, manual pan, and manual zoom interact with center-follow.
- Define behavior near the beginning and end, where the viewport cannot remain centered without extending beyond the timeline bounds.
- Keep current selection and script actions unchanged as the viewport moves.

## Out of scope

- Changing mpv playback speed or playback-follow policy outside the timeline view.
- Adding a follow toggle or changing its default unless M1 establishes a concrete need.
- Changing point navigation, pan gestures, or timeline zoom limits beyond what is required for the centered-follow contract.

## Decisions to settle in M1

- Identify which playhead updates follow: active playback observations and discontinuous seeks should move the view; paused/manual position changes should not recenter unless explicitly requested.
- Keep direct user interaction responsive. In particular, define whether follow is suspended during a left-drag scrub and when it resumes after the seek completes.
- Preserve the existing manual-pan suppression contract: manual pan is not immediately undone while the playhead is outside the viewport. Define whether follow resumes on re-entry or on the next update after re-entry.
- At the start and end, clamp the viewport start to the valid timeline range. The playhead may move away from center as it approaches a boundary, without changing zoom or showing negative/out-of-duration time.
- While follow is active, recenter using the current playhead time and current pixels-per-second. Explicit user zoom should preserve the playhead anchor when center-follow is active; confirm this against existing pointer-anchored zoom behavior.

## Milestones

### M1 — Confirm follow and interaction behavior

- [ ] M1.1 Trace playhead update sources for playback, transport seeks, timeline scrubbing, point navigation, and manual position changes.
- [ ] M1.2 Confirm center-follow activation, pause behavior, manual-pan suppression/resumption, zoom anchoring, and start/end boundary behavior.
- [ ] M1.3 Record concrete acceptance criteria and test cases before changing the timeline.

### M2 — Implement centered playback follow

- [ ] M2.1 Keep the playhead centered on each eligible follow update while preserving pixels per second and avoiding script edits or additional seeks.
- [ ] M2.2 Clamp the viewport cleanly near both timeline boundaries and preserve the selected zoom.
- [ ] M2.3 Preserve the agreed behavior for scrubbing, manual pan, point navigation, pause, and explicit zoom.

### M3 — Verify and document

- [ ] M3.1 Add focused automated coverage for continuous centering, seeking, zoom preservation, boundary clamping, and manual interaction suppression/resumption.
- [ ] M3.2 Run the relevant FunscriptEditor test suite and manually check normal playback, forward/back seeks, start/end, zoom, pan, and selection.
- [ ] M3.3 Update the FunscriptEditor architecture documentation and record results and limitations in `VERIFICATION.md`.

## Verification approach

Use viewport start and pixels-per-second as the observable state. Tests should verify the playhead's rendered X position where centering is possible, unchanged zoom, bounded viewport start, no script mutation or extra seek, and deterministic behavior when follow is suppressed by user interaction.
