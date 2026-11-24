# Movement Speed Indicators — Verification

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Status:** In Progress; implementation and verification are pending.

## Acceptance criteria

- [ ] A compact persistent readout directly below the timeline and above seek/point-navigation controls identifies values as script-space average speed at 1x; playback time remains in the transport row.
- [ ] Switching among segment, point, preview, and drag readout contexts does not change the fixed readout height or resize the timeline; duration, distance, and speed stay aligned in their columns.
- [ ] Hovering a segment shows whole-millisecond duration, whole-pp absolute distance, and speed to one decimal pp/s with units.
- [ ] A selected or dragged point shows both incoming and outgoing segment values; values update from the in-progress action list as the point is edited.
- [ ] Segment hover does not capture point hit targets or interfere with dragging; readout context follows the precedence confirmed in M1.2 without changing selection.
- [ ] The M1.2 readout precedence is verified: active drag, value-bar preview at playhead time and hovered position, hovered point, hovered segment, selected-point fallback, then cleared readout. Pan/scrub suppress hover contexts.
- [ ] Value-bar preview shows the incoming/outgoing values that would result from insertion or replacement, without changing the script until clicked.
- [ ] A segment with positive duration and zero position change reports `0.0 pp/s`; missing endpoint neighbors are labeled explicitly.
- [ ] Duplicate or non-increasing timestamps show `Invalid timing` with zero or negative duration, omit speed, and never receive a speed color.
- [ ] Coloring uses the single Jet-style rainbow palette and has Fit to script as the default; fit uses all valid segments and P99 nearest-rank rounded up to the next 100 pp/s, with a 100 pp/s minimum upper bound.
- [ ] Preferences expose an on-by-default coloring toggle, Fit/Manual scale choice, manual min/max pp/s, and Fit to current script. Toggle, mode, and manual limits persist through `EditorSettings`.
- [ ] Manual range min/max preferences persist globally for cross-script comparisons; invalid `min >= max` is rejected without replacing the last valid range. Fit to script uses all valid segments, recalculates on script load or explicit invocation, and remains stable during edits/dragging.
- [ ] Speeds above/below the configured color range use endpoint colors without being removed or changing the actual numeric readout, which marks the color as capped high/low.
- [ ] Color positions use the square root of the normalized, clamped speed; the legend gradient uses the same transform while numeric readouts remain actual pp/s.
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
- Run focused Common and editor tests, plus the HandySync test suite and complete Release solution test suite after implementation.

## Manual verification plan

- Hover a segment at multiple zoom levels and compare its numeric values; they should remain unchanged.
- Select and drag a point and verify that incoming/outgoing values update, including at the first and last points.
- Inspect zero-distance and invalid-timing examples.
- Toggle segment coloring, read the legend, and verify colors remain stable while zooming and scrolling.
- Check that the readout and legend do not cover important timeline content or interfere with editing.
- In HandySync, inspect pixel columns containing multiple segments and confirm each uses the maximum valid overlapping segment speed, including invalid and empty intervals.
- Set the same manual range in each app and confirm the same speed maps to the same palette color; verify each app persists its own settings independently.
- Check the timeline and HandySync overview in Windows high-contrast mode.

## Results and limitations

The focused calculator tests passed (5 passed, 0 failed, 2026-11-23). The full solution build passed with 0 warnings and 0 errors after the M5.3 HandySync integration (2026-11-24), including Common, both applications, and their test projects. M6.1 verification on 2026-11-24 passed: focused Common calculation/scale tests (3 passed), HandySync column aggregation and cross-app mapping parity tests (4 passed), and the complete editor/HandySync test project (284 passed, 0 failed). The full editor suite exposed and verified a footer layout correction placing the point-navigation row below the seek row. Focused Common verification also caught and fixed swapped RGB channel interpolation in the shared Jet palette. The user reports manually checking editor hover, selection, dragging, invalid timing, color toggle, legend, zoom independence, HandySync maximum-speed columns, and high-contrast behavior in both apps; all work as expected. The user also visually reviewed the fixed-column readout after relaunch and confirmed it looks good. Values describe the script's segment-average motion at 1x in percentage-points-per-second; actual device movement can differ because of playback rate, stroke range, interpolation, and device behavior.

### M6.3 architecture and limitations

- Added permanent descriptions to [`dev/ARCHITECTURE.md`](../../ARCHITECTURE.md), [`dev/Common/ARCHITECTURE.md`](../../Common/ARCHITECTURE.md), [`dev/FunscriptEditor/ARCHITECTURE.md`](../../FunscriptEditor/ARCHITECTURE.md), and [`dev/HandySync/ARCHITECTURE.md`](../../HandySync/ARCHITECTURE.md). Common owns pure segment measurement, Fit/Manual range fitting, square-root mapping, clamp metadata, and Jet palette RGB values. Each app owns settings persistence, rendering, and high-contrast adaptation.
- The Common model tests, HandySync column aggregation tests, cross-app mapping parity test, and full editor/HandySync suite passed as recorded above. Editor build after fixed-column readout change succeeded with 0 warnings and 0 errors. `git diff --check` passed.
- HandySync summarizes each waveform column with the **maximum valid speed among overlapping action segments**. It preserves the fastest movement in compressed columns, but does not represent the distribution or make slower overlapping segments individually visible. Invalid segments contribute no speed; columns without valid segments use the base waveform color. The overview still covers the full movie/script duration without zoom or pan; a detailed segment inspection belongs in the editor until a separate HandySync zoom feature is implemented.
- Speed is calculated from adjacent script actions at 1x and is not a measurement of device motion. Fit-to-script intentionally limits the color maximum using P99 to reduce the influence of extreme authoring segments; speeds beyond the selected range keep their actual numeric value in the editor and use endpoint colors with clamp metadata.
- The new fixed-column readout compiled successfully and the user confirmed its normal-mode appearance after relaunch. High-contrast behavior was manually confirmed before this readout-only layout change; the revised layout uses standard labels/system colors, but a separate high-contrast review of the new layout has not been reported.

## M6 — Verification and documentation deliverables

- [x] For the same speed, color range, and enabled palette, Common returns the same normalized mapping and palette RGB value to both applications.
- [ ] Each app independently saves and restores the same speed-color settings structure; when both use the same range, the shared mapping produces identical colors.
- [x] HandySync's per-column speed aggregation is documented and tested for multiple overlapping segments, invalid timing, and empty columns.
- [ ] Both applications remain readable in Windows high-contrast mode while using the shared speed mapping.
- [x] Permanent architecture documentation describes the Common speed-color model, editor integration, and HandySync maximum-speed column aggregation; update this plan and verification results with measured outcomes and remaining limitations.
