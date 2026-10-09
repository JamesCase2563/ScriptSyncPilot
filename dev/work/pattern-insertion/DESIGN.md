# Pattern Insertion Design

**Status:** Agreed design; pure generation, conflict planning, single-timeline editor application, and undo/redo implemented. Manual UI verification remains pending.

See [plan](PLAN.md) and [verification](VERIFICATION.md).

## Agreed Behavior

One cycle is a complete bottom-to-top-to-bottom movement, not a directional stroke. Users enter the output interval and directly choose how many full cycles it contains. There is no separate cadence measurement or suggested count.

```text
outputCyclePeriodMs = outputDurationMs / outputCycleCount
outputCyclesPerMinute = 60000 * outputCycleCount / outputDurationMs
```

For example, five cycles over 12 seconds means 25 cycles/minute and a 2400 ms cycle period. Changing the cycle count directly changes the output cadence.

Generate only complete cycles. Users can specify a positive integer output cycle count and an output timeframe; squeeze or stretch each cycle's relative profile to fit that count exactly within the timeframe:

```text
outputCyclePeriodMs = outputDurationMs / outputCycleCount
effectiveCyclesPerMinute = 60000 * outputCycleCount / outputDurationMs
```

Never clip a cycle halfway through or extend beyond the requested output interval to finish one. The selected count and interval determine output cadence; changing profile shape alone does not change it.

Decisions recorded from [user answers](notes/2026-10-04_13-39-57.md).

## Profiles

Represent profile time as normalized phase from 0 to 1, with normalized position from 0 to 1. Map position to the chosen minimum/maximum and phase to cycle timestamps.

- Triangle: minimum at phase 0, maximum at 0.5, minimum at 1; constant speed within each direction.
- Asymmetric cycle: expose rise time as a percentage of cycle duration. Default to 25% rise / 75% return; values above 50% make the rise slower than the return.
- Slowed direction changes: use cosine easing for rise/fall, slowing near both extrema. Funscript consumers interpolate between actions, so use adaptive intermediate sampling targeting at most one position-unit deviation from the intended curve. Verify accuracy after millisecond/position rounding. Tune defaults after trial rather than adding more initial configuration questions.

Offer two initial starting modes: **Continue Existing** and **Pattern Start**. Continue Existing uses the interpolated script value at insertion time and chooses a compatible profile phase/direction where possible. If there is no usable value or compatible phase, fall back to Pattern Start and make the fallback visible in preview. Pattern Start uses the profile's natural initial phase; built-in profiles begin at minimum moving upward. Every generated cycle returns to its starting phase. Continuation tie-breaking is specified below.

Put additional phase controls in profile settings rather than adding many top-level choices. Keep the dialog suitable for a future editor whose custom patterns may start anywhere; custom pattern authoring is not part of this release.

## Generation and Validation

- Pure generation takes inputs and produces an action list without accessing UI, mpv, files, or hardware.
- Validate positive output duration, a positive integer output cycle count, and integer positions with `0 <= minimum < maximum <= 100`.
- Calculate timestamps from absolute cycle/phase offsets, then round to milliseconds. Do not accumulate a rounded cycle period: that would introduce cadence drift.
- Emit shared cycle boundaries once. Output timestamps must be strictly increasing and positions valid integers.
- Reject inputs that collapse required distinct points onto one millisecond or exceed the integer timestamp representation/practical action-count limit. Do not silently distort frequency.
- Default the configurable maximum generated-section duration to three hours and cap generated output at 100,000 actions initially. These are editor safeguards, not verified Handy upload/API limits. Duration alone does not predict size: cadence and sampling density also matter. Validate configuration against timestamp limits and the independent action cap; choose the configuration UI/range during implementation. This limits section length, not loaded video duration.
- Fit the requested full-cycle count into the requested timeframe using absolute phase timestamps. Include cycle boundaries once; never emit a partial final cycle. Existing boundary preservation during application is described below.
- Insertion may extend beyond both the current script/timeline extent and the loaded media duration. Neither is an output limit: the funscript may be new or only partly authored, and the loaded video may be unrelated or too short. Generate the complete requested section without clipping to either extent, subject only to input validation and timestamp/action-count limits. After applying, expand the timeline to include the generated endpoint while retaining the media duration as context.

## Deterministic Numeric Rules

These rules define the first implementation and its tests. Numeric choices are not additional user controls.

### Rounding and Timebase

- Use `MidpointRounding.AwayFromZero` for timestamps and integer positions. All valid times/positions are nonnegative, so an exact half rounds upward. Do not use .NET's default midpoint-to-even rounding.
- Keep user-entered timing/cadence calculations in decimal arithmetic where practical. Use double precision for trigonometric profile evaluation; reject nonfinite inputs. Do not round displayed cadence and then feed it back into generation.
- Quantize the captured start time and total output duration once to integer milliseconds. Reject a duration rounding to zero. Preview shows the quantized start/end and effective cadence.
- For cycle `k` and relative phase offset `u`, compute `startMs + round(durationMs * (k + u) / cycleCount)`, never repeatedly add a rounded period. Treat total start/end as authoritative: first timestamp is startMs and last is startMs + durationMs.
- Evaluate the phase-shifted profile in real-valued position units before rounding positions. Round only at output. Values outside 0-100 are invalid; roundoff-only excursions may be clamped to the requested range.
- Use checked arithmetic for final timestamp bounds. Reject negative start time or endpoints above `int.MaxValue` before editing the document.
- Seed mandatory phase boundaries, cycle boundaries, and extrema in time order. Shared representations of the same intended point are emitted once. Distinct mandatory times collapsing to the same millisecond are rejected, even if their rounded positions happen to match; silently deduplicating them could erase cycle structure.

### Continue Existing Tie-Breaking

- Use a sorted, unique-timestamp snapshot. At an exact action time, use that action's value; otherwise linearly interpolate between the enclosing pair, retaining fractional position until final output rounding. Do not extrapolate before/after the script; fall back to Pattern Start if no value is available.
- For direction at an exact action, prefer the incoming segment. If it is flat or absent, use the outgoing segment. Between actions, use the enclosing segment. If no nonzero slope is available, prefer the ascending branch.
- Normalize the existing value to the selected movement range without clamping an incompatible value. Out-of-range continuation falls back visibly to Pattern Start.
- Find matching phases of the built-in profile, prefer the branch matching the inferred direction, and choose the earliest normalized phase in `[0, 1)` when candidates remain tied. At extrema choose the valid outgoing direction (up at minimum, down at maximum); phase 1 canonicalizes to 0.
- Direction compatibility means matching the sign, not guaranteeing identical speed. Preview must not describe continuation as a guaranteed velocity-continuous join.

### Quantized Adaptive Sampling

For the cosine profile, use `p(u) = (1 - cos(2 * pi * u)) / 2` before range mapping and phase shifting. Piecewise-linear profiles use their defined knots. Let `F(t)` be the intended real-valued position at absolute time `t`, and `L(t)` the linearly interpolated saved integer actions.

- Seed mandatory points first, then process segments chronologically. For each final quantized segment, require `max(abs(F(t) - L(t))) <= 1` position unit over its entire continuous time interval, with a numerical comparison allowance of `1e-9` position units.
- Check interval endpoints and all interior error extrema, not just the midpoint. For linear profiles include every crossed knot; for cosine profiles include every solution of `F'(t) = L'(t)`, splitting analysis at phase/half-cycle boundaries. These checks cover error introduced by both timestamp and position rounding.
- If a segment exceeds tolerance, split at the nearest integer millisecond to its temporal midpoint using the same rounding rule, constrained strictly between its endpoints. Evaluate `F` at that integer timestamp and round its position, then recheck the left segment before the right.
- Do not omit collinear points unless the resulting combined segment passes the same bound and no mandatory cycle/extremum point is lost.
- If no interior millisecond exists, a mandatory endpoint already violates tolerance, or refinement would exceed 100,000 generated actions, reject with a precision/size error rather than relaxing tolerance or cadence. The generation cap includes all seeded/refined points before destination boundary suppression.
- Verify the bound for the generated candidate before conflict resolution. Merge and preserved existing boundary values can change the resulting curve; preview those differences rather than applying the generator's accuracy guarantee to the resolved script.

## Conflicts and Atomic Application

Inspect a snapshot of existing destination actions before mutation. Existing interior value-points are conflicts; interpolation between outside points is not, even when it crosses the whole section. Preserve any existing action exactly at the insertion start/end and omit the competing generated endpoint. Boundary-only points handled this way do not need a conflict prompt. Preserve actions strictly outside the section.

Do not omit generated endpoints unconditionally: emit them where no existing boundary action replaces them. Preview the actual incoming/outgoing transitions. Preserving endpoints avoids competing values but does not guarantee smooth speed or preserve the ideal first/last cycle: interpolation can form a long ramp or alter those edge cycles. Do not hide that effect or silently shift the timeframe to compensate.

When a conflict exists, ask the user:

- Stop: insert generated actions strictly before the first existing interior point, then retain that point and all following existing actions. Stop may end partway through a cycle; Cancel closes the conflict dialog without changing the script.
- Replace: remove existing interior values and insert the generated section, retaining existing start/end boundary actions as described above.
- Merge: combine existing and new actions, with Keep Existing or Use New for different values at identical interior timestamps. Remember the last conflict resolution and collision choice across application sessions. Identical timestamp/position pairs need only one resulting point. Boundary preservation takes precedence over the interior collision choice.

Without a conflict, apply without a question, including insertion between two distant existing points. Ask for a Merge collision sub-option only when needed to resolve different values at identical timestamps. Do not silently use Common's AddOrReplace policy: one resulting action timestamp can hold only one position. Preview the resolved result rather than implying that Merge preserves the generated profile unchanged.

Construct and validate the complete result before application. Do not replace the Funscript instance with an actions-only reconstruction, which would lose preserved root metadata. If Common needs a bulk edit API, keep it narrowly scoped and validate before modifying the existing list.

Common provides `Funscript.ReplaceActionsAtomically(candidate.Actions)`: it copies and validates the complete ordered action list before swapping it into the same Funscript instance. It preserves root extension data and returns `false` without mutation for an identical action list. The caller owns ScriptChanged/dirty notifications, preview staleness, selection, and undo/redo. This is the commit primitive; editor UI application and history integration are not implemented yet.

## Implemented Editor Integration

The Edit > Insert Pattern tool provides profile selection/settings, selectable starting phase, movement range, output start/end times and directly editable full-cycle count, output cadence, and Insert/Close. Each time input has a "From playhead" button and millisecond precision. While the modeless dialog is open, the main timeline shows the output interval as a tinted band; it follows time edits and turns red when the interval is invalid. Preview overlays the generated points and line in a distinct color. Invalidating the preview or successfully inserting clears only the generated points/line; closing the dialog clears the range band as well. Insert regenerates against the current script before conflict analysis and leaves the tool open after insertion. All pattern controls and the last conflict/collision choices persist in EditorSettings across dialog and application sessions.

Preview is non-destructive and shows the generated section's start/end/cadence. Moving video playback while the tool is open never changes entered times. The conflict dialog appears only when the planner finds interior points; it previews the selected Stop/Replace/Merge result and preserves boundary actions.

Apply resolves a candidate through `PatternInsertionPlan` and commits it with `Funscript.ReplaceActionsAtomically`. On a real change, the editor emits one ScriptChanged notification, marking the document dirty and reclamping timeline limits; selection/drag state is refreshed. No-op candidates, Cancel, and validation failures do not mark the document dirty. No mpv connection is required.

If the resolved action list is identical to the current list, application is a no-op: do not emit ScriptChanged, add a history entry, change the dirty flag, or clear redo. Stop is a real edit when its generated prefix adds actions; Cancel is not an edit. Remembering a dialog preference is independent of document action history.

## Undo and Redo

Include a proper editor action-history stack, not a one-off insertion rollback. Users can undo and redo multiple edits. A new committed edit after undo clears the redo stack; preview and cancellation are not edits and do not clear it. Stop creates a history entry when it inserts a prefix. This is the conventional user expectation.

Pattern application is one history entry; manual insertion, deletion, and a completed drag must also participate so intervening edits remain ordered. Preserve root metadata and reconcile selection/drag state after undo/redo. Opening/creating a document starts a separate history; saving tracks a saved-state checkpoint for accurate dirty indication. Do not serialize history into standard funscript output.

Implementation: Common's `ActionEdit.Between(before, after)` stores only the differing contiguous section (common prefix/suffix trimmed), so a click stores one or two actions and a pattern insertion stores its affected section. `ActionHistory` keeps undo/redo stacks of these deltas; `Funscript.ApplyEdit` splices them and rejects an edit whose removed section does not match. Each entry has a never-reused id and the saved checkpoint is the id on top of the undo stack, so branching never falsely reads as clean. The editor's `TimelineControl` owns the history, records each committed edit (a drag is recorded on mouse-up), and resets it on New/Open; Edit > Undo/Redo use Ctrl+Z/Ctrl+Y.

Store deltas for small edits and retain affected sections for bulk changes rather than snapshotting the entire document on every edit. Do not impose a restrictive edit-count limit initially. Two integer fields occupy roughly eight bytes of raw action data: 100,000 actions are about 0.8 MB before overhead. Repeated full-document snapshots can nevertheless become large, and serialized files are larger than raw data. Measure practical history overhead during trial; implementation details must preserve the agreed undo/redo behavior.

## Remaining Implementation Details and Trial

### Implemented Pure API

Common's `PatternGenerator.Generate(PatternGenerationOptions, existingActions)` returns a `GeneratedPattern` with read-only actions, quantized start/end, effective cadence, resolved start phase, and an explicit Pattern Start fallback flag. It never mutates an existing Funscript or accesses UI, media, files, or hardware.

Timing inputs are decimal seconds; cycle count is a positive integer. Options select profile, start mode, range, rise fraction, and a configurable duration ceiling. The 100,000-action cap is fixed initially. The generated result reports cadence directly from the chosen count and quantized output duration. Callers do not pass media duration; generation cannot clip to a loaded video.

Continuation validates a copied sorted/unique action snapshot. Generation errors are reported before returning a candidate. Decimal timing uses midpoint-away-from-zero rounding; analytic full-interval error checks refine quantized linear/cosine segments chronologically. Application-specific boundary suppression and collision handling are deliberately not part of generation.

### Implemented Conflict Planner

`PatternInsertionPlan.Create(existingActions, generatedPattern)` validates and copies both inputs. It exposes original/generated snapshots, interior conflicting actions, differing-value interior collisions, and flags for preserved start/end boundaries. `HasConflicts` indicates whether a future UI should ask Stop/Replace/Merge; `RequiresMergeChoice` indicates differing values at identical interior timestamps. Crossing lines and boundary-only actions do not set those flags.

`Resolve(choice, mergeChoice)` builds a read-only complete action candidate without touching the source Funscript, metadata, dirty state, or history. Stop returns the original snapshot plus generated actions strictly before the first existing interior point; if there are no conflicts, it returns the full generated result. Replace removes only interior actions; Merge combines timestamps and selects Keep Existing or Use New for collisions. Existing endpoints are retained for every policy. Identical values deduplicate; HasChanges compares the full result to the captured original so a no-op remains detectable.

Inputs must contain valid, sorted, unique timestamps. Generated actions must span StartMs/EndMs and fit the generation cap. Plans remain stable when caller collections change later; the future apply layer must verify the original snapshot is still current before committing. The planner does not present dialogs, remember preferences, commit candidates, or implement undo.

The feature choices and deterministic numeric rules above are specified. Concrete delta-history structures and any interaction refinements can be resolved during implementation. Manually validate analytic error appearance, edge transitions, dialog behavior, and memory use in trial. These details must not reintroduce partial cycles, extra rounding options, unprompted interior overwrites, or one-off undo.
