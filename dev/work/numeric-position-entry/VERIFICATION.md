# Numeric Position Entry Verification

**Status:** Planned checks only; implementation and verification have not started. No checkbox is evidence of a passing check until results are recorded below.

See [plan](PLAN.md) and [design](DESIGN.md). The top-row physical map, timeline-only focus scope, modifier exclusions, and repeat suppression are agreed requirements. Future recording behavior is a compatibility contract, not an implemented feature under test here.

## Key Mapping

- [ ] Physical QWERTY top-row `1` through `9` positions insert 10 through 90, regardless of characters produced by the active layout.
- [ ] Physical top-row `0` position inserts 100, not 0.
- [ ] On the user's normal close-to-standard keyboard, all eleven positions map correctly, including left-of-`1`=0%; record the layout tested.
- [ ] Automated decoding checks cover the fixed scan-code map and reject unrelated/extended keys. No per-language mapping tables or exhaustive layout matrix are required.
- [ ] Numpad keys, including Multiply, do not insert points. Num Lock off retains native navigation.
- [ ] Shift/Ctrl/Alt/AltGr combinations, including mixed modifiers, do not insert points. Shift is reserved for future recording minimums, not a second point-entry binding.

## Insertion and History

- [ ] A new document accepts a point at the current playhead with no mpv or device attached.
- [ ] Insertion uses the captured playhead timestamp and existing timeline clamp/rounding behavior without moving the playhead.
- [ ] A missing timestamp inserts in sorted order and preserves all unrelated actions.
- [ ] An existing timestamp changes only its position; no duplicate timestamp is added.
- [ ] An identical timestamp/value is a no-op: no history entry, dirty change, ScriptChanged event, or redo invalidation.
- [ ] A real insertion/replacement creates exactly one history entry and one ScriptChanged event, and selects the affected point.
- [ ] Undo/Redo restore a sequence of keyboard and mouse edits in order, retaining metadata and saved clean/dirty state.
- [ ] After Undo, a real numeric edit clears Redo while identical numeric input preserves it.
- [ ] Keyboard insertion safely finalizes an existing drag using the established commit boundary.
- [ ] Saving/reloading preserves positions, timestamps, and root JSON properties.

## Manual Interaction

- [ ] With the timeline focused and video playing, successive presses create points at successive media timestamps.
- [ ] While paused, repeated different positions replace the action at that same playhead timestamp.
- [ ] Digits and OEM keys retain native behavior in text/numeric fields, combo boxes, and the modeless pattern dialog.
- [ ] File pickers, error messages, and other modal windows do not insert points into the editor.
- [ ] Existing file shortcuts, Undo/Redo, Delete, and playback controls continue to work.
- [ ] Accepted keys are consumed once, without duplicate routing, unwanted beeps, or triggering another control.
- [ ] Holding a position key inserts once; release/repress inserts again. Changing modifiers while it remains held does not create another command.
- [ ] Focus loss and New/Open do not leave held-key tracking stuck or admit held-key repeat messages as fresh commands.
- [ ] Manual trial on the user's normal keyboard confirms all eleven physical positions and modifier handling. Norwegian/US layout-switch smoke testing is optional where readily available, not a completion gate.

## Required Gates

Run focused automated checks for mapping, accepted modifiers, insertion/replacement/no-op behavior, and history wherever existing test infrastructure can exercise them. Do not claim a Common model test proves WinForms focus or physical-layout behavior. Keep new test infrastructure proportional to the editor-owned risk.

Build FunscriptEditor after implementation. If Common changes, run its persistence/history regressions and build both desktop consumers. Record exact commands and results; builds alone do not verify key mapping or interaction behavior.

Complete the applicable manual checks above and record rejected/deferred options explicitly. Update the permanent editor architecture/backlog only to behavior actually implemented and verified.

## Results and Limitations

Planning documents created on 2026-10-10 and updated to the agreed fixed top-row physical map, timeline-only focus scope, modifier exclusions, and held-key repeat suppression. Assume Latin-like layouts with close-to-standard PC keyboard geometry; universal language/hardware support and rebinding are not required. No code changes, automated test runs, or manual feature checks performed for this work item. Numpad insertion is outside scope; implementation and focused verification remain to be completed.