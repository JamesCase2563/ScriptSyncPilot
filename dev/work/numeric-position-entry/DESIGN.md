# Numeric Position Entry Design

**Status:** Agreed top-row physical-key mapping, modifier roles, timeline focus scope, and repeat policy; not implemented.

See [plan](PLAN.md) and [verification](VERIFICATION.md).

## Key Mapping

The top row gives a spatial progression from 0% at the key left of `1` to 100% at `0`, without Shift. All labels refer to physical QWERTY positions, not the characters produced by the active layout. This keeps the user's hand near the future physical Q-P-row recording controls while leaving that mode separate.

| Key group | Key | Position |
| --- | --- | --- |
| Top row | Physical key immediately left of `1` | 0% |
| Top row | `1` through `9` | 10% through 90% |
| Top row | `0` | 100% |

Numpad keys do not insert points in this version. Preserve their native behavior, including navigation with Num Lock off.

The physical-key-to-position map is identical across authoring modes; modifiers select an operation, not a different position:

| Input | Point-entry mode | Future live-recording mode |
| --- | --- | --- |
| Unmodified position key | Insert mapped position | Set mapped maximum |
| Shift only + position key | No authoring action | Set mapped minimum |
| Any Ctrl/Alt/AltGr combination, with or without Shift | No authoring action | No authoring action |

Do not strip modifiers and then treat the key as unmodified. Existing file and Undo/Redo shortcuts remain unchanged. Recording range validation must reject invalid min/max combinations without silently moving the other boundary; recording itself is outside this work item.

## Physical Keys and Layouts

Assume a Latin-like alphabet and close-to-standard PC keyboard geometry, including Norwegian and US keyboards. Use a fixed scan-code map for the eleven physical positions, not typed characters or layout-specific digit/OEM virtual-key mappings. The key left of `1` represents 0% regardless of its label: vertical bar on Norwegian keyboards, grave accent on US. No per-language mapping tables are needed.

Read scan-code and extended-key identity from Windows keyboard messages within the focused timeline's keyboard boundary. Reject unrelated or extended keys rather than treating a shared scan-code fragment as a top-row position. Keep decoding separate from mode/modifier decisions so future recording can reuse the exact position map. Do not add a global keyboard hook or route through generated characters.

Check the fixed map automatically and manually on the user's normal keyboard/layout. A Norwegian/US layout-switch smoke check is useful where readily available, but an exhaustive language/hardware matrix is not a completion gate. Unusual geometry and nonstandard keyboards are unsupported in this scope; do not add detection, fallback mappings, or a rebinding facility.

## Focus and Routing

Numeric authoring shortcuts are active only when the timeline has focus. Text boxes, numeric controls, combo boxes, the pattern dialog, and file/error dialogs keep their native behavior. No global capture or playback-arrow forwarding is introduced.

Handle each accepted press at one ownership boundary, preferably TimelineControl, and suppress the consumed key so it cannot activate another command or beep. Do not route the same press through both MainForm.KeyPreview and the timeline. Numeric authoring must not depend on MainForm's playback handler, which returns when no mpv session is attached.

One command per physical press: ignore operating-system repeat messages until release. Use the keyboard message's previous-key-state information and, if needed, physical pressed-key tracking; changing modifiers while a position key remains held must not count as another press. Clear any tracking on focus loss/document switch so missed releases cannot disable input, and still reject repeat messages received after focus returns. Release/repress permits another command.

## Model and History

Capture the current timeline playhead once per accepted command. Convert to milliseconds using the existing value-bar insertion policy: clamp to the timeline duration and apply its established rounding. Do not introduce marker or grid snapping in this feature.

Expose a small timeline command accepting an integer position in 0-100. Have mouse value-bar insertion and keyboard insertion share the same position-based path where practical. Reuse `Commit` and `Funscript.AddOrReplace`; do not mutate `_script.Actions` or create a second history stack.

- Missing timestamp: insert an action in order.
- Existing timestamp: replace its position without creating a duplicate.
- Identical timestamp and position: do not add history, clear redo, emit ScriptChanged, or change dirty state.
- Real change: one history entry, one ScriptChanged notification, and normal timeline refresh.
- Select the action at the captured timestamp after insertion/replacement, matching value-bar behavior. A no-op may select that existing point without changing document history.
- Preserve playhead time, root metadata, and unrelated actions. Undo/Redo must restore the expected states and saved checkpoint.

Use the existing drag-finalization boundary before committing a keyboard edit; reset any held-key tracking when a document is loaded. The command must also work on an empty document with no video or device connection.

## Live Recording Boundary

This feature inserts individual points at the user's chosen times, supporting manual rhythm entry without tap-tempo inference. Future recording reuses these same eleven physical positions and values: unmodified keys set maximum and Shift-only keys set minimum. Its mode routing must ensure a press cannot both insert a point and change a pattern parameter. The future speed presets should likewise use physical Q-P-row positions rather than language-dependent letter characters. This change does not implement or arm recording.

## Limitations

Scan-code decoding and native interaction require focused verification on close-to-standard PC keyboards with Latin-like layouts. Universal keyboard compatibility, numpad insertion, continuous recording, sub-frame timing compensation, and input-latency measurement are outside scope. Reuse existing model/history behavior without unrelated refactoring.