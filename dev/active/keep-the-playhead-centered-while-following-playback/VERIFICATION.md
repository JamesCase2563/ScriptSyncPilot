# Adjustable Playhead Follow Anchor — Verification

- **Issue:** [#52 — Keep the playhead centered while following playback](https://github.com/JamesCase2563/ScriptSyncPilot/issues/52)
- **Status:** Planning; implementation and verification are pending.

## Acceptance criteria

- [ ] During eligible follow updates, the playhead remains at the active user-set horizontal anchor while the timeline moves underneath.
- [ ] Timeline click-to-seek sets the anchor to the clicked X position; playback holds the marker there without an unexpected jump to center.
- [ ] Pan and zoom update the anchor to the playhead's resulting X position. If panned offscreen, the playhead remains offscreen while the waveform continues scrolling at movie speed; there is no suspend/re-entry snap.
- [ ] Center Playhead returns the current playhead to center and resumes follow.
- [ ] Follow updates and seeks preserve the selected zoom level and do not mutate script actions or issue an additional seek.
- [ ] Explicit seeks, including while paused, use the active anchor; passive paused-position refreshes do not move the viewport.
- [ ] At the beginning/end, the viewport can extend beyond the movie range; out-of-movie time is transparent and start/end boundary lines are visible.
- [ ] Pausing alone does not move the viewport; timeline scrubbing remains pointer-driven, and point-navigation seeks follow the M1 rules.
- [ ] Manual pan and zoom set the anchor without stopping movie-speed scrolling; the playhead line can remain offscreen until the anchor is changed or Center Playhead is pressed.
- [ ] Point selection remains associated with the same action as the viewport moves.

## Automated verification plan

- Verify that consecutive eligible updates keep the playhead at the active anchor while changing viewport start and preserving pixels per second.
- Verify that timeline clicks and pan/zoom update the anchor as agreed, while explicit seeks preserve it.
- Verify that a discontinuous seek moves the view to the active anchor without changing script contents or issuing a second seek.
- Verify transparent out-of-movie regions and movie-boundary lines at both ends.
- Verify offscreen playhead behavior after pan: the waveform continues scrolling at the normal movie-time rate without snapping when the playhead re-enters.
- Verify pause/manual updates, scrubbing, point navigation, and manual panning follow the M1 policy.
- Run the focused FunscriptEditor tests and then the full FunscriptEditor test project.

## Manual verification plan

- Play through several viewport widths and confirm the marker stays at its chosen anchor while the script scrolls underneath.
- Click at different horizontal positions to seek, then play and confirm the marker stays at each chosen position.
- Seek with transport and point-navigation controls, including while paused, and confirm the active anchor and zoom are preserved.
- Check transparent out-of-movie regions and start/end boundary lines while playing at both ends.
- Pan and zoom during follow; confirm the anchor updates to the playhead's resulting position without the view snapping back.
- Pan the playhead offscreen, confirm the waveform continues scrolling at movie speed without snapping on re-entry, then use Center Playhead to restore centered follow.
- Scrub, select points, and navigate between points; confirm follow does not fight the active gesture and resumes as specified.

## Results and limitations

**M1.1 source trace complete; implementation and runtime verification are pending.** Playback observations and direct mpv position changes arrive through `MpvSession.PositionChanged` → `MainForm.OnMpvPositionChanged` → `TimelineControl.SetPlayhead`; only unpaused observations currently request follow. Timeline clicks/scrubs and point-navigation actions update the timeline immediately and emit `SeekRequested`; transport/frame stepping send relative mpv commands and wait for position observations. Numeric seek-to-time from `PatternInsertionDialog` uses `MainForm.MovePlayheadTo`. Paused absolute seeks use generation-correlated verification, which must be preserved. The current manual-pan suppression until re-entry conflicts with the agreed continuous movie-speed scrolling and must be removed. See M1.1 in `PLAN.md` for the full route trace and anchor implications.

The agreed behavior uses a user-set anchor, keeps movie-speed scrolling active even when the playhead line is offscreen, provides a Center Playhead action, and shows transparent out-of-movie regions with start/end lines. Existing code currently recenters only when the playhead exits the viewport; no implementation or runtime verification has been performed yet.
