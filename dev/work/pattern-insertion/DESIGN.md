# Pattern Insertion Design

**Status:** Agreed feature design; implementation not started. Engineering details below can be resolved during implementation and trial.

See [plan](PLAN.md) and [verification](VERIFICATION.md).

## Agreed Behavior

One cycle is a complete bottom-to-top-to-bottom movement, not a directional stroke. Users count cycles during video playback over seconds to several minutes, then enter the count and elapsed time. For positive inputs:

```text
cyclesPerMinute = 60 * observedCycles / observedSeconds
cyclePeriodMs = 1000 * observedSeconds / observedCycles
```

For example, 12 cycles over 30 seconds means 24 cycles/minute and a 2500 ms cycle period. The observation interval measures cadence; it does not automatically define the inserted section length.

Generate only complete cycles. Users can specify a positive integer output cycle count and an output timeframe; squeeze or stretch each cycle's relative profile to fit that count exactly within the timeframe:

```text
outputCyclePeriodMs = outputDurationMs / outputCycleCount
effectiveCyclesPerMinute = 60000 * outputCycleCount / outputDurationMs
```

Never clip a cycle halfway through or extend beyond the requested output timeframe to finish one. If cycle count alone is specified, derive duration from observed cadence. For a specified timeframe, automatically suggest the nearest positive whole-cycle count from measured cadence and duration, with direct editing available. Do not expose rounding-policy options. Show observed and effective cadence when fitting changes it; changing profile shape alone must not change output cadence.

Decisions recorded from [user answers](notes/2026-10-04_13-39-57.md).

## Profiles

Represent profile time as normalized phase from 0 to 1, with normalized position from 0 to 1. Map position to the chosen minimum/maximum and phase to cycle timestamps.

- Triangle: minimum at phase 0, maximum at 0.5, minimum at 1; constant speed within each direction.
- Fast-up/slow-down: default to 25% rise / 75% return, exposing the ratio in profile settings.
- Slowed direction changes: use cosine easing for rise/fall, slowing near both extrema. Funscript consumers interpolate between actions, so use adaptive intermediate sampling targeting at most one position-unit deviation from the intended curve. Verify accuracy after millisecond/position rounding. Tune defaults after trial rather than adding more initial configuration questions.

Offer two initial starting modes: **Continue Existing** and **Pattern Start**. Continue Existing uses the interpolated script value at insertion time and chooses a compatible profile phase/direction where possible. If there is no usable value or compatible phase, fall back to Pattern Start and make the fallback visible in preview. Pattern Start uses the profile's natural initial phase; built-in profiles begin at minimum moving upward. Every generated cycle returns to its starting phase. Infer existing direction where possible; ambiguous-direction tie-breaking is an implementation detail.

Put additional phase controls in profile settings rather than adding many top-level choices. Keep the dialog suitable for a future editor whose custom patterns may start anywhere; custom pattern authoring is not part of this release.

## Generation and Validation

- Pure generation takes inputs and produces an action list without accessing UI, mpv, files, or hardware.
- Validate positive finite observation count/time and output duration, a positive integer output cycle count, and integer positions with `0 <= minimum < maximum <= 100`.
- Calculate timestamps from absolute cycle/phase offsets, then round to milliseconds. Do not accumulate a rounded cycle period: that would introduce cadence drift.
- Emit shared cycle boundaries once. Output timestamps must be strictly increasing and positions valid integers.
- Reject inputs that collapse required distinct points onto one millisecond or exceed the integer timestamp representation/practical action-count limit. Do not silently distort frequency.
- Default the configurable maximum generated-section duration to three hours and cap generated output at 100,000 actions initially. These are editor safeguards, not verified Handy upload/API limits. Duration alone does not predict size: cadence and sampling density also matter. Validate configuration against timestamp limits and the independent action cap; choose the configuration UI/range during implementation. This limits section length, not loaded video duration.
- Fit the requested full-cycle count into the requested timeframe using absolute phase timestamps. Include cycle boundaries once; never emit a partial final cycle. Existing boundary preservation during application is described below.
- Insertion may extend beyond both the current script/timeline extent and the loaded media duration. Neither is an output limit: the funscript may be new or only partly authored, and the loaded video may be unrelated or too short. Generate the complete requested section without clipping to either extent, subject only to input validation and timestamp/action-count limits. After applying, expand the timeline to include the generated endpoint while retaining the media duration as context.

## Conflicts and Atomic Application

Inspect a snapshot of existing destination actions before mutation. Existing interior value-points are conflicts; interpolation between outside points is not, even when it crosses the whole section. Preserve any existing action exactly at the insertion start/end and omit the competing generated endpoint. Boundary-only points handled this way do not need a conflict prompt. Preserve actions strictly outside the section.

Do not omit generated endpoints unconditionally: emit them where no existing boundary action replaces them. Preview the actual incoming/outgoing transitions. Preserving endpoints avoids competing values but does not guarantee smooth speed or preserve the ideal first/last cycle: interpolation can form a long ramp or alter those edge cycles. Do not hide that effect or silently shift the timeframe to compensate.

When a conflict exists, ask the user:

- Stop: cancel; retain actions, metadata, dirty state, and selection.
- Replace: remove existing interior values and insert the generated section, retaining existing start/end boundary actions as described above.
- Merge: combine existing and new actions, with Keep Existing or Use New for different values at identical interior timestamps. Remember the previous collision choice in the dialog; the initial default is not significant. Identical timestamp/position pairs need only one resulting point. Boundary preservation takes precedence over the interior collision choice.

Without a conflict, apply without a question, including insertion between two distant existing points. Ask for a Merge collision sub-option only when needed to resolve different values at identical timestamps. Do not silently use Common's AddOrReplace policy: one resulting action timestamp can hold only one position. Preview the resolved result rather than implying that Merge preserves the generated profile unchanged.

Construct and validate the complete result before application. Do not replace the Funscript instance with an actions-only reconstruction, which would lose preserved root metadata. If Common needs a bulk edit API, keep it narrowly scoped and validate before modifying the existing list.

## Editor Integration

Use a focused pattern tool with profile selection/settings, selectable starting phase, observation count/time, observed/effective cycles per minute, movement range, output timeframe/full-cycle count, preview, and Apply/Cancel. Reuse existing WinForms conventions.

Capture insertion time when opening the tool. Preview is non-destructive; it must show the same start/end/cadence that Apply will use. If the script changes while the tool is open, recompute conflicts or invalidate the preview; never apply an obsolete snapshot blindly. Moving video playback alone must not silently move the captured insertion point.

On successful application, emit one coherent ScriptChanged notification, refresh the timeline limits, and clear/reconcile stale selection and drag indices. Cancel, validation failures, and Stop must not mark the document dirty. No mpv connection is required.

## Undo and Redo

Include a proper editor action-history stack, not a one-off insertion rollback. Users can undo and redo multiple edits. A new committed edit after undo clears the redo stack; preview, cancellation, and Stop are not edits and do not clear it. This is the conventional user expectation.

Pattern application is one history entry; manual insertion, deletion, and a completed drag must also participate so intervening edits remain ordered. Preserve root metadata and reconcile selection/drag state after undo/redo. Opening/creating a document starts a separate history; saving tracks a saved-state checkpoint for accurate dirty indication. Do not serialize history into standard funscript output.

Store deltas for small edits and retain affected sections for bulk changes rather than snapshotting the entire document on every edit. Do not impose a restrictive edit-count limit initially. Two integer fields occupy roughly eight bytes of raw action data: 100,000 actions are about 0.8 MB before overhead. Repeated full-document snapshots can nevertheless become large, and serialized files are larger than raw data. Measure practical history overhead during trial; implementation details must preserve the agreed undo/redo behavior.

## Implementation Details and Trial

The feature choices above are accepted. Resolve cycle-count midpoint rounding, Continue Existing direction tie-breaking/fallback presentation, configuration UI/range, adaptive sampling after quantization, and concrete delta-history structures during implementation. Validate edge transitions and memory use in trial. These details must not reintroduce partial cycles, extra rounding options, unprompted interior overwrites, or one-off undo.
