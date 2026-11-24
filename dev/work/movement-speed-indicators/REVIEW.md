# Code Review: Movement Speed Indicators

**Branch:** `issue/34-movement-speed-indicators`  
**Compared with:** `origin/main`  
**Status:** Changes requested

## Findings

- **[P2] Readout hit-testing and preview construction scale with the full script** — `src/ScriptSyncPilot.FunscriptEditor/TimelineControl.cs:1058-1080,1098-1157`. Each mouse move over the plot scans all actions in `HitTest`, then—if no point is hit—`FindHoveredSegmentIndex` repeats that point scan and scans every segment, including off-screen segments. Each move over the value bar also copies the entire action list and linearly searches the copy to build a hypothetical preview. This can make hover/preview sluggish and create substantial allocation pressure for large scripts. Reuse the point-hit result, limit segment search to the visible time range, and compute preview measurements from the adjacent actions without cloning the full list.

- **[P3] Invalid timing can retain a stale screen-reader speed description** — `src/ScriptSyncPilot.FunscriptEditor/MainForm.cs:541-545`. The valid-speed path sets `speed.AccessibleDescription`, but the invalid-timing branch only changes `Text`. If the same label previously showed a valid segment, assistive technology may still receive that old speed description. Clear the accessible description when displaying invalid timing.

## Verification

- Full Release solution test run: **383 passed** — 99 Common and 284 FunscriptEditor; 0 failed or skipped.
- No interactive UI session was performed during this review.
- `git diff --check` reports trailing spaces in Markdown hard-break lines in `DESIGN.md`, `PLAN.md`, and `VERIFICATION.md`.
