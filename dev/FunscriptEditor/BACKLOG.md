# FunscriptEditor Backlog

## Pattern Insertion Tool

**Priority:** To be decided. **Status:** Future feature; details to be refined.

Insert a repeated movement pattern at the playhead. Users select a profile, set movement range, and specify the target section's duration or cycle count.

### Cadence

Count full cycles while a video plays over an observation interval lasting seconds to several minutes, then enter count and elapsed time. Calculate cycles per minute as `60 * cycleCount / elapsedSeconds`. A full cycle is bottom-to-top-to-bottom, distinct from one directional stroke.

### Profiles

Distribute movement time within each cycle, squeezing/stretching the profile to match measured cadence. Candidates: triangle wave, fast-up/slow-down, and slowing near direction changes.

### Open Decision

Interaction with existing actions in the target range, including overwrite behavior, remains undecided.

## Undo and Redo

**Priority:** To be decided. **Status:** Proposed.

Support reversible insertion/deletion, dragging, and pattern generation. Treat a completed drag/generated segment as a coherent edit.

## Markers and Snapping

**Priority:** To be decided. **Status:** Proposed.

Evaluate markers and configurable grid/marker snapping for alignment with video, retaining precise unsnapped editing.

## Multiple Tracks and Project Format

**Priority:** Optional. **Status:** Exploratory.

Evaluate multiple tracks/device-channel mapping only for a concrete workflow. A richer document could hold tracks, markers, metadata, video reference, and offset. Preserve standard funscript import/export and define a versioned schema if adopted.

## Verification and Boundaries

See the [shared backlog](../BACKLOG.md) for interaction tests, persistence round-trips, CI, and service separation. The former controller proposal does not imply a device controller in the editor.