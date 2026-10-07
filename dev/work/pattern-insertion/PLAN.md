# Pattern Insertion Plan

**Status:** In progress; generation, planning, and single-timeline editor application implemented. Undo/redo and manual UI verification pending.
**Selected:** 2026-10-04.
**Affected projects:** Common (pure generation and tests), FunscriptEditor (future application/UI). HandySync behavior is outside scope.

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

Midpoint rounding, direction tie-breaking, and quantized sampling are specified in DESIGN.md. Configuration presentation and concrete history structures can be resolved during implementation without reopening accepted choices.

## Milestones and Tasks

### 1. Design and Test Cases

- [x] Record accepted feature decisions in DESIGN.md from the user's answer note.
- [x] Specify deterministic rounding/tie-breaking and quantized sampling rules for tests in DESIGN.md, with concrete cases in VERIFICATION.md. Generator tests are implemented; application/history tests remain pending.
- [x] Establish MSTest/VSTest tooling with `tests/ScriptSyncPilot.Common.Tests` and passing Funscript regression tests. Add a separate editor test project if feature logic is editor-owned; general CI remains separate work.
- [x] Establish generation, rounding, conflict, and atomic-edit test cases: G01-G13, C01-C10, and A01-A11 in VERIFICATION.md. Pure generator coverage is implemented; conflict/atomic-edit coverage awaits those components.

### 2. Generation and Application Logic

- [x] Implement a pure profile generator independent of WinForms and mpv in Common's PatternGenerator.
- [x] Validate inputs, generate deterministic millisecond actions, and reject unsafe sizes/quantization, with focused generator tests.
- [x] Compute conflict information and candidate Stop/Replace/Merge results without mutation using Common's PatternInsertionPlan, with exact-fixture and snapshot-isolation tests.
- [x] Provide a validated atomic Common action-replacement API and verify a Replace candidate preserves outside actions/metadata.
- [x] Wire candidate commit into the editor as one coherent edit with one notification, dirty update, selection/extent refresh, and conflict-dialog behavior.
- [ ] Implement action history shared by generated and manual edits, including saved-state checkpoints.

### 3. Editor Workflow

- [x] Add pattern controls/settings, selectable phase, observed/effective cadence, and preview.
- [x] Capture insertion time for the modal preview/apply workflow.
- [x] Prompt only on conflicts; cancel/Stop leave the document unchanged.
- [x] Refresh timeline state and preserve root metadata through the Common commit API.
- [ ] Wire multilevel Undo/Redo controls and manual edit history; new committed edits clear redo.

### 4. Verification and Completion

- [ ] Complete the automated and manual checks in VERIFICATION.md.
- [ ] Document limitations and results; record the PR reference.
- [ ] Update permanent editor/Common architecture for actual implemented behavior.
- [ ] Return deferred work to the appropriate backlog.
- [ ] Archive this folder under `dev/archive/YYYY-MM-pattern-insertion/` and update links.

## Outcome

Generation, conflict planning, and editor candidate application are implemented. Common tests and a full solution build pass; see VERIFICATION.md. Undo/redo and manual UI verification remain outstanding, so the complete feature is not yet verified. PR reference: not yet available.
