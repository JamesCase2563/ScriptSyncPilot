# Numeric Position Entry Verification

**Status:** Shared timeline insertion and physical-key routing with repeat suppression implemented. Five focused editor tests and the solution build pass; manual checks remain pending. Results below distinguish automated decisions/model checks from running-UI verification.

See [plan](PLAN.md) and [design](DESIGN.md). The top-row physical map, timeline-only focus scope, modifier exclusions, and repeat suppression are agreed requirements. Future recording behavior is a compatibility contract, not an implemented feature under test here.

## Key Mapping

- [x] Decoder maps physical QWERTY top-row `1` through `9` scan codes to 10 through 90 without character input.
- [x] Decoder maps physical top-row `0` scan code to 100, not 0.
- [x] On the user's normal close-to-standard keyboard, all eleven positions map correctly, including left-of-`1`=0%; record the layout tested.
- [x] Automated decoding checks cover the fixed scan-code map and reject unrelated/extended keys. No per-language mapping tables or exhaustive layout matrix are required.
- [x] Numpad keys, including Multiply, do not insert points. Num Lock off retains native navigation.
- [x] Decoder rejects Shift/Ctrl/Alt combinations, including mixed modifiers and the Ctrl+Alt representation of AltGr. Shift is reserved for future recording minimums, not a second point-entry binding. Actual keyboard delivery remains a manual check.

## Insertion and History

- [x] A new document accepts a point at the current playhead with no mpv or device attached.
- [x] Insertion uses the captured playhead timestamp and existing timeline clamp/rounding behavior without moving the playhead.
- [x] A missing timestamp inserts in sorted order and preserves all unrelated actions.
- [x] An existing timestamp changes only its position; no duplicate timestamp is added.
- [x] An identical timestamp/value is a no-op: no history entry, dirty change, ScriptChanged event, or redo invalidation.
- [x] A real insertion/replacement creates exactly one history entry and one ScriptChanged event, and selects the affected point.
- [x] Undo/Redo restore a sequence of keyboard and mouse edits in order, retaining metadata and saved clean/dirty state.
- [x] After Undo, a real numeric edit clears Redo while identical numeric input preserves it.
- [x] Keyboard insertion safely finalizes an existing drag using the established commit boundary.
- [x] Saving/reloading preserves positions, timestamps, and root JSON properties.

## Manual Interaction

- [x] With the timeline focused and video playing, successive presses create points at successive media timestamps.
- [x] While paused, repeated different positions replace the action at that same playhead timestamp.
- [x] Digits and OEM keys retain native behavior in text/numeric fields, combo boxes, and the modeless pattern dialog.
- [x] File pickers, error messages, and other modal windows do not insert points into the editor.
- [x] Existing file shortcuts, Undo/Redo, Delete, and playback controls continue to work.
- [x] Accepted keys are consumed once, without duplicate routing, unwanted beeps, or triggering another control.
- [x] Holding a position key inserts once; release/repress inserts again. Changing modifiers while it remains held does not create another command.
- [x] Holding Left/Right continues repeated frame stepping; holding Shift/Ctrl+Left/Right continues repeated seeking. Position-entry repeat suppression does not alter existing navigation behavior.
- [x] Focus loss and New/Open do not leave held-key tracking stuck or admit held-key repeat messages as fresh commands.
- [x] Manual trial on the user's normal keyboard confirms all eleven physical positions and modifier handling. Norwegian/US layout-switch smoke testing is optional where readily available, not a completion gate.

## Required Gates

Run focused automated checks for mapping, accepted modifiers, insertion/replacement/no-op behavior, and history wherever existing test infrastructure can exercise them. Do not claim a Common model test proves WinForms focus or physical-layout behavior. Keep new test infrastructure proportional to the editor-owned risk.

Build FunscriptEditor after implementation. If Common changes, run its persistence/history regressions and build both desktop consumers. Record exact commands and results; builds alone do not verify key mapping or interaction behavior.

Complete the applicable manual checks above and record rejected/deferred options explicitly. Update the permanent editor architecture/backlog only to behavior actually implemented and verified.

## Results and Limitations

Planning documents created on 2026-10-10 and updated to the agreed fixed top-row physical map, timeline-only focus scope, modifier exclusions, and held-key repeat suppression. Assume Latin-like layouts with close-to-standard PC keyboard geometry; universal language/hardware support and rebinding are not required. Numpad insertion is outside scope.

### Shared Insertion Command (2026-10-10)

Added `TimelineControl.InsertPositionAtPlayhead(int position)`, rejecting values outside 0-100 before mutation. Existing value-bar insertion delegates to this command, retaining its timestamp clamp/rounding, Commit/AddOrReplace history behavior, selection, and repaint. Keyboard routing and navigation handling are unchanged.

`dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --no-restore` succeeded. No automated behavior tests or manual feature checks were performed for this step; the checks above remain pending.

### Timeline Keyboard Routing (2026-10-10)

`TimelineControl.PreProcessMessage` consumes unmodified, non-extended top-row key-down messages while the timeline is focused, before character translation. Scan code 0x29 maps to 0%; 0x02 through 0x0B map to 10% through 100%. Accepted presses call the shared insertion command. Bit 30 (previous key state) suppresses held-key repeats without local tracking; navigation, modified keys, and unrelated messages retain the existing processing path.

`dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --no-restore` succeeded. No automated behavior tests or manual keyboard checks performed yet; focus, mapping, repeats, and navigation remain to be verified in the running editor.

### Focused Automated Tests (2026-10-10)

Added [NumericPositionEntryTests](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) in a Windows-targeted MSTest/VSTest project following the existing package conventions. Extracted the existing decision logic into `TimelineControl.TryDecodePositionKey`, used by the production message handler and deterministic tests; routing behavior is otherwise unchanged.

Five tests cover all eleven scan codes, consumed repeats without insertion, all seven Shift/Ctrl/Alt combinations, absent focus, non-key-down messages, extended/unrelated keys, and navigation/numpad repeat rejection. STA tests instantiate the real timeline and exercise insertion, replacement, timestamp rounding/clamping, invalid positions, no-op notification/redo behavior, saved checkpoints, and undo/redo branching.

`dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --filter FullyQualifiedName~NumericPositionEntryTests` passed 5 tests, 0 failed, 0 skipped. `dotnet build ScriptSyncPilot.slnx --no-restore` succeeded, including both consumers and both test projects. Common source was unchanged.

These tests do not simulate physical keyboard delivery, show/focus the running form, inspect selection, or verify drag/metadata round-trips. The corresponding unchecked checks remain pending; decoder tests do not establish actual Windows focus, character suppression, or arrow hold-repeat behavior in mpv.