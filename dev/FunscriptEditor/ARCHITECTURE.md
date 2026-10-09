# FunscriptEditor Architecture

## Responsibilities

FunscriptEditor is a WinForms editor for standard funscript timestamp/position actions. Video playback/VR projection stays in mpv or an external player; the editor does not control Handy hardware.

- `MainForm` owns persistence, dirty state, settings, transport UI, and mpv lifecycle.
- `TimelineControl` renders the action graph, selection, playhead, and value-bar guide; it inserts/moves/deletes actions using Common's Funscript model.
- `TimelineWheelFilter` routes wheel zoom to the hovered timeline.
- `EditorSettings` persists paths, pipe configuration, and pattern dialog values on a best-effort basis.

## Editing Contracts

Actions use milliseconds and 0-100 positions. Common preserves unknown root JSON properties. Empty/new documents are valid. ScriptChanged marks edits and refreshes timeline zoom, viewport, and scrollbar limits.

The timeline spans media/script duration, with a 60-second minimum without media. Scrollbar units are milliseconds. Deleting selection clears drag state. The right value bar inserts at the playhead and draws a hover guide across the visible graph.

## Playback Lifecycle

Launch/attach transitions share a gate and disable connection buttons. Attach retains its cancellation source and checks lifecycle identity before publishing. Position/pause/duration/disconnect events retain session identity, and queued UI callbacks reject obsolete sessions. Successful attachment refreshes current observed properties.

Shutdown cancels/disposes the session, clears media duration, disables transport, and terminates owned mpv, not externally managed players. Failed attachment and active-session disconnect reuse cleanup.

## Pattern Insertion

Edit > Insert Pattern generates a candidate through Common's pure `PatternGenerator`, uses explicit output start/end times in a modeless tool window (regenerating against the current script on Insert), and uses `PatternInsertionPlan` for conflict analysis and Stop/Replace/Merge candidates. Conflicts preview their resolved destination section; boundary actions are preserved. The last conflict resolution and Merge collision preference persist through `EditorSettings`. A chosen candidate is committed to the existing Funscript through `TimelineControl.ReplaceActions` (`ReplaceActionsAtomically` plus one history entry), followed by one ScriptChanged notification. Insert leaves the tool open; its complete set of numeric and selection values persist through `EditorSettings`. A no-op does not mark dirty. Manual UI verification remains pending.

## Undo/Redo

`TimelineControl` owns a Common `ActionHistory` and records every committed edit as a delta: value-bar/double-click insertion, Delete, a completed drag (one entry on mouse-up), and pattern insertion. Edit > Undo/Redo (Ctrl+Z/Ctrl+Y) apply deltas and reselect the affected action. The title's dirty marker derives from the history's saved checkpoint; New/Open reset history, and Save marks the checkpoint.

## Future Direction

Keep editing, persistence, and playback ownership distinct. Extract session orchestration only when complexity/testability warrants it. See the [editor backlog](BACKLOG.md) and [shared backlog](../BACKLOG.md). Multitrack editing/richer project files are possibilities, not implemented capabilities.