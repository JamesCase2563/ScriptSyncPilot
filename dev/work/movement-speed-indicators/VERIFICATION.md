# Movement Speed Indicators — Verification

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Status:** In Progress; implementation and verification are pending.

## Acceptance criteria

- [ ] A compact persistent readout directly below the timeline and above seek/point-navigation controls identifies values as script-space average speed at 1x; playback time remains in the transport row.
- [ ] Switching between one-line segment values and three-line point/drag values does not change the readout container height or resize the timeline.
- [ ] Hovering a segment shows whole-millisecond duration, whole-pp absolute distance, and speed to one decimal pp/s with units.
- [ ] A selected or dragged point shows both incoming and outgoing segment values; values update from the in-progress action list as the point is edited.
- [ ] Segment hover does not capture point hit targets or interfere with dragging; readout context follows the precedence confirmed in M1.2 without changing selection.
- [ ] The M1.2 readout precedence is verified: active drag, value-bar preview at playhead time and hovered position, hovered point, hovered segment, selected-point fallback, then cleared readout. Pan/scrub suppress hover contexts.
- [ ] Value-bar preview shows the incoming/outgoing values that would result from insertion or replacement, without changing the script until clicked.
- [ ] A segment with positive duration and zero position change reports `0.0 pp/s`; missing endpoint neighbors are labeled explicitly.
- [ ] Duplicate or non-increasing timestamps show `Invalid timing` with zero or negative duration, omit speed, and never receive a speed color.
- [ ] Coloring uses the single vivid, ordered multi-hue palette and has Fit to script as the default; fit uses all valid segments and P99 nearest-rank rounded up to the next 100 pp/s, with a 100 pp/s minimum upper bound.
- [ ] Preferences expose an on-by-default coloring toggle, Fit/Manual scale choice, manual min/max pp/s, and Fit to current script. Toggle, mode, and manual limits persist through `EditorSettings`.
- [ ] Manual range min/max preferences persist globally for cross-script comparisons; invalid `min >= max` is rejected without replacing the last valid range. Fit to script uses all valid segments, recalculates on script load or explicit invocation, and remains stable during edits/dragging.
- [ ] Speeds above/below the configured color range use endpoint colors without being removed or changing the actual numeric readout, which marks the color as capped high/low.
- [ ] Within a script, zooming and scrolling do not change its fitted range or a segment's color; manual-range colors remain comparable across scripts.
- [ ] Segment line coloring remains distinguishable at practical zoom; if not, a narrow aligned color strip uses the same time segments and mapping without acting as a separate speed graph.
- [ ] The legend and numeric readout remain usable in Windows high-contrast mode.
- [ ] Numerical values remain available when coloring is enabled; colors are not the only way to distinguish speed.
- [ ] The UI describes these as script-space segment-average speeds at normal (1x) playback and makes no safety or physical-device-speed claim.
- [ ] Existing point selection, dragging, and timeline navigation continue to work.

## Automated verification plan

- Test the pure speed calculation for increasing and decreasing positions, positive durations, zero-distance segments, and both invalid timestamp cases.
- Test formatting and endpoint presentation, plus selected/dragged point readouts using adjacent actions and refreshing as the edited action changes.
- Test point-versus-segment hover precedence, value-bar insertion/replacement preview, pan/scrub suppression, and readout clearing; verify dragging reads in-progress values.
- Test selected range modes, boundary values, endpoint clamping, and an unchanged mapped color for the same segment speed at different zoom and viewport settings.
- Test fit-to-script uses the full script, not the currently visible time range; test manual preference save/load and invalid-bound rejection.
- Run the focused editor tests and the complete Release solution test suite after implementation.

## Manual verification plan

- Hover a segment at multiple zoom levels and compare its numeric values; they should remain unchanged.
- Select and drag a point and verify that incoming/outgoing values update, including at the first and last points.
- Inspect zero-distance and invalid-timing examples.
- Toggle segment coloring, read the legend, and verify colors remain stable while zooming and scrolling.
- Check that the readout and legend do not cover important timeline content or interfere with editing.

## Results and limitations

No tests or manual checks have been run. Values are intended to describe the script's segment-average motion at 1x in percentage-points-per-second. Actual device movement can differ because of playback rate, stroke range, interpolation, and device behavior.
