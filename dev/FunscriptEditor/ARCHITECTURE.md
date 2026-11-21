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

## Keyboard Position Entry

With the timeline focused, eleven physical top-row scan codes insert positions at the current playhead: the key left of `1` sets 0%, `1` through `9` set 10-90%, and `0` sets 100%. These labels describe standard keyboard positions, not layout-dependent characters. Close-to-standard PC keyboards with Latin-like layouts are assumed; numpad insertion is not supported.

`TimelineControl.PreProcessMessage` uses `TryDecodePositionKey` to accept only unmodified, non-extended position-key messages before character translation. Accepted repeats are consumed without insertion using the message's previous-key-state flag; arrow navigation retains hold-repeat. Other controls, modifiers, and unrelated messages keep native processing.

Keyboard and value-bar entry share `InsertPositionAtPlayhead`, which validates 0-100, captures/clamps/rounds the playhead using the existing timestamp policy, and commits through AddOrReplace and action history. An existing timestamp is updated without duplicates; identical values do not create history or invalidate redo. The affected point is selected. Automated editor tests and user-performed manual checks cover this behavior.

Middle-button dragging in the timeline pans the viewport horizontally without seeking or editing actions. Panning uses mouse capture, clamps to existing viewport bounds, and updates the scrollbar; release, capture loss, or document loading ends it. Left-button point dragging and scrubbing retain their existing behavior.

## Playback Lifecycle

During active playback, reaching the visible timeline's right edge recenters the viewport on the playhead without changing zoom or seeking. Existing timeline bounds limit centering near the end. Auto-follow is suppressed during panning, point dragging, or scrubbing; paused/manual playhead updates do not recenter.

Playback speed is controlled independently of funscript actions/history and media-time coordinates. The transport provides a 0.10-5.00x input in 0.05x steps and a 1x reset; the displayed rate follows mpv observations, including external changes and values outside the editor input range. Attaching preserves an external player's current rate; editor-launched mpv starts paused at 1x. Speed is not persisted or applied on reconnect. Queued observations and write failures are guarded by session identity and form lifetime.

Launch/attach transitions share a gate and disable connection buttons. Attach retains its cancellation source and checks lifecycle identity before publishing. Position/pause/duration/disconnect events retain session identity, and queued UI callbacks reject obsolete sessions. Successful attachment refreshes current observed properties.

Shutdown cancels/disposes the session, clears media duration, disables transport, and terminates owned mpv, not externally managed players. Failed attachment and active-session disconnect reuse cleanup.

## Pattern Insertion

Edit > Insert Pattern generates a candidate through Common's pure `PatternGenerator`, uses explicit output start/end times in a modeless tool window (regenerating against the current script on Insert), and uses `PatternInsertionPlan` for conflict analysis and Stop/Replace/Merge candidates. Conflicts preview their resolved destination section; boundary actions are preserved. The last conflict resolution and Merge collision preference persist through `EditorSettings`. A chosen candidate is committed to the existing Funscript through `TimelineControl.ReplaceActions` (`ReplaceActionsAtomically` plus one history entry), followed by one ScriptChanged notification. Insert leaves the tool open; its complete set of numeric and selection values persist through `EditorSettings`. A no-op does not mark dirty. Quantitative large-script memory/performance measurement is deferred.

## Undo/Redo

`TimelineControl` owns a Common `ActionHistory` and records every committed edit as a delta: value-bar/double-click insertion, Delete, a completed drag (one entry on mouse-up), and pattern insertion. Edit > Undo/Redo (Ctrl+Z/Ctrl+Y) apply deltas and reselect the affected action. The title's dirty marker derives from the history's saved checkpoint; New/Open reset history, and Save marks the checkpoint.

## Future Direction

Keep editing, persistence, and playback ownership distinct. Extract session orchestration only when complexity/testability warrants it. See the [editor backlog](BACKLOG.md), [shared backlog](../BACKLOG.md), and [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues). Multitrack editing/richer project files are possibilities, not implemented capabilities.
