# Keep the Playhead at an Adjustable Follow Anchor

- **Issue:** [#52 — Keep the playhead centered while following playback](https://github.com/JamesCase2563/ScriptSyncPilot/issues/52)
- **Affected project:** `ScriptSyncPilot.FunscriptEditor`
- **Status:** Complete.

## Goal

Keep the playhead marker at a user-set horizontal anchor while active playback advances, moving the timeline beneath it. The anchor defaults to the center, can be repositioned by timeline interaction, and can be reset with a Center Playhead button. Preserve the current zoom while following.

## Current behavior

`TimelineControl.SetPlayhead(seconds, followPlayback: true)` currently recenters only when the playhead leaves the visible time range. While it remains visible, its screen position changes. The current recenter preserves pixels per second and clamps the viewport to the timeline bounds. Manual pan suppresses follow until the playhead re-enters the viewport.

## M1.1 — Playhead update source trace

- **Playback and external/player position changes:** `MpvSession.PositionChanged` is subscribed in `MainForm.AttachAsync` and routed through `MainForm.OnMpvPositionChanged`. The UI callback checks the active session and pending paused-seek state, then calls `_timeline.SetPlayhead(seconds, followPlayback: !session.IsPaused)`. This is the only current route that enables follow, and only while mpv reports active playback.
- **Timeline click and scrub:** `TimelineControl.OnMouseDown` begins scrubbing on an empty timeline track and calls `Seek(e.X)`. `OnMouseMove` calls `Seek` while scrubbing. `Seek` updates the timeline playhead immediately and raises `SeekRequested`; `MainForm.OnSeekRequested` sends an absolute mpv seek. Mouse-up ends scrubbing without another seek. Clicking on an existing point selects/drags it rather than seeking; clicking the value bar inserts/updates a point.
- **Transport seek buttons and playback keyboard commands:** the ±1/±10 second buttons and corresponding timeline-focused shortcuts call `StepAsync`, which sends a relative mpv seek. Frame buttons/shortcuts call `StepFrameAsync`. Neither updates the timeline directly; the resulting `PositionChanged` event updates it. While paused those updates use `followPlayback: false`.
- **Previous/Next/Selected point navigation:** `TimelineControl.NavigateToPreviousAction`, `NavigateToNextAction`, and `NavigateToSelectedAction` update `_playheadSeconds` immediately and raise `SeekRequested`. `MainForm` sends the absolute seek. Paused seeks use a pending-seek generation and a correlated position query before accepting the final playhead value; stale paused `PositionChanged` notifications are ignored during that verification.
- **Numeric/manual time entry:** `PatternInsertionDialog` receives `MainForm.MovePlayheadTo` for its seek-to-time action. That method updates the timeline playhead immediately and routes the seek through `OnSeekRequested`. There is no separate main-form numeric position editor in the traced code. Direct seeks made through mpv outside the editor arrive through the same `PositionChanged` path as playback observations.
- **Pan and zoom:** timeline pan changes `_viewStartSeconds`; zoom slider and wheel update zoom/view through `PixelsPerSecond`/`ZoomAt`. Neither currently records a follow anchor. Pan release currently suppresses playback follow until the playhead becomes visible again.

**Implementation implications:** follow-anchor placement must be explicit at each user action, rather than inferred from every `SetPlayhead` call. Timeline click and scrub release establish the pointer position as anchor; transport, point-navigation, and numeric time seeks preserve the active anchor. Playback observations move the viewport to hold the current anchor. The current paused-seek correlation should remain intact, with final reported seek position applied at the same anchor. Remove the current pan suppression/re-entry behavior; it conflicts with the confirmed requirement for movie-speed scrolling while the playhead is offscreen. Treat point dragging/value-bar editing as script edits, not seeks or anchor changes.

## Scope

- Store the follow anchor as a horizontal position within the timeline view; default it to center.
- A timeline click that seeks sets the anchor to the click's horizontal position. During playback the playhead remains there while the timeline scrolls underneath.
- Pan and zoom update the anchor to the playhead's resulting horizontal position. If the playhead is panned offscreen, keep that anchor and continue scrolling the waveform at movie speed; the playhead line may remain offscreen until the user changes the anchor or presses Center Playhead.
- Add a Center Playhead action that moves the current playhead to the center, restores the centered anchor, and resumes follow.
- Preserve pixels per second during follow and seeking; anchor changes must not change zoom.
- Define how playback pause, local scrubbing, point navigation, manual pan, and manual zoom interact with the adjustable anchor.
- At movie boundaries, allow the viewport to extend beyond the movie range and render out-of-movie time transparently, with clear start/end boundary lines.
- Keep current selection and script actions unchanged as the viewport moves.

## Out of scope

- Changing mpv playback speed or playback-follow policy outside the timeline view.
- Adding a follow toggle or changing its default unless M1 establishes a concrete need.
- Changing point navigation or timeline zoom limits beyond what is required for the adjustable-anchor follow contract.

## Decisions to settle in M1

- **Confirmed:** anchor defaults to center; timeline seek click assigns the anchor to clicked X; transport and point-navigation seeks preserve the current anchor, including while paused.
- **Confirmed:** pan and zoom set/update the anchor from the playhead's resulting horizontal position. If that anchor lies outside the viewport, follow continues at movie speed and the marker remains offscreen; do not suspend and later snap on re-entry.
- **Confirmed:** timeline scrubbing stays pointer-driven; the release position becomes the anchor and follow then continues.
- A passive position refresh while paused does not move the viewport; an explicit seek may reposition it at the current anchor.
- **Confirmed:** allow transparent viewport space before movie start and after movie end, with visible movie start/end lines.
- **Confirmed:** Center Playhead moves the current playhead to the center, sets the anchor there, and continues follow.
- Keep pixels per second unchanged during follow, seek, pan, and anchor changes. Zoom changes only through the user's zoom action and updates the anchor from the playhead's resulting position.

## Milestones

### M1 — Confirm follow and interaction behavior

- [x] M1.1 Trace playhead update sources for playback, transport seeks, timeline scrubbing, point navigation, and manual position changes.
- [x] M1.2 Confirm follow-anchor, pause/seek, pan/zoom, scrubbing, recenter-button, and start/end boundary behavior.
- [x] M1.3 Record concrete acceptance criteria and test cases before changing the timeline.

### M2 — Implement adjustable-anchor follow

- [x] M2.1 Keep the playhead at the active horizontal anchor on eligible follow updates, including when that anchor is offscreen, while preserving movie-speed scrolling, pixels per second, and avoiding script edits or additional seeks.
- [x] M2.2 Support out-of-movie transparent viewport regions and clear movie start/end boundary lines without changing the zoom.
- [x] M2.3 Complete anchor updates for timeline clicks, zoom, scrubbing, point navigation, pause, and Center Playhead; pan release/capture-loss anchor tracking is implemented in M2.1.

### M3 — Verify and document

- [x] M3.1 Add focused automated coverage for anchor updates, seeking, zoom preservation, boundary rendering, and offscreen follow after manual interaction.
- [x] M3.2 Run the FunscriptEditor test suite and manually check normal playback, forward/back seeks, start/end, zoom, pan, and selection. Automated and user-performed manual checks passed.
- [x] M3.3 Update the FunscriptEditor architecture documentation and record verification results and limitations in `VERIFICATION.md`.

## Verification approach

Use viewport start, pixels-per-second, anchor X, and movie bounds as observable state. Tests should verify that eligible updates hold the playhead at the active anchor, including offscreen anchors, while scrolling at movie speed; clicks/pan/zoom update the anchor as agreed; transparent out-of-movie regions and boundary lines appear; zoom remains unchanged by follow; and no script data or extra seeks are introduced.
