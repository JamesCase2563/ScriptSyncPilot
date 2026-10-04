# Pattern Insertion Verification

**Status:** Acceptance checks drafted; implementation and behavior tests not run.

See [plan](PLAN.md) and [design](DESIGN.md). The accepted feature decisions define the checks below; engineering details should be recorded as implementation proceeds.

## Acceptance Criteria

- [ ] Enter observation count/time and see correct cycles per minute, explicitly distinguished from directional strokes.
- [ ] Select all three profiles, configure profile settings and starting phase, and retain the chosen output cadence/range.
- [ ] Fit an integer number of full cycles into an output timeframe; observation duration remains independent.
- [ ] Show observed/effective cadence when whole-cycle fitting changes the measured cadence.
- [ ] Default the configurable section limit to three hours and reject generation over 100,000 actions; test valid and invalid configuration without treating these as verified Handy upload limits.
- [ ] Remember the Merge collision choice across dialog uses.
- [ ] Offer Continue Existing and Pattern Start, with a visible fallback when continuation cannot be resolved.
- [ ] Undo/redo multiple generated and manual edits; a new committed edit clears redo.
- [ ] Preview without changing actions, selection, metadata, or dirty state.
- [ ] Apply at the captured timestamp; require no mpv/Handy connection.
- [ ] Ask Stop/Replace/Merge only on conflicts; otherwise apply without asking.
- [ ] Preserve outside-section actions and root JSON properties after edit/save.
- [ ] Refresh timeline extent/zoom/scroll limits and avoid stale selection/drag indices.
- [ ] Generate the complete requested section beyond the current timeline/script endpoint, including a new or partly authored script.
- [ ] Load an unrelated video shorter than the requested section and verify insertion is not rejected or truncated by its duration.

## Generation Tests

- [ ] 12 observed cycles in 30 seconds produces 24 cycles/minute and 2500 ms periods.
- [ ] Measurement intervals of a few seconds and several minutes use the same units/formula.
- [ ] Full-cycle mode ends at the calculated boundary; extrema hit the requested position range.
- [ ] Triangle timing is symmetric; fast-up defaults to 25% rise / 75% return; cosine-eased samples slow near reversals.
- [ ] Adaptive sampling meets the one-position-unit deviation target after timestamp/position rounding and respects the action cap.
- [ ] Fractional periods over many cycles do not accumulate rounding drift; timestamps remain ordered and unique.
- [ ] Shared cycle boundaries are emitted once, including nonzero insertion timestamps.
- [ ] Five cycles in 12 seconds gives 2400 ms per cycle and ends exactly at 12 seconds, at the selected starting phase; never emit a partial final cycle.
- [ ] A timeframe that would imply fractional cycles at measured cadence uses a user-confirmed whole-cycle count and exposes the effective cadence rather than silently clipping.
- [ ] Automatically suggest the nearest positive whole-cycle count, minimum one; direct override works without rounding-policy controls.
- [ ] Continue Existing matches interpolated value and compatible direction/phase when available; out-of-range/no-value cases fall back to the profile's natural start.
- [ ] Phase/settings changes modify the profile while preserving the chosen cycle count and timeframe.
- [ ] Reject zero/negative/nonfinite inputs, invalid position ranges, timestamp overflow, excessive output size, and sub-millisecond point collisions without mutation.

## Conflict and Persistence Tests

- [ ] Empty script and conflict-free target apply with no prompt.
- [ ] Existing interior values trigger the conflict prompt; existing boundary-only points are preserved without a prompt.
- [ ] In both Replace and Merge, preserve existing start/end actions and omit competing generated endpoints; emit endpoints where no existing boundary action is present.
- [ ] With existing points at 0 and 100 seconds, insert from 1 to 20 seconds without a conflict prompt; crossing interpolation alone is not a conflict.
- [ ] Stop/dialog cancellation leave the script and dirty state unchanged.
- [ ] Replace changes only the agreed target interval, retaining exact outside timestamps/positions.
- [ ] Merge supports both Keep Existing and Use New for different positions at identical timestamps; identical pairs deduplicate and no duplicate timestamps remain.
- [ ] Any changed document invalidates or refreshes preview/conflict results before Apply.
- [ ] Save/reload retains lowercase actions, edited values, and metadata/version/range/inverted/custom root properties.
- [ ] Undo/redo a sequence containing pattern insertion, manual insertion, deletion, and dragging in correct order without discarding metadata.
- [ ] One completed drag or pattern insertion is one history entry, not many intermediate entries.
- [ ] A new committed edit after undo clears redo; preview/cancel/Stop do not.
- [ ] Saving records a checkpoint; undo/redo update dirty state accurately, and opening/creating another document does not reuse old history.
- [ ] Small edits store deltas and bulk edits retain affected sections, not full-document snapshots per operation; measure memory across repeated large insertions and undo/redo without a restrictive edit-count cap.

## Manual Editor Checks

- [ ] Generate into a new document and a loaded script, with and without mpv attached.
- [ ] Move the video playhead while previewing; the captured insertion time stays explicit and stable.
- [ ] Try each conflict choice and verify visible results match preview.
- [ ] Verify preview exposes the actual edge transitions, including preserved boundaries that create ramps or alter the ideal first/last cycle.
- [ ] Insert beyond both timeline and media endpoints; verify Fit All and high-zoom scrolling reach the complete generated section while the original media duration remains unchanged.
- [ ] Repeated Apply/Cancel operations do not leave stale selection, drag state, or duplicate actions.
- [ ] Controls fit at ordinary/resized Windows window sizes and supported DPI scales; invalid input gives actionable feedback.

## Required Gates

Run focused generator/application tests and build the affected projects before declaring completion. If Common changes, include shared persistence regression checks and build both consumers. Record commands, results, and any manual checks actually performed; a successful build alone does not verify profile timing or overwrite behavior.

## Results and Limitations

### Persistence Baseline (2026-10-04)

Added [ScriptSyncPilot.Common.Tests](../../../tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj) to the solution using MSTest with the VSTest adapter and .NET 9. Ran:

```powershell
dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --nologo
```

Result: 11 passed, 0 failed, 0 skipped. Covered existing action edits, ordered load, empty documents, invalid JSON, lowercase action fields, unknown root-property preservation, and CSV output. This verifies current Common behavior, not pattern application or undo/redo.

No feature verification results yet. Generator/application tests, manual checks, limitations, and PR reference will be recorded during implementation. Do not mark the feature acceptance criteria above as passed based on these baseline tests.
