# Pattern Insertion Verification

**Status:** Generator, planner, atomic model-commit, and action-history tests implemented; editor workflow and undo/redo are wired. Manual UI verification remains pending.

See [plan](PLAN.md) and [design](DESIGN.md). The acceptance checks below include workflow behavior that is implemented but still requires manual UI verification.

## Acceptance Criteria

- [ ] Set output start/end and full-cycle count directly; show output cycles per minute, explicitly distinguishing cycles from directional strokes.
- [x] Select all three profiles, configure profile settings and starting phase, and retain the chosen output cadence/range.
- [x] Fit the selected integer number of full cycles exactly into the output interval and show its resulting cadence.
- [x] Default the configurable section limit to three hours and reject generation over 100,000 actions; test valid and invalid configuration without treating these as verified Handy upload limits.
- [x] Remember the last Stop/Replace/Merge resolution across dialog uses.
- [x] Remember the last Keep Existing/Use New collision choice across dialog uses.
- [x] Restore the last conflict and collision choices after restarting the application.
- [x] Offer Continue Existing and Pattern Start, with a visible fallback when continuation cannot be resolved.
- [x] Undo/redo pattern insertion and a subsequent manual edit; a new committed edit after undo clears redo.
- [x] Undoing a drag or deletion restores the expected actions and shows the saved clean state in the title.
- [x] Preview without changing actions, selection, metadata, or dirty state.
- [x] Apply at the captured timestamp; require no mpv/Handy connection.
- [x] Conflict-free intervals apply without a conflict prompt.
- [x] Conflicting intervals show Stop, Replace, and Merge; the last resolution is restored on later dialog uses.
- [x] Stop inserts generated points before the first existing interior point, preserves that point and the remaining script, and may end mid-cycle; Cancel leaves the script unchanged. Stop marks the document dirty, while Cancel preserves the clean state.
- [x] Preserve outside-section actions and root JSON properties after edit/save.
- [x] Refresh timeline extent/zoom/scroll limits after insertion, with and without video attached.
- [x] Select the first inserted point after pattern insertion instead of retaining the previous selection.
- [x] After a bulk pattern insertion, subsequent drag/delete and undo/redo use valid selection and drag indices.
- [x] Generate the complete requested section beyond the current timeline/script endpoint, including a new or partly authored script.
- [x] Load an unrelated video shorter than the requested section and verify insertion is not rejected or truncated by its duration.
- [x] Change every pattern control, close and reopen the dialog, then restart the app and verify all values are restored. Insert once and verify the dialog remains open with its values intact.

## Generation Tests

- [x] Five selected cycles in 12 seconds produces 25 cycles/minute and 2400 ms periods.
- [x] Full-cycle mode ends at the calculated boundary; extrema hit the requested position range.
- [x] Triangle timing is symmetric; fast-up defaults to 25% rise / 75% return; cosine-eased samples slow near reversals.
- [x] Adaptive sampling meets the one-position-unit deviation target after timestamp/position rounding and respects the action cap.
- [x] Fractional periods over many cycles do not accumulate rounding drift; timestamps remain ordered and unique.
- [x] Shared cycle boundaries are emitted once, including nonzero insertion timestamps.
- [x] Five cycles in 12 seconds gives 2400 ms per cycle and ends exactly at 12 seconds, at the selected starting phase; never emit a partial final cycle.
- [x] The selected full-cycle count is used directly for the requested output interval; no measured-cadence suggestion or hidden rounding is applied.
- [x] Continue Existing matches interpolated value and compatible direction/phase when available; out-of-range/no-value cases fall back to the profile's natural start.
- [x] Phase/settings changes modify the profile while preserving the chosen cycle count and timeframe.
- [x] Reject zero/negative/nonfinite inputs, invalid position ranges, timestamp overflow, excessive output size, and sub-millisecond point collisions without mutation.

## Deterministic Numeric Cases

These cases specify expected results. Core generation/rounding/continuation/analytic error checks now have executable tests; application-specific checks remain pending. See results below.

- [x] Five cycles in 12 seconds reports 25 cycles/minute; changing the direct count to four reports 20 cycles/minute.
- [ ] Captured start 1.2345 seconds becomes 1235 ms; duration 0.0005 seconds becomes 1 ms; duration 0.0004 seconds is rejected. Integer positions 50.5 and 51.5 become 51 and 52.
- [x] For a triangle starting at 1000 ms, spanning 1000 ms with three cycles over 0-100, mandatory timestamp/position pairs are `(1000,0), (1167,100), (1333,0), (1500,100), (1667,0), (1833,100), (2000,0)`. The end is exact and shared boundaries appear once.
- [x] Distinct mandatory triangle extrema/boundaries colliding after rounding are rejected (for example two full cycles in 1 ms); increasing point density never silently removes a cycle.
- [x] Checked endpoint overflow and a total duration rounding to zero fail before mutation.
- [x] With actions `(0,0), (1000,100)`, continuation at 500 ms uses value 50 and ascending phase 0.25 for a triangle. With `(0,100), (1000,0)`, it uses descending phase 0.75.
- [x] At a peak `(0,0), (1000,100), (2000,0)`, continuation uses the peak phase 0.5 and its valid outgoing descending direction. At flat interior values with no direction, choose the ascending branch.
- [x] Continue Existing before/after the known action range or outside the selected movement range visibly falls back, without extrapolation or clamping to manufacture a matching phase.
- [x] For the cosine profile from 0 to 100 over one second, an extrema-only candidate has zero midpoint error on a half-cycle but excessive error elsewhere; the full-interval check detects it and refines.
- [x] Independently check the generated saved polyline's endpoints, knots, and all roots of `F'(t) = L'(t)`; maximum error is no more than `1 + 1e-9` position units after timestamp/position rounding, including phase-shifted profiles.
- [ ] Refinement uses midpoint-away-from-zero splits, strict interior integer milliseconds, and left-before-right processing; repeated identical inputs produce identical actions on the supported runtime.
- [x] Impossible one-millisecond refinement, mandatory-point error above tolerance, and output above the action cap fail rather than silently loosening accuracy. Count points before destination boundary suppression.
- [ ] Preserved boundary values and Merge results are previewed as resolved output, without falsely claiming the generator's one-unit error bound still applies.

## Conflict and Persistence Tests

- [x] Empty script and conflict-free target apply with no prompt.
- [x] Existing interior values trigger the conflict prompt; existing boundary-only points are preserved without a prompt.
- [x] In both Replace and Merge, preserve existing start/end actions and omit competing generated endpoints; emit endpoints where no existing boundary action is present.
- [x] With existing points at 0 and 100 seconds, insert from 1 to 20 seconds without a conflict prompt; crossing interpolation alone is not a conflict.
- [x] Canceling the conflict dialog leaves the script and dirty state unchanged.
- [x] Stop inserts the generated prefix before the first interior point without replacing it or later actions, and marks the document dirty.
- [x] Replace changes only the agreed target interval, retaining exact outside timestamps/positions.
- [x] Merge supports both Keep Existing and Use New for different positions at identical timestamps; identical pairs deduplicate and no duplicate timestamps remain.
- [x] Any changed document invalidates or refreshes preview/conflict results before Apply.
- [x] Save/reload retains lowercase actions, edited values, and metadata/version/range/inverted/custom root properties.
- [x] Undo/redo a sequence containing pattern insertion, manual insertion, deletion, and dragging in correct order without discarding metadata.
- [ ] One completed drag or pattern insertion is one history entry, not many intermediate entries.
- [ ] A new committed edit after undo clears redo; preview/Cancel do not, while a Stop prefix is treated as a committed edit.
- [ ] Saving records a checkpoint; undo/redo update dirty state accurately, and opening/creating another document does not reuse old history.
- [ ] Small edits store deltas and bulk edits retain affected sections, not full-document snapshots per operation; measure memory across repeated large insertions and undo/redo without a restrictive edit-count cap.

## Feature Test Case Matrix

**Status:** Generator coverage implemented in PatternGeneratorTests; conflict-analysis/candidate portions of C01-C09 in PatternInsertionPlanTests. A02/A03 and the model-level parts of A05-A10 are covered by FunscriptTests/ActionHistoryTests; C10 dialog preference persistence and editor-level A cases remain pending. IDs identify verification scenarios, not a claim that the complete insertion feature passes. Use `(atMs,pos)` notation, Pattern Start, range 0-100, and valid default limits unless overridden. Expected arrays below describe complete results, including existing boundary preservation.

### Generation and Rounding

| ID | Input | Expected result |
| --- | --- | --- |
| G01 | Triangle, start 1000 ms, one cycle in 2000 ms | Exactly `(1000,0), (2000,100), (3000,0)`; effective cadence 30 cycles/minute. |
| G02 | Fast-up/slow-down, same timing, default 25% rise | Exactly `(1000,0), (1500,100), (3000,0)`; cadence unchanged from G01. |
| G03 | Triangle, range 20-80, two cycles in 4000 ms from 0 | Exactly `(0,20), (1000,80), (2000,20), (3000,80), (4000,20)`; shared boundary at 2000 appears once. |
| G04 | Triangle, three cycles in 1000 ms from 1000 | Exact seven-point fixture in Deterministic Numeric Cases; final timestamp 2000 ms, no accumulated rounded-period drift. |
| G05 | Five selected cycles in a 12-second output interval, then change the count to four | Cadence is 25 cycles/minute (2400 ms period), then 20 cycles/minute (3000 ms period); selected counts are honored exactly. |
| G06 | One selected cycle in a 5-second output interval, then 7 seconds | Cadence follows the interval directly: 12 cycles/minute, then approximately 8.57 cycles/minute. |
| G07 | Start 1.2345 seconds; output 2.0005 seconds, one triangle cycle | Quantized start 1235, duration 2001; points `(1235,0), (2236,100), (3236,0)`. Half-period rounds upward; authoritative endpoint remains exact. |
| G08 | Cosine, one cycle in 1000 ms, range 0-100 | Mandatory min/max/end present, additional samples generated, final saved polyline satisfies the continuous one-unit error bound; independent oracle evaluates error extrema. |
| G09 | Continue Existing, ascending/descending/flat/peak fixtures | Match the phase/value/direction cases in Deterministic Numeric Cases; repeat inputs yield identical output. No extrapolation or manufactured in-range value on fallback. |
| G10 | One triangle cycle over 3 hours, then over 3 hours + 1 ms | Default duration limit accepts the first and rejects the second. Raising the configured limit permits the second if all other bounds hold. No media-duration check applies. |
| G11 | Triangle, range 0-100, 49,999 cycles in 10,000,000 ms; then 50,000 cycles | First has 99,999 mandatory actions and fits the cap; second requires 100,001 and is rejected before application. Cap counts generated points before boundary suppression. The longer timeframe isolates size validation from the independent precision limit. |
| G12 | Zero/negative counts/time, fractional output count, nonfinite input, invalid range, overflow, quantization collisions | Each invalid input produces a validation failure and no candidate application. Test each failure separately; never silently clamp unsafe counts or relax tolerance. |
| G13 | Empty/new script, triangle from 70,000 to 72,000 ms, loaded video ends at 10,000 ms | Full candidate ends at 72,000; generation is identical with no video. Applying expands timeline reach without changing the media duration. |

For G08 use an independent mathematical error oracle, not the generator's own acceptance function. Check both unshifted and continued/phase-shifted curves after integer rounding. Do not require one exact adaptive sample count unless the deterministic refinement contract dictates it.

### Conflict Fixtures

Use generated candidate `P = [(1000,0), (2000,100), (3000,0)]` and section `[1000,3000]`.

Use populated destination:

```text
S = [(0,10), (1000,20), (1500,30), (2000,40),
	(2500,50), (3000,60), (4000,70)]
```

| ID | Input/choice | Expected result |
| --- | --- | --- |
| C01 | Empty destination, apply P | No conflict prompt; exact result P. |
| C02 | Destination `[(0,10), (4000,70)]`, apply P | No prompt despite crossing interpolation; result `[(0,10), (1000,0), (2000,100), (3000,0), (4000,70)]`. |
| C03 | Destination `[(0,10), (1000,20), (3000,60), (4000,70)]`, apply P | Boundary-only values do not prompt; result `[(0,10), (1000,20), (2000,100), (3000,60), (4000,70)]`. Preview shows changed edge shape, not guaranteed smoothness. |
| C04 | S with P, Stop or close conflict dialog | Conflict reported for interior timestamps 1500, 2000, 2500. Stop inserts generated points strictly before 1500 and preserves all existing actions from 1500 onward; closing the dialog leaves S unchanged. |
| C05 | S with P, Replace | Result `[(0,10), (1000,20), (2000,100), (3000,60), (4000,70)]`; no duplicate endpoints, outside actions unchanged. |
| C06 | S with P, Merge / Keep Existing | Result S; generated interior value at 2000 loses. No action change means no history entry or new dirty state. |
| C07 | S with P, Merge / Use New | Result `[(0,10), (1000,20), (1500,30), (2000,100), (2500,50), (3000,60), (4000,70)]`; existing boundaries win irrespective of the interior option. |
| C08 | Destination `[(0,10), (1500,30), (2000,100), (4000,70)]`, Merge | Conflict due to interior points; no differing-position collision sub-prompt needed. Result `[(0,10), (1000,0), (1500,30), (2000,100), (3000,0), (4000,70)]`; equal pair deduplicates. |
| C09 | One destination boundary present, other absent | Preserve only the present boundary; generate the absent endpoint. Exercise start-only and end-only cases. |
| C10 | Choose Use New, reopen conflict dialog on another insertion | Use New remains selected; choosing Keep Existing then reopening remembers Keep Existing. Do not assert a particular first-use default. |

### Atomic Edits and History

| ID | Setup/action | Expected result |
| --- | --- | --- |
| A01 | Preview P against S; cancel conflict dialog | Actions, root metadata, selection, dirty flag, undo/redo stacks, and saved checkpoint all unchanged. No ScriptChanged event. |
| A02 | Apply C05 candidate to a saved Funscript with metadata/version/inverted fields using `ReplaceActionsAtomically` | Entire candidate replaces actions in the same Funscript instance, outside values and root properties persist through save/reload, no intermediate candidate is exposed. Editor event/history behavior remains pending. |
| A03 | Commit unsorted/duplicate timestamps, negative time, invalid position, then a valid candidate | Each invalid candidate throws before mutation; original actions remain intact. The valid planner candidate commits as one collection replacement. Dirty/event/history integration remains pending. |
| A04 | Preview S, then manually add `(1750,90)` before Apply | Old snapshot is not committed. Recompute/invalidate preview and conflict information; cancelling leaves the newer document, including the manual point, intact. |
| A05 | Apply C05, then Undo and Redo | Undo restores S exactly; Redo restores C05 exactly, both retaining metadata. Dirty returns to saved state on Undo and becomes true on Redo. Each operation refreshes timeline/selection safely. |
| A06 | Pattern insertion, manual insertion, deletion, completed drag | Undo all four in reverse order and redo all four in forward order. Pattern and whole drag are each one history entry; mouse-move intermediates are not entries. |
| A07 | Undo an edit, preview/cancel, then commit a new manual edit | Preview/cancel preserve redo. The new committed edit clears redo; previous history remains undoable. A Stop that inserts a prefix is itself a committed edit and clears redo. |
| A08 | Save a script, insert a pattern, Undo, Redo, then Undo and branch with a manual edit | Undo to the saved checkpoint is clean, Redo is dirty, and branching clears redo. |
| A09 | New/Open after history has entries; cancel Open separately | Successfully switching documents resets history and saved checkpoint appropriately. Cancelling Open leaves existing history/document unchanged. |
| A10 | Apply no-op C06 while clean or with existing redo history | No new history entry/event/dirty change and redo remains available. Conflict-option preference may still be remembered independently of action history. |
| A11 | Apply G13, Undo, Redo with a shorter loaded video | Timeline includes the generated endpoint after Apply/Redo and reclamps after Undo; media duration remains unchanged throughout. |

History fixtures should observe action contents and notifications, not depend on an internal history implementation. Add a large-script memory check confirming small edits retain deltas and bulk edits retain only affected data. No physical device or live mpv process is required for pure generation/application tests; WinForms presentation and timeline refresh can be checked separately.

## Manual Editor Checks

- [x] Generate into a new document and a loaded script, with and without mpv attached.
- [x] Operate video while the tool window is open; moving the playhead never changes entered times, and "From playhead" fills the current position.
- [x] Clicking the pattern preview or blank dialog background transfers keyboard focus for playback shortcuts without interfering with arrow-key editing in input fields.
- [x] Closing the pattern dialog returns keyboard focus to the timeline; clicking the main-form background focuses the timeline without requiring a click in the log.
- [x] Preview a pattern, edit the script while the tool is open, then Insert; regenerated pattern/conflict results use the current script.
- [x] Try each profile and start mode; confirm range/rise settings change the preview without changing the selected cycle count, output interval, or cadence.
- [x] Try each conflict choice and verify visible results match preview.
- [x] Empty intervals show no conflict prompt.
- [x] Existing actions at the output start/end take precedence over generated endpoint actions.
- [x] Verify Stop, Replace, and Merge are all visible, and the previous selection is remembered when reopening the conflict dialog.
- [x] Verify the conflict and collision preferences are restored after restarting the application.
- [x] Verify preview exposes the actual edge transitions, including preserved boundaries that create ramps or alter the ideal first/last cycle.
- [x] Insert beyond both timeline and media endpoints; verify Fit All and high-zoom scrolling reach the complete generated section while the original media duration remains unchanged.
- [x] After insertion, timeline extent refreshes with and without video attached; the first inserted point is selected and the previous selection is cleared.
- [x] Save a script, insert a pattern, Undo/Redo and verify the title's clean/dirty state; after Undo, a new manual edit clears Redo.
- [x] Repeated Insert operations at the same timestamp do not duplicate actions; Cancel leaves the script unchanged.
- [x] Repeated Insert/Close operations leave selection and drag state valid.
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

### Pure Generator (2026-10-05)

Implemented [PatternGenerator](../../../src/ScriptSyncPilot.Common/PatternGenerator.cs) and [PatternGeneratorTests](../../../tests/ScriptSyncPilot.Common.Tests/PatternGeneratorTests.cs). Focused generator tests passed, followed by the complete suite and consumer build:

```powershell
dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --nologo
dotnet build ScriptSyncPilot.slnx --nologo
```

Results: 40 tests passed, 0 failed, 0 skipped (29 generator cases plus 11 existing persistence cases). Solution build succeeded, including Common, both desktop consumers, and tests.

Generator coverage includes exact triangle/asymmetric fixtures, custom ratios/ranges, observed/effective cadence, midpoint rounding, non-accumulating fractional periods, whole-cycle endpoints, continuation direction/fallback, independent analytic cosine-error checks, repeated-input determinism, duration/action limits, precision failures, source immutability, and read-only results.

The generator has no media-duration input and does not modify actions or apply boundary/conflict rules. C/A application/history cases and manual editor acceptance checks are not implemented or passed by these tests. Nonfinite inputs are unrepresentable through the decimal timing API; trigonometric evaluation uses validated finite values. Actual API upload limits and UI sampling/memory trial remain unverified. PR reference: pending.

### Conflict Planning (2026-10-05)

Implemented [PatternInsertionPlan](../../../src/ScriptSyncPilot.Common/PatternInsertionPlan.cs) and [planner tests](../../../tests/ScriptSyncPilot.Common.Tests/PatternInsertionPlanTests.cs).

Focused planner run: 21 passed. Full Common suite: 61 passed, 0 failed, 0 skipped (21 planner, 29 generator, 11 persistence). `dotnet build ScriptSyncPilot.slnx --nologo` succeeded for both desktop consumers and tests.

Verified candidate results for empty/crossing/boundary-only destinations, interior conflict reporting, Stop, Replace, both Merge choices, equal-pair deduplication, one-sided boundary preservation, and no-op detection. Also verified copied snapshots remain stable after caller changes, results are read-only, invalid actions/policies are rejected, and repeated resolution does not mutate source scripts.

This covers pure conflict/candidate semantics of C01-C09, not UI prompts or cancellation/history side effects. C10 preference memory, stale-snapshot checks at commit time, metadata-preserving application, notifications, undo/redo, and UI remain pending. No candidate is applied to a document by the planner.

### Atomic Candidate Commit (2026-10-05)

Added `Funscript.ReplaceActionsAtomically` and focused model tests. A metadata round-trip test creates an actual PatternInsertionPlan Replace candidate and commits it into the loaded Funscript before saving.

Focused commit tests: 3 passed. Full Common suite: 64 passed, 0 failed, 0 skipped (29 generator, 21 planner, 14 Funscript/persistence). `dotnet build ScriptSyncPilot.slnx --nologo` succeeded for Common, both desktop consumers, and tests.

The validated Common commit primitive is verified. The planner itself remains nonmutating; its candidate is committed only by the editor workflow described below. Undo/redo remains unimplemented.

### FunscriptEditor Workflow Integration (2026-10-06)

Added the Edit > Insert Pattern workflow with profile/input controls, generated graph preview, observed/effective cadence summary, and modal Apply/Cancel. Interior conflicts open a resolution dialog with Stop/Replace/Merge, remembered Merge collision preference, and a preview of the resolved section. Candidate application uses `Funscript.ReplaceActionsAtomically`; actual changes raise one timeline ScriptChanged notification, refreshing dirty state/extent/selection, while no-op/cancel paths do not mutate the document.

Verification run:

```powershell
dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --nologo
dotnet build ScriptSyncPilot.slnx --nologo
```

Results: all 64 Common tests passed and the full solution built, including HandySync, FunscriptEditor, and tests. This does not exercise the WinForms dialogs, visual boundary transitions, remembered-choice behavior across repeated interactions, or timeline updates in a running UI. Undo/redo is not implemented. Complete the manual editor checks above before declaring the feature verified.

### Action History (2026-10-07)

Added Common `ActionEdit`/`ActionHistory` and `Funscript.ApplyEdit`, wired into `TimelineControl` (pattern insertion, value-bar/double-click insertion, Delete, and completed drags) with Edit > Undo/Redo (Ctrl+Z/Ctrl+Y). Dirty state now derives from the history's saved checkpoint.

`ActionHistoryTests` cover minimal delta extraction, ordered undo/redo of pattern/insert/delete/multi-step drag (A06 model level), no-op not clearing redo and new edits clearing it (A07/A10 model level), saved checkpoint across undo/redo/branching and reset (A08/A09 model level), stale-edit rejection, and metadata through undo/redo/save (A05 model level).

Results: 70 Common tests passed; full solution built. Editor-level behavior (selection after undo, title/dirty refresh, timeline reclamp for A11, New/Open/cancel for A09) and memory measurement remain manual checks.

### Direct Output Cycle Entry (2026-10-08)

Simplified the pattern tool to use one output interval and a directly editable full-cycle count. Removed the separate observation interval, observed cycle count, cadence-based suggestion, and observed/effective comparison. Preview reports only the cadence implied by the chosen count and quantized output duration. Updated the active plan, design, verification cases, and editor backlog accordingly.

Verification: `dotnet build ScriptSyncPilot.slnx` succeeded with no warnings or errors; `dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --no-build` passed all 70 tests. `PatternGeneratorTests.Generate_DirectCycleCountSetsOutputCadence` verifies five cycles in 12 seconds reports 25 cycles/minute and four cycles reports 20 cycles/minute. The modeless UI and editable count control still require manual visual/interaction verification.

### Conflict Dialog Follow-up (2026-10-09)

Manual verification confirmed conflict-free intervals do not prompt, but reported that Merge was clipped and the last resolution was not remembered. Made the conflict-options panel autosize, increased the dialog height, and persisted the Stop/Replace/Merge and Keep Existing/Use New selections in EditorSettings. The editor builds successfully to a temporary output directory; visual verification of the conflict choices and in-session selection is recorded below.

Follow-up manual check: all three conflict options are visible, the dialog restores the previous selection when reopened, and all selections remain remembered after restarting the application. Replace and Merge work as expected. Existing actions at the output start/end take precedence over generated endpoint actions, and the preview shows the resulting edge transitions correctly. Stop inserts the prefix before the first existing point and marks the document dirty; Cancel leaves the clean document unchanged. Repeated Insert operations at the same timestamp do not duplicate actions, Cancel leaves the script unchanged, and repeated Insert/Close cycles leave selection and drag state valid. Preview leaves the script, selection, and clean state unchanged when the tool is closed without inserting. Continue Existing follows the script between existing points; outside the range, it falls back to Pattern Start. Insertion works without mpv, beyond script and media endpoints, and with a video shorter than the requested section; Fit All and high-zoom scrolling reach the full generated section. Timeline extent refreshes with and without video attached, and the first inserted point is selected instead of the previous selection. Undoing a drag or deletion restores the expected actions and updates the title to the saved clean state. Drag/delete and undo/redo continue to work correctly after pattern insertion. Editing the script while the tool is open refreshes the generated pattern and conflict results before insertion. Video can be operated while the tool is open without changing entered times, and "From playhead" fills the selected time field.

Stop behavior follow-up: Stop inserts generated points strictly before the earliest existing interior point; Cancel remains the no-change action. Partial final cycles are allowed for this Stop prefix. Manual verification confirmed the preview and applied result; the planner test verifies preservation of the first conflict and remaining actions. All 70 Common tests pass, and the editor builds to a temporary output directory with zero warnings/errors.

### Modeless Playback Keyboard Shortcuts (2026-10-09)

Forwarded the editor's existing Left/Right frame-step and Shift/Ctrl seek shortcuts from the modeless pattern window when an editable pattern control does not have focus. The editor builds successfully; manual keyboard verification remains pending.
