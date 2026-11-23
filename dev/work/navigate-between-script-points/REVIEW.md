# Code Review: Navigate Between Script Points

**Status:** Approved

## Findings

No code findings.

## Positive coverage

- Previous and next navigation are strictly relative to the current playhead and do not wrap at boundaries.
- Selected-point navigation preserves selection and is a no-op when no action is selected.
- Successful navigation updates the playhead and raises one seek request while preserving zoom, script data, selection, and undo/redo history.
- Empty scripts, exact timestamps, fractional playhead positions, and first/last-point behavior are covered.
- Ctrl+Up, Ctrl+Down, and Ctrl+Enter are routed only while the timeline has focus, with exact modifier checks.
- Previous, Selected, and Next buttons share the timeline commands with their shortcuts and track availability.
- The unrelated media files reported in the first review are absent from the cleaned branch.

## Verification

- Focused `TimelineNavigationTests`: 8/8 passed.
- Full Release solution suite: 364 tests passed — 94 Common and 270 FunscriptEditor.
- Branch patch passes `git diff --check`.

Interactive visual and live-mpv smoke verification remains unperformed, as recorded in `VERIFICATION.md`.

## Follow-up review — 2026-11-22

**Status:** Resolved

### Findings

- **[P2] Frame-rounded paused seeks are treated as failures — resolved with a limitation.** Seek confirmation now uses half of mpv's detected frame duration (with the existing sub-millisecond tolerance as a floor). If verification reaches its deadline without confirmation, it logs the result and clears the pending seek so later paused position observations are no longer suppressed. If mpv provides no usable frame rate, confirmation falls back to 0.5 ms; a frame-rounded seek may still be retried and remain pending until the 2.5-second timeout.

### Verification

- Focused `TimelineNavigationTests` and `MpvSessionReplacementTests`: 15/15 passed.
- Focused `MpvSessionTests`: 16/16 passed.
- No live-mpv or interactive UI smoke test was performed.
