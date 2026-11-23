# Movement Speed Indicators

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Affected project:** `ScriptSyncPilot.FunscriptEditor`  
**Status:** In Progress; implementation has not started.<br>
**Priority:** Not set in the issue.  
**Design decisions:** [DESIGN.md](DESIGN.md)

## Goal

Make the average speed represented by funscript segments clear while editing, regardless of timeline zoom. The segment slope on screen is not a speed indicator because zoom changes its apparent angle.

## Scope

- Show a numerical readout when hovering a segment and while dragging a point. Report elapsed time in milliseconds, absolute position change in percentage points, and average speed in percentage points per second.
- For a selected or dragged point, show the speed of both its incoming and outgoing segments and refresh the values as the edit changes.
- Add optional segment coloring on a scale independent of zoom and the visible time range. Use the decided vivid, ordered multi-hue palette, include a legend, and keep the numeric readout accurate even when colors saturate at the configured maximum.
- Color the connecting segments first; add a narrow, time-aligned color strip below the waveform only if line coloring is not clear enough at practical zoom levels.
- Treat duplicate or decreasing timestamps as invalid timing; do not calculate a finite speed for those segments.
- Describe the values as script-space segment averages at normal (1x) playback. Do not characterize script values as safe/unsafe or imply that they measure physical device speed.

## Out of scope

- Measuring actual movement speed from a device or media playback.
- Device-limit detection or alerting; deferred from issue #34. Users may choose a manual script-speed color range, but the editor does not validate it against a device.
- Accounting for playback-rate changes, configured stroke range, interpolation, or device behavior in the script-space calculation.
- A derived speed-graph track. That remains a possible follow-up under [Multiple Tracks and Project Format](../../FunscriptEditor/BACKLOG.md#multiple-tracks-and-project-format).

## Decisions to settle before implementation

- Color and legend treatment for Windows high-contrast mode; color must remain supplemental to the numeric readout.

## Timing invariants

Actions in a loaded `Funscript` are sorted by timestamp. Loading sorts the input but retains duplicate timestamps, so decreasing input order is normalized while duplicate-time segments can still reach the feature. Atomic action-list replacement rejects duplicate or decreasing timestamps. The speed calculation must nevertheless validate each adjacent pair itself: a non-increasing pair is invalid and has no finite speed. Cover decreasing timestamps directly at the pure calculator boundary, since normal loaded scripts sort them before calculation.

## Milestones

### M1 — Confirm interaction and presentation

- [x] M1.1 Inspect timeline hit-testing, selection, point dragging, and current status/help surfaces; confirm point targets have priority over segment hover and identify conflicts for readouts.
- [x] M1.2 Resolve exact placement and context precedence for the compact main-form readout, numeric precision, in-progress drag values, invalid/zero-distance presentation, and preferences dialog behavior for manual limits, fit-to-script, and capped colors.
- [x] M1.3 Decide and document the scale modes/default, P99 fitted upper bound rounded to the next 100 pp/s, fit timing, color-only clamping, and single vivid, ordered multi-hue palette. Capped values retain their actual numeric readout.

#### M1.1 inspection findings — 2026-11-23

- [`TimelineControl.HitTest`](../../../src/ScriptSyncPilot.FunscriptEditor/TimelineControl.cs) checks action points within an 8-pixel radius. On left mouse-down the point hit test runs before empty-space scrubbing; a hit selects and starts dragging that point. Double-click insertion also rejects point hits. Segment hover is not implemented yet.
- Preserve that precedence by checking point targets before segment hover and suppressing segment hover during point drag, scrubbing, panning, or value-bar interaction. Hover must not change selection or capture a drag.
- The point-navigation and seek rows already display keyboard hints. The transport row's `_timeLabel` is dedicated to current video time/duration, and the bottom `_log` is an append-only diagnostic log. Neither is a dedicated transient segment-readout surface. M1.2 fixed the compact readout placement and behavior (see presentation decisions below).
- Existing value-bar hover uses the playhead timestamp and maps pointer Y to the candidate position; clicking inserts/replaces at that time. The new readout can preview duration/distance/speed for the candidate without editing or changing selection.
- Point hover must show the point under the pointer even if another point remains selected. During drag, readouts use in-progress values. For overlapping point hit regions, existing hit testing returns the first action in timestamp order.
- Segment lines are the first color surface. If the lines prove hard to distinguish at practical zoom, use a narrow strip aligned to timeline time and reusing the same segment colors; this is a fallback, not a separate speed graph.

#### M1.2 presentation decisions — 2026-11-23

- Place a compact, persistent readout row directly below the timeline and above the seek and point-navigation controls. Keep it separate from the transport time label, so transient segment context does not overwrite playback time. Include a concise label identifying the values as script-space average speed at 1x.
- Context precedence is: (1) active point drag, (2) value-bar insertion/replacement preview under the pointer, (3) point under the pointer, (4) segment under the pointer, (5) selected point when the pointer is over no other meaningful target. If none applies, clear the readout. Hover and preview never change selection or the script; during pan/scrub suppress hover contexts and retain only selected-point fallback, if any.
- A point context displays incoming and outgoing rows. A segment context displays that segment. Format duration as whole milliseconds, absolute distance as whole percentage points (`pp`), and speed to one decimal percentage point per second (`pp/s`). Use invariant numeric formatting and include units in the readout.
- During point drag, calculate from the current in-progress action list and refresh both adjacent rows on each edit. At either endpoint show `No incoming segment` or `No outgoing segment`. For positive duration and no position change show `0.0 pp/s`. For duplicate/non-increasing timestamps show `Invalid timing (0 ms)` or `Invalid timing (negative duration)` and omit speed; do not assign a speed color. If a computed speed is outside the selected color range, retain its actual numeric value and append `color capped` (or `color capped low`) to that row.
- The Preferences dialog includes a segment-coloring toggle (on by default), a scale-mode choice (`Fit to script` or `Manual`), manual minimum/maximum pp/s fields, and a `Fit to current script` command. Manual limits persist application-wide and apply across scripts. Entering Fit mode or invoking Fit recalculates against all valid segments in the current script; Fit mode also recalculates when another script is loaded, but not continuously while editing/dragging. Manual mode retains its entered bounds across script loads. Validate `minimum < maximum`; reject invalid values with an inline/error message and keep the last valid range. Values above/below the range clamp to endpoint colors only, with the numeric readout indicating the cap.
- Fit is unavailable when there is no loaded script; retain the last valid fitted range/legend until a script is loaded, while keeping segment coloring inactive without segment data. The coloring toggle, mode, and manual bounds are saved through the existing `EditorSettings` application settings file.
- Preserve the selected palette and legend decisions in DESIGN. In Windows high contrast, use system-derived contrasting line/legend treatment and keep labels and numeric values visible; color remains supplemental.

### M2 — Calculate script-space segment speeds

- [x] M2.1 Add a pure calculation for elapsed milliseconds, absolute position distance, and average percentage-points-per-second.
- [x] M2.2 Validate adjacent timestamps in the calculation itself; represent duplicate/decreasing times as invalid, and positive-duration zero-distance segments as zero speed.
- [x] M2.3 Add focused automated coverage for normal, rising, falling, zero-distance, duplicate-time, and synthetic decreasing-time cases.

### M3 — Show numerical movement details

- [ ] M3.1 Show segment values on hover, including duration, absolute distance, and average speed, without stealing point hit targets.
- [ ] M3.2 Show incoming/outgoing values for the dragged or hovered point using in-progress values; use the selected point as readout fallback and handle missing endpoints.
- [ ] M3.3 Keep the readout legible and non-obstructive while preserving existing selection, drag, and zoom behavior.

### M4 — Add optional speed coloring

- [ ] M4.1 Implement the selected manual and/or whole-script-fit modes; derive fit values from all valid segments, not the viewport, and clamp speeds over the color maximum without altering reported speeds.
- [ ] M4.2 Add the preferences controls and legend using the decided vivid, ordered multi-hue palette; ensure settings and readout remain usable in high-contrast mode.
- [ ] M4.3 Keep numeric values available and avoid safe/unsafe claims.

### M5 — Verify and document

- [ ] M5.1 Run focused calculation and timeline tests, including an assertion that a segment's mapped color is unchanged across different zoom and viewport settings; then run the relevant editor test suite.
- [ ] M5.2 Manually verify hover, selection, drag updates, invalid timing, color toggle, legend, and zoom independence.
- [ ] M5.3 Update the permanent editor architecture documentation and record remaining limitations here and in `VERIFICATION.md`.

#### M2.1 implementation — 2026-11-23

- Added [`SegmentSpeedCalculator`](../../../src/ScriptSyncPilot.FunscriptEditor/SegmentSpeedCalculator.cs), a pure calculation over two `FunscriptAction` values. It returns signed elapsed milliseconds, absolute position distance in percentage points, and nullable average pp/s (`distance * 1000 / elapsed`). Non-positive elapsed time yields no speed; M2.3 covers these cases with focused automated tests.

#### M2.2 implementation — 2026-11-23

- The calculation now returns an explicit timing status: `Valid`, `DuplicateTimestamp`, or `DecreasingTimestamp`. Both invalid cases retain signed elapsed milliseconds and absolute distance but have no speed; a positive-duration zero-distance pair has valid timing and speed `0`.

#### M2.3 automated coverage — 2026-11-23

- Added [`SegmentSpeedCalculatorTests`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/SegmentSpeedCalculatorTests.cs) for rising and falling position changes, duration/distance/speed values, zero-distance movement, duplicate timestamps, and a synthetic decreasing-time pair at the calculator boundary.

## Review feedback incorporated — 2026-11-23

- Documented load sorting, duplicate preservation, atomic replacement validation, and calculator-level rejection of non-increasing adjacent timestamps.
- Added explicit hover precedence and in-progress drag values to M1 and M3.
- Made fixed thresholds, labels, endpoint-color clamping, and high-contrast usability part of the scale design and implementation milestones.
- Added direct zoom/viewport independence coverage for the segment color mapping to M5.
