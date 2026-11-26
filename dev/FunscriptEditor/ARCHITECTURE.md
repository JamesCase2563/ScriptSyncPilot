# FunscriptEditor Architecture

## Responsibilities

FunscriptEditor is a WinForms editor for standard funscript timestamp/position actions. Video playback/VR projection stays in mpv or an external player; the editor does not control Handy hardware.

- `MainForm` owns persistence, dirty state, settings, transport UI, and mpv lifecycle.
- `TimelineControl` renders the action graph, selection, playhead, and value-bar guide; it inserts/moves/deletes actions using Common's Funscript model.
- `TimelineWheelFilter` routes wheel zoom to the hovered timeline.
- `EditorSettings` persists paths, pipe configuration, and pattern dialog values on a best-effort basis.

## Movement-Speed Coloring and Readout

`TimelineControl` calculates adjacent segment measurements and uses the shared Common speed-color model for valid segments. The range is either Fit to script (all valid segments, P99) or a persisted manual range. Fit is recalculated on script load or explicit fit; editing, zooming, and scrolling do not change the current fitted range. Invalid-timing segments remain neutral. Color clamping affects only the color; it does not alter the reported speed.

The fixed-height readout reports script-space average speed at 1x. Its fixed columns show segment duration, absolute position distance, and average pp/s; point, value-preview, and drag contexts use incoming/outgoing rows. The legend uses the same square-root transfer and palette as segment drawing. `EditorSettings` owns the editor's independent settings JSON fields for enablement, scale mode, and manual minimum/maximum.

High-contrast adaptation remains in the editor: timeline segments use system highlight and outline colors, and the legend and preferences remain readable. Numeric values remain available when coloring is disabled or high contrast replaces the rainbow colors. These figures describe script data, not device capability or physical movement.

## Editing Contracts

Actions use milliseconds and 0-100 positions. Common preserves unknown root JSON properties. Empty/new documents are valid. ScriptChanged marks edits and refreshes timeline zoom, viewport, and scrollbar limits.

The timeline spans the longer of media and script duration, with a 60-second minimum without media. Its scrollbar uses millisecond units and permits up to one viewport of overscroll before time zero and after the timeline's final time. Deleting selection clears drag state. The right value bar inserts at the playhead and draws a hover guide across the visible graph.

## Keyboard Position Entry

With the timeline focused, eleven physical top-row scan codes insert positions at the current playhead: the key left of `1` sets 0%, `1` through `9` set 10-90%, and `0` sets 100%. These labels describe standard keyboard positions, not layout-dependent characters. Close-to-standard PC keyboards with Latin-like layouts are assumed; numpad insertion is not supported.

`TimelineControl.PreProcessMessage` uses `TryDecodePositionKey` to accept only unmodified, non-extended position-key messages before character translation. Accepted repeats are consumed without insertion using the message's previous-key-state flag; arrow navigation retains hold-repeat. Other controls, modifiers, and unrelated messages keep native processing.

Keyboard and value-bar entry share `InsertPositionAtPlayhead`, which validates 0-100, captures/clamps/rounds the playhead using the existing timestamp policy, and commits through AddOrReplace and action history. An existing timestamp is updated without duplicates; identical values do not create history or invalidate redo. The affected point is selected. Automated editor tests and user-performed manual checks cover this behavior.

Middle-button dragging in the timeline pans the viewport horizontally without seeking or editing actions. Panning uses mouse capture, clamps to the extended viewport bounds, and updates the scrollbar; release, capture loss, or document loading ends it. Pan release and capture loss retain the playhead's resulting screen X as the follow anchor. Left-button point dragging and scrubbing retain their existing behavior.

## Playback Lifecycle

`TimelineControl` keeps a horizontal follow anchor in client coordinates, defaulting to the track center. Every eligible active-playback position update moves the viewport so the playhead remains at that X while the timeline scrolls at the observed movie-time rate; following does not change zoom, edit script actions, or issue seeks. The anchor may be outside the visible track, so playback continues scrolling even while the playhead line is offscreen. Panning updates the anchor to the playhead's resulting screen X. Timeline clicks/scrub movement use the pointer X, with scrub release establishing the final anchor. Zoom updates the anchor to the playhead's resulting screen X. Explicit point and time seeks preserve the current anchor, including while paused; passive paused position observations and pausing alone do not move the view. Paused relative transport/frame steps query mpv for the resulting position and align that explicit seek to the active anchor. The Center Playhead button places the playhead at track center and resets the anchor there. A translucent overlay marks viewport time outside the media duration, with a solid start boundary and dashed end boundary; these movie boundaries remain distinct from the longer script timeline when a script extends beyond its video.

Playback speed is controlled independently of funscript actions/history and media-time coordinates. The transport provides a 0.10-5.00x input in 0.05x steps and a 1x reset; the displayed rate follows mpv observations, including external changes and values outside the editor input range. Attaching preserves an external player's current rate; editor-launched mpv starts paused at 1x. Speed is not persisted or applied on reconnect. Queued observations and write failures are guarded by session identity and form lifetime.

Launch/attach transitions share a gate and disable connection buttons. Attach retains its cancellation source and checks lifecycle identity before publishing. Position/pause/duration/disconnect events retain session identity, and queued UI callbacks reject obsolete sessions. Successful attachment refreshes current observed properties.

Shutdown cancels/disposes the session, clears media duration, disables transport, and terminates owned mpv, not externally managed players. Failed attachment and active-session disconnect reuse cleanup.

## Pattern Insertion

Edit > Insert Pattern generates a candidate through Common's pure `PatternGenerator`, uses explicit output start/end times in a modeless tool window (regenerating against the current script on Insert), and uses `PatternInsertionPlan` for conflict analysis and Stop/Replace/Merge candidates. Conflicts preview their resolved destination section; boundary actions are preserved. The last conflict resolution and Merge collision preference persist through `EditorSettings`. A chosen candidate is committed to the existing Funscript through `TimelineControl.ReplaceActions` (`ReplaceActionsAtomically` plus one history entry), followed by one ScriptChanged notification. Insert leaves the tool open; its complete set of numeric and selection values persist through `EditorSettings`. A no-op does not mark dirty. Quantitative large-script memory/performance measurement is deferred.

## Undo/Redo

`TimelineControl` owns a Common `ActionHistory` and records every committed edit as a delta: value-bar/double-click insertion, Delete, a completed drag (one entry on mouse-up), and pattern insertion. Edit > Undo/Redo (Ctrl+Z/Ctrl+Y) apply deltas and reselect the affected action. The title's dirty marker derives from the history's saved checkpoint; New/Open reset history, and Save marks the checkpoint.

## Future Direction

Keep editing, persistence, and playback ownership distinct. Extract session orchestration only when complexity/testability warrants it. See the [editor backlog](BACKLOG.md), [shared backlog](../BACKLOG.md), and [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues). Multitrack editing/richer project files are possibilities, not implemented capabilities.
