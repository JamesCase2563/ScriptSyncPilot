# FunscriptEditor Backlog

## Table of Contents

- [Editor GUI Improvements](#editor-gui-improvements)
- [Pattern Improvements](#pattern-improvements)
- [Large-Script Performance and History Memory](#large-script-performance-and-history-memory)
- [Markers and Snapping](#markers-and-snapping)
- [Constrained Point Movement and Keyboard Nudging](#constrained-point-movement-and-keyboard-nudging)
- [Multiple-Point Selection and Group Editing](#multiple-point-selection-and-group-editing)
- [Tap-Based Pattern Generation](#tap-based-pattern-generation)
- [Editable Pattern Preview](#editable-pattern-preview)
- [Expressive Patterns and Controlled Variation](#expressive-patterns-and-controlled-variation)
- [Movement Speed Indicators](#movement-speed-indicators)
- [Author-Guided Video Motion Detection](#author-guided-video-motion-detection)
- [Live Pattern Recording](#live-pattern-recording)
- [Multiple Tracks and Project Format](#multiple-tracks-and-project-format)
- [Verification and Boundaries](#verification-and-boundaries)
- [Completed](#completed)
	- [Pattern Insertion Tool](#pattern-insertion-tool)
	- [Undo and Redo](#undo-and-redo)
	- [Video Playback Speed](#video-playback-speed)
	- [Keyboard Numeric Position Entry](#keyboard-numeric-position-entry)

Entries above Completed link to GitHub issues for active work. Completed feature details and verification records remain in the linked archives.

## Initial-release scope classification

Reviewed against the [approved support scope](../archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
New authoring features below remain future work; classification does not remove them
or change their relative future priority. Defects affecting existing supported
editing, file preservation or shutdown remain in scope.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| Editor GUI Improvements | Deferred | Editor-specific usability ideas; coordinate with shared GUI work |
| Pattern Improvements | Deferred | Additional profiles and generation refinements; no initial-release requirement |
| Large-Script Performance and History Memory | Deferred | Quantitative optimization work is not a release prerequisite absent an identified blocker; no unmeasured large-script performance guarantee |
| Markers and Snapping | Deferred | New editing feature |
| Constrained Point Movement and Keyboard Nudging | Deferred | New editing feature |
| Multiple-Point Selection and Group Editing | Deferred | New editing feature |
| Tap-Based Pattern Generation | Deferred | New generation workflow |
| Editable Pattern Preview | Deferred | New draft-editing workflow beyond the implemented preview |
| Expressive Patterns and Controlled Variation | Deferred | New generation behavior; no randomization commitment |
| Movement Speed Indicators | Deferred | New readouts/visualizations, not physical safety certification |
| Author-Guided Video Motion Detection | Experimental | Exploration only; no selected implementation or new dependency approved |
| Live Pattern Recording | Deferred | New recording mode; retain existing numeric point-entry behavior |
| Multiple Tracks and Project Format | Deferred | Includes proposed track types, duplication/conversion, audio-derived actions, chapter editing/export and project persistence; no new media tooling or schema required for release |
| Verification and Boundaries | In scope | Verify existing interactions, persistence and lifecycle; no device controller in the editor |
| Completed features | In scope | Preserve pattern insertion, undo/redo, video playback speed and numeric position entry |

## Editor GUI Improvements

Tracked in GitHub issue [#25](https://github.com/JamesCase2563/ScriptSyncPilot/issues/25).

## Pattern Improvements

Tracked in GitHub issue [#26](https://github.com/JamesCase2563/ScriptSyncPilot/issues/26).

## Large-Script Performance and History Memory

Tracked in GitHub issue [#27](https://github.com/JamesCase2563/ScriptSyncPilot/issues/27).

## Markers and Snapping

Tracked in GitHub issue [#28](https://github.com/JamesCase2563/ScriptSyncPilot/issues/28).

## Constrained Point Movement and Keyboard Nudging

Tracked in GitHub issue [#29](https://github.com/JamesCase2563/ScriptSyncPilot/issues/29).

## Multiple-Point Selection and Group Editing

Tracked in GitHub issue [#30](https://github.com/JamesCase2563/ScriptSyncPilot/issues/30).

## Tap-Based Pattern Generation

Tracked in GitHub issue [#31](https://github.com/JamesCase2563/ScriptSyncPilot/issues/31).

## Editable Pattern Preview

Tracked in GitHub issue [#32](https://github.com/JamesCase2563/ScriptSyncPilot/issues/32).

## Expressive Patterns and Controlled Variation

Tracked in GitHub issue [#33](https://github.com/JamesCase2563/ScriptSyncPilot/issues/33).

## Movement Speed Indicators

Tracked in GitHub issue [#34](https://github.com/JamesCase2563/ScriptSyncPilot/issues/34).

## Author-Guided Video Motion Detection

Tracked in GitHub issue [#35](https://github.com/JamesCase2563/ScriptSyncPilot/issues/35).

## Live Pattern Recording

Tracked in GitHub issue [#36](https://github.com/JamesCase2563/ScriptSyncPilot/issues/36).

## Multiple Tracks and Project Format

Tracked in GitHub issue [#37](https://github.com/JamesCase2563/ScriptSyncPilot/issues/37).

## Verification and Boundaries

Tracked in GitHub issue [#38](https://github.com/JamesCase2563/ScriptSyncPilot/issues/38).

## Completed

### Pattern Insertion Tool

Implemented and automatically/manually verified: interval-and-whole-cycle generation, profiles, preview, conflict handling, and undoable application. Quantitative large-script measurements remain a [separate item](#large-script-performance-and-history-memory). Archived [plan](../archive/2026-10-pattern-insertion/PLAN.md), [design](../archive/2026-10-pattern-insertion/DESIGN.md), and [verification](../archive/2026-10-pattern-insertion/VERIFICATION.md).

### Undo and Redo

Implemented and automatically/manually verified: multilevel delta-based edits, coherent drag/generated-section history, and saved-state checkpoints. Memory measurement remains a [separate item](#large-script-performance-and-history-memory). Included in the archived [pattern-insertion plan](../archive/2026-10-pattern-insertion/PLAN.md), [design](../archive/2026-10-pattern-insertion/DESIGN.md), and [verification](../archive/2026-10-pattern-insertion/VERIFICATION.md).

### Video Playback Speed

Implemented and verified (2026-10-11): editor-controlled mpv playback rate for review and timing work. Archived [plan](../archive/2026-10-video-playback-speed/PLAN.md), [design](../archive/2026-10-video-playback-speed/DESIGN.md), and [verification](../archive/2026-10-video-playback-speed/VERIFICATION.md).

### Keyboard Numeric Position Entry

Implemented and verified with automated tests and user-confirmed manual checks; PR review status is not recorded. Physical position keys insert individual undoable points at video timestamps. Keep this separate from [live recording](#live-pattern-recording) range controls so one keypress cannot do both. Archived [plan](../archive/2026-10-numeric-position-entry/PLAN.md), [design](../archive/2026-10-numeric-position-entry/DESIGN.md), and [verification](../archive/2026-10-numeric-position-entry/VERIFICATION.md).
