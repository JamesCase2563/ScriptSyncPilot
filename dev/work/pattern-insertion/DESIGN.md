# Pattern Insertion Design

**Status:** Proposed design; unresolved choices below require confirmation before implementation.

See [plan](PLAN.md) and [verification](VERIFICATION.md).

## Agreed Behavior

One cycle is a complete bottom-to-top-to-bottom movement, not a directional stroke. Users count cycles during video playback over seconds to several minutes, then enter the count and elapsed time. For positive inputs:

```text
cyclesPerMinute = 60 * observedCycles / observedSeconds
cyclePeriodMs = 1000 * observedSeconds / observedCycles
```

For example, 12 cycles over 30 seconds means 24 cycles/minute and a 2500 ms cycle period. The observation interval measures cadence; it does not automatically define the inserted section length.

Choose an output duration or full-cycle count, a position range within 0-100, and a profile. Fit each profile into the same cycle period; changing shape must not change measured cadence.

## Profiles

Represent profile time as normalized phase from 0 to 1, with normalized position from 0 to 1. Map position to the chosen minimum/maximum and phase to cycle timestamps.

- Triangle: minimum at phase 0, maximum at 0.5, minimum at 1; constant speed within each direction.
- Fast-up/slow-down: earlier peak within the cycle followed by a longer return. Proposed peak phase is 0.25; exact ratio is not yet agreed.
- Slowed direction changes: eased rise and fall with low speed near both extrema. Funscript consumers interpolate between actions, so this requires intermediate samples, not merely extrema. Choose an error tolerance and sampling limit before implementation.

Proposed starting phase is minimum position moving upward. Boundary blending with surrounding actions is not promised by the first version and must not silently change cadence or existing values.

## Generation and Validation

- Pure generation takes inputs and produces an action list without accessing UI, mpv, files, or hardware.
- Validate positive finite cycle count, observation time, and output duration/count; validate integer positions with `0 <= minimum < maximum <= 100`.
- Calculate timestamps from absolute cycle/phase offsets, then round to milliseconds. Do not accumulate a rounded cycle period: that would introduce cadence drift.
- Emit shared cycle boundaries once. Output timestamps must be strictly increasing and positions valid integers.
- Reject inputs that collapse required distinct points onto one millisecond or exceed the integer timestamp representation/practical action-count limit. Do not silently distort frequency.
- Full-cycle-count mode ends after the requested number of cycles. Proposed duration mode evaluates a partial final cycle at the exact requested end; this remains to be confirmed.
- Insertion may extend beyond both the current script/timeline extent and the loaded media duration. Neither is an output limit: the funscript may be new or only partly authored, and the loaded video may be unrelated or too short. Generate the complete requested section without clipping to either extent, subject only to input validation and timestamp/action-count limits. After applying, expand the timeline to include the generated endpoint while retaining the media duration as context.

## Conflicts and Atomic Application

Inspect a snapshot of existing destination actions before mutation. The target section starts at the captured insertion timestamp and ends at the computed output endpoint. Proposed range semantics are inclusive of both endpoints, with actions strictly outside retained exactly.

When a conflict exists, ask the user:

- Stop: cancel; retain actions, metadata, dirty state, and selection.
- Replace: remove existing values in the target section and insert the generated section.
- Merge: combine existing and new actions; proposed identical-timestamp policy retains the existing value and deduplicates identical points.

Without a conflict, apply without a question. Merge must not rely on Common's current AddOrReplace behavior to silently overwrite a conflicting value: a standard action timestamp can hold only one resulting position. Exact collision policy and treatment of an existing line interpolating through the target range are open decisions. Preview the resolved result rather than implying that Merge preserves the generated profile unchanged.

Construct and validate the complete result before application. Do not replace the Funscript instance with an actions-only reconstruction, which would lose preserved root metadata. If Common needs a bulk edit API, keep it narrowly scoped and validate before modifying the existing list.

## Editor Integration

Use a focused pattern dialog/tool with profile selection, observation count/time, calculated cycles/minute, range, output duration/count mode, preview, and Apply/Cancel. Reuse existing WinForms visual conventions.

Capture insertion time when opening the tool. Preview is non-destructive; it must show the same start/end/cadence that Apply will use. If the script changes while the tool is open, recompute conflicts or invalidate the preview; never apply an obsolete snapshot blindly. Moving video playback alone must not silently move the captured insertion point.

On successful application, emit one coherent ScriptChanged notification, refresh the timeline limits, and clear/reconcile stale selection and drag indices. Cancel, validation failures, and Stop must not mark the document dirty. No mpv connection is required.

Aim for a single undoable insertion. An editor-wide undo framework is a separate backlog feature; the minimum safe undo scope and behavior after intervening edits remain to be decided.

## Open Choices

Merge collision precedence; conflict/boundary interpolation rules; partial-cycle endings; initial phase; profile ratio/sampling tolerance; maximum output size; first-release undo scope. Record agreed choices here before coding and mirror them in verification cases.
