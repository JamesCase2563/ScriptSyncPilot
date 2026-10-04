# Pattern Insertion Plan

**Status:** Planning; implementation not started.
**Selected:** 2026-10-04.
**Affected projects:** FunscriptEditor; Common if a narrowly scoped action-edit API is needed. HandySync behavior is outside scope.

Source: [editor backlog](../../FunscriptEditor/BACKLOG.md). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Let a user count full movement cycles over a measured video interval, enter those values, and insert a selectable repeated profile at the playhead with the same cycles-per-minute cadence.

## First-Version Scope

- Work on the existing single movement timeline; no multitrack/project-format dependency.
- Enter observed cycle count and elapsed seconds, including intervals lasting several minutes; display calculated cycles per minute.
- Select triangle, fast-up/slow-down, or slowed-direction-change profiles.
- Set minimum/maximum position and output duration or full-cycle count. Observation duration and generated-section duration are distinct inputs.
- Preview generated values and section extent without changing the document.
- Apply at a captured playhead timestamp, with Stop/Replace/Merge choices only when destination values conflict.
- Preserve values outside the destination section and all root JSON extension data.
- Allow insertion beyond current script/timeline and media duration, including new/partly authored scripts and an unrelated loaded video. Do not clip the requested section to those extents.
- Update selection/drag state, dirty indication, timeline extent, and scroll/zoom limits coherently after application.

## Out of Scope

Chapters, audio analysis/conversion, multitrack editing, hardware output, automatic video analysis, a new project format, and a complete editor-wide undo system. Whether this feature ships with a single-operation undo remains an explicit scope decision.

## Decisions Before Implementation

- [ ] Confirm Merge semantics for different positions at the same millisecond. Proposed default: retain the existing destination value; never write duplicate timestamps.
- [ ] Confirm duration-mode handling of a final partial cycle. Proposed default: clip to the requested end and include its evaluated endpoint.
- [ ] Confirm initial phase/direction. Proposed default: minimum position, moving upward.
- [ ] Define conflict detection, including existing interpolation through an otherwise empty target range and boundary actions.
- [ ] Decide profile ratios, curved-profile sampling tolerance, and practical duration/action-count limits.
- [ ] Decide first-release undo support and its interaction with later edits.

The agreed Stop/Replace/Merge prompt is not open for redesign; only the detailed collision and boundary semantics need resolution.

## Milestones and Tasks

### 1. Design and Test Cases

- [ ] Resolve the decisions above and record them in DESIGN.md.
- [ ] Choose focused test tooling consistent with the repository; do not silently expand this into general CI work.
- [ ] Establish generation, rounding, conflict, and atomic-edit test cases.

### 2. Generation and Application Logic

- [ ] Implement a pure profile generator independent of WinForms and mpv.
- [ ] Validate inputs, generate deterministic millisecond actions, and reject unsafe sizes/quantization.
- [ ] Compute conflict information and candidate Stop/Replace/Merge results without mutation.
- [ ] Apply the chosen result as one coherent edit while preserving metadata and outside actions.

### 3. Editor Workflow

- [ ] Add pattern controls, computed cadence, and preview.
- [ ] Capture insertion time, detect document/playhead changes, and keep preview/apply consistent.
- [ ] Prompt only on conflicts; cancel/Stop leave document and dirty state unchanged.
- [ ] Refresh timeline state and validate load/edit/save compatibility.

### 4. Verification and Completion

- [ ] Complete the automated and manual checks in VERIFICATION.md.
- [ ] Document limitations and results; record the PR reference.
- [ ] Update permanent editor/Common architecture for actual implemented behavior.
- [ ] Return deferred work to the appropriate backlog.
- [ ] Archive this folder under `dev/archive/YYYY-MM-pattern-insertion/` and update links.

## Outcome

Implementation, verification results, and PR reference: not yet available. This plan does not claim the feature is implemented.
