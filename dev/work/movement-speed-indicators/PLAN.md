# Movement Speed Indicators

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Affected project:** `ScriptSyncPilot.FunscriptEditor`  
**Status:** Proposed; implementation has not started.  
**Priority:** Not set in the issue.

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
- Whether segment coloring is off by default and how users enable it; whether that preference should persist.
- The fixed speed-scale boundaries, color mapping, and displayed numeric precision.
- The exact wording and visual treatment for invalid timing and zero-distance segments.

## Milestones

### M1 — Confirm interaction and presentation

- [ ] M1.1 Inspect timeline hit-testing, selection, point dragging, and current status/help surfaces; identify conflicts for segment hover and readouts.
- [ ] M1.2 Resolve readout placement, numeric precision, invalid/zero-distance presentation, and the optional color setting.
- [ ] M1.3 Choose and document a fixed speed scale and legend that remain meaningful at every zoom and viewport.

### M2 — Calculate script-space segment speeds

- [ ] M2.1 Add a pure calculation for elapsed milliseconds, absolute position distance, and average percentage-points-per-second.
- [ ] M2.2 Represent duplicate or non-increasing timestamps as invalid, and positive-duration zero-distance segments as zero speed.
- [ ] M2.3 Add focused automated coverage for normal, rising, falling, zero-distance, duplicate-time, and decreasing-time cases.

### M3 — Show numerical movement details

- [ ] M3.1 Show segment values on hover, including duration, absolute distance, and average speed.
- [ ] M3.2 Show incoming and outgoing values for a selected or dragged point; refresh them during a drag and handle missing endpoints.
- [ ] M3.3 Keep the readout legible and non-obstructive while preserving existing selection, drag, and zoom behavior.

### M4 — Add optional speed coloring

- [ ] M4.1 Color segments using the fixed scale, independently of zoom and visible range.
- [ ] M4.2 Add a legend and an accessible enable/disable control consistent with the M1 decision.
- [ ] M4.3 Keep numeric values available and avoid safe/unsafe claims.

### M5 — Verify and document

- [ ] M5.1 Run focused calculation and timeline tests, then the relevant editor test suite.
- [ ] M5.2 Manually verify hover, selection, drag updates, invalid timing, color toggle, legend, and zoom independence.
- [ ] M5.3 Update the permanent editor architecture documentation and record remaining limitations here and in `VERIFICATION.md`.
