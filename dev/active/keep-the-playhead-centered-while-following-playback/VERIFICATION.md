# Keep the Playhead Centered While Following Playback — Verification

- **Issue:** [#52 — Keep the playhead centered while following playback](https://github.com/JamesCase2563/ScriptSyncPilot/issues/52)
- **Status:** Planning; implementation and verification are pending.

## Acceptance criteria

- [ ] During eligible playback-follow updates, the playhead remains at the horizontal center whenever the timeline bounds permit; the timeline moves beneath it.
- [ ] Follow updates and seeks preserve the selected zoom level and do not mutate script actions or issue an additional seek.
- [ ] Direct playback seeks move the timeline to the target and center the playhead when the agreed follow policy is active.
- [ ] At the beginning and end, viewport clamping is predictable, keeps the time range valid, and does not change zoom unexpectedly.
- [ ] Paused/manual position updates, timeline scrubbing, and point navigation follow the M1 interaction rules and remain responsive.
- [ ] Manual pan is not immediately undone; suppression and resumption follow the behavior agreed in M1.
- [ ] Point selection remains associated with the same action as the viewport moves.

## Automated verification plan

- Verify that consecutive eligible playback updates keep the playhead at the center while changing viewport start and preserving pixels per second.
- Verify that a discontinuous seek recenters without changing script contents or issuing a second seek.
- Verify start/end clamping when the playhead cannot be centered within the valid timeline range.
- Verify pause/manual updates, scrubbing, point navigation, and manual panning follow the M1 policy.
- Run the focused FunscriptEditor tests and then the full FunscriptEditor test project.

## Manual verification plan

- Play through several viewport widths and confirm the marker stays centered while the script scrolls underneath.
- Seek forward and backward, including a seek while paused, and confirm the agreed centering behavior and unchanged zoom.
- Check playback near the beginning and end of a script for stable boundary behavior.
- Zoom during follow and confirm the selected zoom is retained and the playhead remains the anchor.
- Manually pan, scrub, select points, and navigate between points; confirm follow does not fight the active gesture and resumes as specified.

## Results and limitations

No implementation or verification has been performed yet. The current timeline recenters only when the playhead leaves the viewport; M1 must confirm how the new continuous-center behavior interacts with paused seeks, scrubbing, manual panning, and zoom anchoring before implementation.
