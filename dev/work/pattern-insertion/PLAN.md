# Pattern Insertion Plan

**Status:** Feature design agreed; implementation not started.
**Selected:** 2026-10-04.
**Affected projects:** FunscriptEditor; Common if a narrowly scoped action-edit API is needed. HandySync behavior is outside scope.

Source: [editor backlog](../../FunscriptEditor/BACKLOG.md). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Let a user measure cadence from video, then insert a selectable repeated profile at the playhead. Fit a user-confirmed number of whole cycles into an output timeframe and show any difference between observed and effective cadence.

## First-Version Scope

- Work on the existing single movement timeline; no multitrack/project-format dependency.
- Enter observed cycle count and elapsed seconds, including intervals lasting several minutes; display calculated cycles per minute.
- Select triangle, fast-up/slow-down, or slowed-direction-change profiles.
- Set minimum/maximum position, starting phase, and profile settings. A future pattern editor may share the settings dialog.
- Fit an integer count of whole cycles into the output timeframe, with no partial-cycle endings. Observation duration and output duration are distinct inputs.
- Default the configurable section limit to three hours and cap generated output at 100,000 actions initially; these are editor safeguards, not verified API limits.
- Suggest the nearest positive whole-cycle count automatically, allow direct editing, and show no rounding-policy controls.
- Offer Continue Existing and Pattern Start; preserve existing section boundary points and preview resulting transitions.
- Preview generated values and section extent without changing the document.
- Apply at a captured playhead timestamp, with Stop/Replace/Merge choices only when destination values conflict.
- Preserve values outside the destination section and all root JSON extension data.
- Allow insertion beyond current script/timeline and media duration, including new/partly authored scripts and an unrelated loaded video. Do not clip the requested section to those extents.
- Update selection/drag state, dirty indication, timeline extent, and scroll/zoom limits coherently after application.
- Include multilevel undo/redo for pattern and manual action edits; new edits clear redo and one completed drag/pattern application forms one history entry.

## Out of Scope

Chapters, audio analysis/conversion, multitrack editing, hardware output, automatic video analysis, a new project format, and custom pattern authoring. Editor action undo/redo is now in scope, rather than a one-operation shortcut.

## Decisions Before Implementation

- [x] Merge collision precedence is an additional user-selectable sub-option, not a fixed existing-wins policy.
- [x] Only existing value-points in the section count as conflicts; crossing interpolation alone does not.
- [x] Fit whole cycles into the specified timeframe; do not stop mid-cycle.
- [x] Starting phase is selectable; expose profile settings with future pattern-editor compatibility.
- [x] Use configurable duration limits of a few hours and proper multilevel undo/redo; new edits clear redo.
- [x] Remember Merge collision choice; initial default is not significant.
- [x] Preserve existing boundary actions and omit competing generated endpoints, but do not omit endpoints unconditionally or promise smooth transitions.
- [x] Automatically suggest nearest positive whole-cycle count with direct editing, no rounding-policy controls.
- [x] Offer Continue Existing and Pattern Start, with additional phase controls in profile settings.
- [x] Start with 25% rise / 75% return and cosine easing; adaptive sampling targets at most one position-unit deviation.
- [x] Default configurable duration to three hours and generated output cap to 100,000 actions.
- [x] Use small-edit deltas and affected-section history for bulk edits, saved-state checkpoints, and no restrictive initial edit-count limit.

Engineering details such as midpoint rounding, direction tie-breaking, configuration presentation, and concrete history structures can be resolved during implementation without reopening these accepted choices.

## Milestones and Tasks

### 1. Design and Test Cases

- [x] Record accepted feature decisions in DESIGN.md from the user's answer note.
- [ ] Specify deterministic rounding/tie-breaking and quantized sampling rules for tests.
- [ ] Choose focused test tooling consistent with the repository; do not silently expand this into general CI work.
- [ ] Establish generation, rounding, conflict, and atomic-edit test cases.

### 2. Generation and Application Logic

- [ ] Implement a pure profile generator independent of WinForms and mpv.
- [ ] Validate inputs, generate deterministic millisecond actions, and reject unsafe sizes/quantization.
- [ ] Compute conflict information and candidate Stop/Replace/Merge results without mutation.
- [ ] Apply the chosen result as one coherent edit while preserving metadata and outside actions.
- [ ] Implement action history shared by generated and manual edits, including saved-state checkpoints.

### 3. Editor Workflow

- [ ] Add pattern controls/settings, selectable phase, observed/effective cadence, and preview.
- [ ] Capture insertion time, detect document/playhead changes, and keep preview/apply consistent.
- [ ] Prompt only on conflicts; cancel/Stop leave document and dirty state unchanged.
- [ ] Refresh timeline state and validate load/edit/save compatibility.
- [ ] Wire multilevel Undo/Redo controls and manual edit history; new committed edits clear redo.

### 4. Verification and Completion

- [ ] Complete the automated and manual checks in VERIFICATION.md.
- [ ] Document limitations and results; record the PR reference.
- [ ] Update permanent editor/Common architecture for actual implemented behavior.
- [ ] Return deferred work to the appropriate backlog.
- [ ] Archive this folder under `dev/archive/YYYY-MM-pattern-insertion/` and update links.

## Outcome

Implementation, verification results, and PR reference: not yet available. This plan does not claim the feature is implemented.
