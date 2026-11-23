# Movement Speed Indicators

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Affected project:** `ScriptSyncPilot.FunscriptEditor`  
**Status:** Proposed; implementation has not started.  
**Priority:** Not set in the issue.  
**Design decisions:** [DESIGN.md](DESIGN.md)

## Goal

Make the average speed represented by funscript segments clear while editing, regardless of timeline zoom. The segment slope on screen is not a speed indicator because zoom changes its apparent angle.

## Scope

- Show a numerical readout when hovering a segment and while dragging a point. Report elapsed time in milliseconds, absolute position change in percentage points, and average speed in percentage points per second.
- For a selected or dragged point, show the speed of both its incoming and outgoing segments and refresh the values as the edit changes.
- Add optional segment coloring on a fixed speed scale, independent of zoom and the visible time range. Include a legend and keep the numerical readout available so color is not the sole indication.
- Treat duplicate or decreasing timestamps as invalid timing; do not calculate a finite speed for those segments.
- Describe the values as script-space segment averages at normal (1x) playback. Do not characterize script values as safe/unsafe or imply that they measure physical device speed.

## Out of scope

- Measuring actual movement speed from a device or media playback.
- Accounting for playback-rate changes, configured stroke range, interpolation, or device behavior in the script-space calculation.
- A derived speed-graph track. That remains a possible follow-up under [Multiple Tracks and Project Format](../../FunscriptEditor/BACKLOG.md#multiple-tracks-and-project-format).

## Decisions to settle before implementation

- Where the readout and color legend fit in the existing editor without obscuring the timeline or point edits.
- Hover precedence: point hit targets retain priority over segment hover; the selected/dragged point's incoming/outgoing readout takes priority over a hovered-segment readout. During a drag, calculate from the in-progress action values.
- Whether segment coloring is off by default and how users enable it; whether that preference should persist.
- Fixed speed-scale boundaries and labels, the color assigned below/above the scale (clamp to endpoint colors), and displayed numeric precision.
- Color and legend treatment for Windows high-contrast mode; color must remain supplemental to the numeric readout.
- The exact wording and visual treatment for invalid timing and zero-distance segments.

## Timing invariants

Actions in a loaded `Funscript` are sorted by timestamp. Loading sorts the input but retains duplicate timestamps, so decreasing input order is normalized while duplicate-time segments can still reach the feature. Atomic action-list replacement rejects duplicate or decreasing timestamps. The speed calculation must nevertheless validate each adjacent pair itself: a non-increasing pair is invalid and has no finite speed. Cover decreasing timestamps directly at the pure calculator boundary, since normal loaded scripts sort them before calculation.

## Milestones

### M1 — Confirm interaction and presentation

- [ ] M1.1 Inspect timeline hit-testing, selection, point dragging, and current status/help surfaces; confirm point targets have priority over segment hover and identify conflicts for readouts.
- [ ] M1.2 Resolve readout placement and precedence, numeric precision, in-progress drag values, invalid/zero-distance presentation, and the optional color setting.
- [ ] M1.3 Choose and document fixed speed thresholds and labels, endpoint-color clamping, legend presentation, and high-contrast behavior.

### M2 — Calculate script-space segment speeds

- [ ] M2.1 Add a pure calculation for elapsed milliseconds, absolute position distance, and average percentage-points-per-second.
- [ ] M2.2 Validate adjacent timestamps in the calculation itself; represent duplicate/decreasing times as invalid, and positive-duration zero-distance segments as zero speed.
- [ ] M2.3 Add focused automated coverage for normal, rising, falling, zero-distance, duplicate-time, and synthetic decreasing-time cases.

### M3 — Show numerical movement details

- [ ] M3.1 Show segment values on hover, including duration, absolute distance, and average speed, without stealing point hit targets.
- [ ] M3.2 Give selected/dragged point readouts precedence over segment hover; show incoming and outgoing values, refresh from in-progress drag values, and handle missing endpoints.
- [ ] M3.3 Keep the readout legible and non-obstructive while preserving existing selection, drag, and zoom behavior.

### M4 — Add optional speed coloring

- [ ] M4.1 Implement a deterministic speed-to-color mapping using the fixed scale, labels, and endpoint-color clamping; keep the mapping independent of zoom and visible range.
- [ ] M4.2 Add a legend and an accessible enable/disable control consistent with the M1 decision; ensure the legend and readout remain usable in high-contrast mode.
- [ ] M4.3 Keep numeric values available and avoid safe/unsafe claims.

### M5 — Verify and document

- [ ] M5.1 Run focused calculation and timeline tests, including an assertion that a segment's mapped color is unchanged across different zoom and viewport settings; then run the relevant editor test suite.
- [ ] M5.2 Manually verify hover, selection, drag updates, invalid timing, color toggle, legend, and zoom independence.
- [ ] M5.3 Update the permanent editor architecture documentation and record remaining limitations here and in `VERIFICATION.md`.

## Review feedback incorporated — 2026-11-23

- Documented load sorting, duplicate preservation, atomic replacement validation, and calculator-level rejection of non-increasing adjacent timestamps.
- Added explicit hover precedence and in-progress drag values to M1 and M3.
- Made fixed thresholds, labels, endpoint-color clamping, and high-contrast usability part of the scale design and implementation milestones.
- Added direct zoom/viewport independence coverage for the segment color mapping to M5.
