# Numeric Position Entry Plan

**Status:** In progress; shared timeline insertion, keyboard routing, fixed scan-code mapping, and position-key repeat suppression implemented. Automated behavior tests and manual verification remain.
**Selected:** 2026-10-10.
**Affected projects:** FunscriptEditor (keyboard routing and timeline insertion); Common only if a genuinely shared model change is needed. HandySync behavior is outside scope.

Source: [editor backlog](../../FunscriptEditor/BACKLOG.md). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Author individual movement points at the current playhead by pressing position keys while watching or stepping through video. Keep each change undoable and preserve native editing behavior in input controls.

## Scope

- Use physical scan-code positions for all eleven top-row position keys, independent of the active language layout. Labels below refer to their familiar QWERTY positions, not typed characters.
- Top-row `1` through `9` insert 10% through 90%; top-row `0` inserts 100%.
- The physical key immediately left of top-row `1` inserts 0%: the key labeled with a vertical bar on Norwegian keyboards and a grave accent on US keyboards.
- Top-row mapping only; numpad insertion is outside this version.
- Insert at the current timeline playhead without requiring mpv or a device connection.
- Replace the value of an existing action at the captured timestamp; otherwise insert in timestamp order.
- Reuse timeline commit/history, dirty-state, selection, and refresh behavior. Identical timestamp/value input is a no-op.
- Activate point entry only while the timeline has focus. Shift/Ctrl/Alt/AltGr combinations do not insert points; native text/numeric editing remains untouched.
- Ignore held-key repeats only for mapped position-entry keys until release; each new physical press can insert one point. Existing arrow navigation, including Shift/Ctrl seeking, retains hold-repeat functionality.

Live pattern generation, physical Q-P-row cycle presets, Shift+position-key min/max controls, playback-speed controls, markers/snapping, numpad insertion, hardware rebinding UI, and multitrack editing are outside this change. Numeric entry records individual points; it does not infer a frequency or generate intervening cycles. Future recording must reuse the same physical-key-to-position map: unmodified keys set maximum, Shift sets minimum, and Ctrl/Alt/AltGr do neither.

## Agreed Decisions

- [x] Use the same physical position map in point-entry and future recording modes, independent of language layout.
- [x] Use top-row keys only in this version; do not add numpad bindings.
- [x] Activate only while the timeline has focus; broader form/dialog routing is outside scope.
- [x] Ignore held-key repeats for position entry only; release/repress permits another command. Preserve existing navigation hold-repeat.
- [x] Point entry accepts unmodified keys only. Shift is reserved for recording minimums; Ctrl/Alt/AltGr combinations are excluded in every authoring mode.

Assume Latin-like layouts and close-to-standard PC keyboard geometry. Use a fixed physical-key map and verify it on the user's normal keyboard; no exhaustive language matrix, unusual-hardware adaptation, or rebinding work is required.

## Milestones

### 1. Design

- [x] Record the agreed top-row physical mapping, modifier roles, and repeat policy.
- [x] Define insertion, no-op, history, and focus contracts in DESIGN.md.
- [x] Resolve initial keyboard and focus scope before implementation.

### 2. Implementation

- [x] Add a timeline position-insertion command using the existing mouse insertion/commit path.
- [x] Route accepted keyboard input once, preserving modifiers and native control behavior.
- [x] Implement scan-code handling for all eleven position keys, independent of language layout.
- [x] Suppress mapped position-key repeats without filtering navigation repeats. Use the message's previous-key-state flag, with no local held-key state to reset on focus loss/document switch.
- [ ] Add focused automated tests using existing test infrastructure where suitable.

### 3. Verification and Completion

- [ ] Run the checks in VERIFICATION.md and build affected projects.
- [ ] Record manual results on the user's normal keyboard/layout, focus behavior, and playback authoring; optionally smoke-check Norwegian/US layout switching if readily available.
- [ ] Update the permanent editor architecture and backlog to the implemented mapping.
- [ ] Record limitations and the PR reference; return deferred ideas to the backlog.
- [ ] Archive the completed folder under `dev/archive/YYYY-MM-numeric-position-entry/` and update links.

## Outcome

`TimelineControl.InsertPositionAtPlayhead` is shared with value-bar insertion. Focused timeline keyboard routing now uses fixed top-row scan codes, excludes modifiers/extended keys, and suppresses position-key repeats. The editor build passes; automated behavior tests and manual verification remain pending. PR reference: not recorded.