# FunscriptEditor Backlog

Future and active work is tracked in [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues).
GitHub Issues are the source of truth for priorities, status, labels, epics, sub-issues, and implementation progress.

This file retains editor release boundaries and completed feature baselines that are not duplicated in issue tracking.

## Initial-release scope classification

Reviewed against the [approved support scope](../archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
New authoring features below remain future work; classification does not remove them
or change their relative future priority. Defects affecting existing supported
editing, file preservation or shutdown remain in scope.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| Editor GUI Improvements | Deferred | Editor-specific usability ideas remain deferred and should be coordinated with shared GUI work |
| Pattern Improvements | Deferred | Additional profiles and generation refinements remain deferred beyond the implemented pattern tool |
| <a id="large-script-performance-and-history-memory"></a>Large-Script Performance and History Memory | Deferred | Quantitative optimization is not a release prerequisite without an identified blocker; current history and script behavior take precedence over unmeasured performance guarantees |
| Markers and Snapping | Deferred | New marker and snapping behavior is deferred |
| Constrained Point Movement and Keyboard Nudging | Deferred | Constrained movement and keyboard nudging are deferred new editing features |
| Multiple-Point Selection and Group Editing | Deferred | Multiple-point selection and group editing are deferred new editing features |
| Tap-Based Pattern Generation | Deferred | Tap-based generation is a deferred new authoring workflow |
| Editable Pattern Preview | Deferred | Editable preview behavior is deferred; the implemented preview remains a separate baseline |
| Expressive Patterns and Controlled Variation | Deferred | Expressive patterns and controlled variation remain deferred; the implementation does not commit to randomization |
| Movement Speed Indicators | Deferred | Movement speed indicators are deferred readouts and visualizations, not physical safety certification |
| Author-Guided Video Motion Detection | Experimental | Experimental exploration only; no implementation or dependency is selected |
| <a id="live-pattern-recording"></a>Live Pattern Recording | Deferred | Live recording is deferred; numeric top-row position entry remains the existing implementation and must not imply range recording |
| Multiple Tracks and Project Format | Deferred | Multiple tracks and a richer project format are deferred; proposed track types, chapter editing/export and project persistence do not require new media tooling or schema until adopted; preserve standard funscript interchange and metadata |
| Verification and Boundaries | In scope | Verify existing interactions, persistence and lifecycle; no device controller in the editor |
| Completed features | In scope | Preserve pattern insertion, undo/redo, video playback speed and numeric position entry |

## Completed

### Pattern Insertion Tool

Implemented and automatically/manually verified: interval-and-whole-cycle generation, profiles, preview, conflict handling, and undoable application. Quantitative large-script measurements remain a [separate item](#large-script-performance-and-history-memory). Archived [plan](../archive/2026-10-pattern-insertion/PLAN.md), [design](../archive/2026-10-pattern-insertion/DESIGN.md), and [verification](../archive/2026-10-pattern-insertion/VERIFICATION.md).

### Undo and Redo

Implemented and automatically/manually verified: multilevel delta-based edits, coherent drag/generated-section history, and saved-state checkpoints. Memory measurement remains a [separate item](#large-script-performance-and-history-memory). Included in the archived [pattern-insertion plan](../archive/2026-10-pattern-insertion/PLAN.md), [design](../archive/2026-10-pattern-insertion/DESIGN.md), and [verification](../archive/2026-10-pattern-insertion/VERIFICATION.md).

### Video Playback Speed

Implemented and verified (2026-10-11): editor-controlled mpv playback rate for review and timing work. Archived [plan](../archive/2026-10-video-playback-speed/PLAN.md), [design](../archive/2026-10-video-playback-speed/DESIGN.md), and [verification](../archive/2026-10-video-playback-speed/VERIFICATION.md).

### Keyboard Numeric Position Entry

Implemented and verified with automated tests and user-confirmed manual checks; PR review status is not recorded. Physical position keys insert individual undoable points at video timestamps. Keep this separate from [live recording](#live-pattern-recording) range controls so one keypress cannot do both. Archived [plan](../archive/2026-10-numeric-position-entry/PLAN.md), [design](../archive/2026-10-numeric-position-entry/DESIGN.md), and [verification](../archive/2026-10-numeric-position-entry/VERIFICATION.md).
