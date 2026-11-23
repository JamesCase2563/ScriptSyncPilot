# Movement Speed Indicators — Verification

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Status:** In Progress; implementation and verification are pending.

## Acceptance criteria

- [ ] Hovering a segment shows its elapsed time in milliseconds, absolute position distance in percentage points, and average speed in percentage points per second.
- [ ] A selected or dragged point shows both incoming and outgoing segment speeds; values update as the point is edited.
- [ ] Segment hover does not capture point hit targets or interfere with dragging; selected/dragged point values take precedence over the hovered-segment readout.
- [ ] A segment with positive duration and zero position change reports zero speed.
- [ ] Duplicate or non-increasing timestamps are explicitly marked as invalid timing and never receive a finite speed value.
- [ ] Coloring uses the single vivid, ordered multi-hue palette and has Fit to script as the default; fit uses all valid segments and P99 nearest-rank rounded up to the next 100 pp/s, with a 100 pp/s minimum upper bound.
- [ ] Manual range min/max preferences persist globally for cross-script comparisons; Fit to script runs on load or explicit invocation and remains stable during point dragging.
- [ ] Speeds above the configured color maximum use the endpoint color without being removed or changing the actual numeric readout, which notes that the color is capped.
- [ ] Within a script, zooming and scrolling do not change its fitted range or a segment's color; manual-range colors remain comparable across scripts.
- [ ] The legend and numeric readout remain usable in Windows high-contrast mode.
- [ ] Numerical values remain available when coloring is enabled; colors are not the only way to distinguish speed.
- [ ] The UI describes these as script-space segment-average speeds at normal (1x) playback and makes no safety or physical-device-speed claim.
- [ ] Existing point selection, dragging, and timeline navigation continue to work.

## Automated verification plan

- Test the pure speed calculation for increasing and decreasing positions, positive durations, zero-distance segments, and both invalid timestamp cases.
- Test that selected/dragged point readouts use the adjacent actions and refresh as the edited action changes.
- Test point-versus-segment hover precedence and that dragging updates readout values from the in-progress action.
- Test selected range modes, boundary values, endpoint clamping, and an unchanged mapped color for the same segment speed at different zoom and viewport settings.
- Test fit-to-script uses the full script, not the currently visible time range; test manual preference save/load if persistence is selected.
- Run the focused editor tests and the complete Release solution test suite after implementation.

## Manual verification plan

- Hover a segment at multiple zoom levels and compare its numeric values; they should remain unchanged.
- Select and drag a point and verify that incoming/outgoing values update, including at the first and last points.
- Inspect zero-distance and invalid-timing examples.
- Toggle segment coloring, read the legend, and verify colors remain stable while zooming and scrolling.
- Check that the readout and legend do not cover important timeline content or interfere with editing.

## Results and limitations

No tests or manual checks have been run. Values are intended to describe the script's segment-average motion at 1x in percentage-points-per-second. Actual device movement can differ because of playback rate, stroke range, interpolation, and device behavior.
