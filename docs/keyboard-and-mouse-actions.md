# Keyboard and Mouse Actions

This document inventories the current app-specific keyboard shortcuts and mouse gestures in HandySync and FunscriptEditor. It is intended as a shared reference when adding or changing interactions so the two apps remain consistent where their workflows overlap.

**Reviewed:** 2026-11-21. This describes implemented behavior, not proposed shortcuts. Standard Windows Forms behavior for text boxes, menus, buttons, and dialogs is not exhaustively listed.

## HandySync

### Keyboard actions (ordered by key)

Device-slider keys work only when the relevant slider is enabled and focused. The offset slider is horizontal; the stroke-range slider is vertical. For the range slider, the active thumb is the minimum or maximum endpoint currently selected by keyboard focus or the last mouse interaction.

| Key | Control | Action |
| --- | --- | --- |
| Down | Vertical stroke-range slider | Decrease the active endpoint by 1 percentage point. |
| End | Either device slider | Set the active value to the slider maximum. For a range slider, this sets the active endpoint to 100%, subject to the other endpoint. |
| Home | Either device slider | Set the active value to the slider minimum. For a range slider, this sets the active endpoint to 0%, subject to the other endpoint. |
| Left | Horizontal offset slider | Decrease device offset by 10 ms. No value change on the vertical stroke-range slider. |
| Page Down | Either device slider | Decrease the active value by the large step: 100 ms for offset or 10 percentage points for stroke range. |
| Page Up | Either device slider | Increase the active value by the large step: 100 ms for offset or 10 percentage points for stroke range. |
| Right | Horizontal offset slider | Increase device offset by 10 ms. No value change on the vertical stroke-range slider. |
| Space | Stroke-range slider | Switch the active minimum/maximum endpoint. No action on the offset slider. |
| Up | Vertical stroke-range slider | Increase the active endpoint by 1 percentage point. |

Slider changes are clamped to their configured range; the stroke minimum cannot pass the maximum and the maximum cannot pass the minimum. Offset spans -1000 to +1000 ms; stroke endpoints span 0% to 100%.

HandySync has no additional app-level form shortcuts for file, playback, attach, or device connection actions. Text fields and standard buttons retain their usual Windows keyboard behavior.

### Mouse actions (ordered by gesture)

| Gesture | Control | Action |
| --- | --- | --- |
| Left-click and drag | Script waveform | Request a seek to the clicked or dragged time when mpv is attached. The waveform spans the whole available script/video duration and does not zoom or scroll. |
| Left-click and drag | Offset slider | Change the single device-offset value. |
| Left-click and drag | Stroke-range slider | Select the nearer endpoint thumb and change that minimum or maximum value; the endpoints cannot cross. |
| Click | Labeled buttons, menus, or file-browse control | Activate the displayed action (for example attach/detach mpv, play/pause video, connect/disconnect Handy, or browse for a script). |

The waveform and custom sliders implement left-button interaction only. There are no app-specific middle/right-button gestures or mouse-wheel actions in HandySync.

## FunscriptEditor

### Keyboard actions (ordered by key)

| Key | Control/context | Action |
| --- | --- | --- |
| Top-row `0`–`9` and the physical key left of `1` | Timeline focused; no modifiers | Insert/update an action at the current playhead. The physical key left of `1` sets 0%; `1`–`9` set 10%–90%; `0` sets 100%. These are physical top-row keys, not numeric-keypad keys. Holding a key does not repeat the insertion. |
| Ctrl+N | Editor, including the pattern dialog | Create a new funscript. |
| Ctrl+O | Editor, including the pattern dialog | Open a funscript. |
| Ctrl+S | Editor, including the pattern dialog | Save the current funscript. |
| Ctrl+Y | Editor | Redo. |
| Ctrl+Z | Editor | Undo. |
| Delete | Timeline focused with an action selected | Delete the selected action. |
| Left / Right | Timeline focused; mpv attached | Step backward/forward one video frame. Other focused controls retain their normal arrow-key behavior. |
| Shift+Left / Shift+Right | Timeline focused; mpv attached | Step backward/forward one second. Other focused controls retain their normal arrow-key behavior. |
| Ctrl+Left / Ctrl+Right | Timeline focused; mpv attached | Step backward/forward ten seconds. Other focused controls retain their normal arrow-key behavior. |
| Space | Timeline focused; no modifiers; mpv attached | Toggle mpv play/pause once per key press. |

When the timeline has focus, arrow keys step video; they do not navigate between funscript points. When another control has focus, its normal keyboard behavior takes precedence. Point-navigation shortcuts are not currently implemented. The timeline's numeric position keys and Space action require timeline focus; the file shortcuts are also forwarded while the pattern dialog is open.

### Mouse actions (ordered by gesture)

| Gesture | Control | Action |
| --- | --- | --- |
| Left-click empty track area | Timeline | Move the playhead and request an mpv seek when mpv is attached. |
| Left-click the value bar at the right edge | Timeline value bar | Insert/update an action at the current playhead time using the clicked position. |
| Left-double-click empty track area | Timeline | Insert an action at the clicked time and position. Double-clicking an existing point or the value bar does not insert. |
| Left-drag an action point | Timeline | Move the point in time and/or position. Its timestamp is constrained so it cannot cross neighboring actions. |
| Left-drag empty track area | Timeline | Scrub the playhead and request mpv seeks continuously when mpv is attached. |
| Middle-drag the track area | Timeline | Pan horizontally without changing zoom. The value bar is not part of the pan area. If playback-follow is active, follow stays suppressed while the playhead is outside the manually chosen view and resumes after it re-enters. |
| Shift+left-drag empty track area | Timeline | Pan horizontally without changing zoom, including when the mouse wheel or middle button is unavailable. Ordinary left-drag continues scrubbing; dragging an action point continues editing. Playback-follow stays suppressed while the playhead is outside the manually chosen view and resumes after it re-enters. |
| Mouse wheel | Pointer over timeline | Zoom around the pointer's time position. The wheel is routed to the timeline even if another control has focus. |
| Drag or click the horizontal scrollbar | Timeline scrollbar | Navigate the visible time range. |
| Drag or click the zoom slider; click **Fit All** | Zoom controls | Change timeline zoom or fit the full duration in the view. |
| Click a labeled menu item or button | Editor controls | Run the displayed action. This includes file operations, playback and frame/second stepping, speed controls, and pattern insertion. |

## Consistency notes

- State whether an action requires focus on a particular control, an attached mpv session, or an enabled device control.
- Prefer the same modifier meanings across apps when the action is analogous. Keep application-specific actions explicit where the workflows differ.
- Before adding a shortcut, check for conflicts with text entry, dialog controls, existing menu shortcuts, and the other app. Update this reference with the implementation.
- Keep issue proposals separate from the implemented-action tables until the behavior ships.

## Source references

- [FunscriptEditor main form](../src/ScriptSyncPilot.FunscriptEditor/MainForm.cs) and [timeline control](../src/ScriptSyncPilot.FunscriptEditor/TimelineControl.cs)
- [FunscriptEditor wheel filter](../src/ScriptSyncPilot.FunscriptEditor/TimelineWheelFilter.cs)
- [HandySync main form](../src/ScriptSyncPilot.HandySync/MainForm.cs), [device slider](../src/ScriptSyncPilot.HandySync/DeviceSliderControl.cs), and [script waveform](../src/ScriptSyncPilot.HandySync/ScriptWaveformControl.cs)
- [FunscriptEditor scripting workflow](scripting-workflow.md)
