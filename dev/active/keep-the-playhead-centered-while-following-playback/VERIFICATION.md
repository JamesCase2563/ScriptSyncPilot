# Adjustable Playhead Follow Anchor — Verification

- **Issue:** [#52 — Keep the playhead centered while following playback](https://github.com/JamesCase2563/ScriptSyncPilot/issues/52)
- **Status:** In Progress; implementation and automated verification are complete, manual runtime verification remains pending.

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

**M1.1 source trace complete.** Playback observations and direct mpv position changes arrive through `MpvSession.PositionChanged` → `MainForm.OnMpvPositionChanged`; only unpaused observations request follow. Timeline clicks/scrubs and point-navigation actions update the timeline immediately and emit `SeekRequested`; transport/frame stepping send relative mpv commands. Numeric seek-to-time from `PatternInsertionDialog` uses `MainForm.MovePlayheadTo`. Paused absolute seeks use generation-correlated verification. See M1.1 in `PLAN.md` for the full route trace and anchor implications.

`TimelineControl.SetPlayhead` now positions the viewport from `FollowAnchorX` on every eligible playback update, without changing pixels per second or clamping the resulting viewport start. This keeps the playhead at the same screen coordinate (including an offscreen coordinate) while the timeline advances at mpv's reported movie-time rate. The former pan suppression-until-visible behavior is removed; ending or losing capture during a pan records the playhead's resulting screen coordinate as the follow anchor. These paths do not issue seeks or mutate script actions.

M2.2 extends the scrollable viewport by one viewport width before time zero and after the final timeline time. The out-of-movie graph regions receive a translucent overlay, with a solid movie-start line and a dashed movie-end line. These changes do not modify pixels per second. The scrollbar maps to the extended viewport range.

M2.3 connects timeline clicks and scrubbing to the pointer anchor, updates the anchor to the playhead's resulting screen position after zoom, and keeps point and numeric seeks at the existing anchor. The Center Playhead button resets the anchor and view to the track center. Paused relative transport/frame steps query mpv after the command and align the confirmed position to the anchor; passive paused position notifications and pausing alone do not move the viewport.

M3.1 adds focused coverage for continuous follow, offscreen anchors, pan release, scrubbing, zoom, explicit seeks, center reset, and movie boundary rendering. The focused timeline tests passed: **29 passed, 0 failed**. M3.2's full FunscriptEditor suite passed: **288 passed, 0 failed, 0 skipped**. The solution build also succeeded with zero warnings and errors. `dev/FunscriptEditor/ARCHITECTURE.md` now documents the anchor and movie-boundary behavior. Manual playback and visual checks were not performed; they remain for user verification because computer control was previously stopped at the user's request.
