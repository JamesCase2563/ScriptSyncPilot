# Navigate Between Script Points — Verification

**Status:** In Progress; implementation and verification remain open.
**Issue:** [#53 — Navigate between script points](https://github.com/JamesCase2563/ScriptSyncPilot/issues/53).
See the [plan](PLAN.md) for proposed command semantics and milestones.

## Scope

Verify direct navigation to previous, next, and selected script actions. Preserve timeline zoom and action data, use the existing seek request path, and keep the shortcuts scoped to timeline focus.

## Current behavior and evidence

- `TimelineControl` exposes `PlayheadSeconds`, `SetPlayhead`, and the `SeekRequested` event. Mouse scrubbing raises a seek request; `MainForm.OnSeekRequested` sends it to the current mpv session.
- `TimelineControl` currently handles Delete for the selected action. No next/previous/selected-action navigation method was found.
- `MainForm.OnFormKeyDown` currently maps Left/Right to frame step, Shift+Left/Right to one-second seek, and Ctrl+Left/Right to ten-second seek. It processes those shortcuts only while the timeline itself is focused and mpv is connected.
- `TimelineControl.PreProcessMessage` handles Space for play/pause and the eleven physical top-row position keys. The timeline's Delete and Left/Right inputs are routed as input keys. Navigation must preserve these paths.
- Existing numeric-entry tests cover focused/unfocused timeline key routing, but no point-navigation test was found at baseline.

## M1.1 — Current shortcut and focus-routing audit

Reviewed `MainForm`, `TimelineControl`, and `PatternInsertionDialog` keyboard handlers and the focused/unfocused routing tests in `NumericPositionEntryTests`.

| Existing input | Current routing and effect | Impact on point navigation |
| --- | --- | --- |
| Left / Right | When the timeline itself is focused and mpv is attached, the form routes Left/Right to one-frame stepping. | Preserve; do not reuse these keys. |
| Shift+Left / Shift+Right | Form routes to relative one-second seek while timeline focus and mpv attachment are active. | Preserve; do not reuse. |
| Ctrl+Left / Ctrl+Right | Form routes to relative ten-second seek while timeline focus and mpv attachment are active. | Preserve; do not reuse. |
| Space | Focused timeline preprocesses Space as play/pause and consumes key repeat without repeated toggles. | Preserve. |
| Top-row position keys | Focused timeline maps eleven physical scan codes to positions; modifiers/extended keys are excluded and held repeats are consumed. | Preserve all bindings and focus scope. |
| Delete | Focused timeline handles Delete to remove the selected action. | Preserve. |
| Ctrl+N / Ctrl+O / Ctrl+S | MainForm menu accelerators for New/Open/Save; PatternInsertionDialog forwards these file shortcuts to the owner. | No collision with the proposed point-navigation shortcuts. |
| Ctrl+Z / Ctrl+Y | MainForm menu accelerators for Undo/Redo. | No collision. |
| Ctrl+Up / Ctrl+Down | No application-level handler or menu binding found in the inspected editor code. Up/Down are not currently claimed by `TimelineControl.IsInputKey`. | Available in the main editor when the timeline owns focus, but implementation must explicitly route them to the timeline rather than relying on current input-key behavior. Never handle them while a NumericUpDown or other input owns focus. |
| Enter | No MainForm or TimelineControl shortcut handler found. PatternInsertionDialog has an AcceptButton and standard controls may use Enter for their normal action. | Available in the main editor while the timeline owns focus; scope the selected-point command to that focus and leave dialogs/other controls unchanged. |

**M1.1 result:** No additional main-editor shortcut conflict was found for proposed Ctrl+Up, Ctrl+Down, and Enter. Keep point navigation independent of mpv attachment so it can update the timeline playhead even when `OnSeekRequested` has no session to send to. Ctrl+Up/Down require explicit timeline input routing; Enter must be consumed only in timeline focus. The existing form's arrow-key playback commands already require timeline focus and an attached mpv session. This is a source audit, not a runtime test of the proposed bindings.

## Proposed behavior to verify

| User action | Expected result |
| --- | --- |
| Previous point button or Ctrl+Up | Seek to the closest action strictly earlier than the current playhead. |
| Next point button or Ctrl+Down | Seek to the closest action strictly later than the current playhead. |
| Selected point button or Enter | Seek to the selected action; do nothing if there is no current selection. |
| Navigate before the first or after the last action | No wrap; leave playhead, viewport, selection, script, and history unchanged. |
| Navigate an empty script | No-op. |
| Successful navigation | Set playhead and raise one seek request for the target timestamp; preserve zoom and script data/history. |
| Focus an editable input or another control | Navigation shortcuts are not consumed; the control retains normal keyboard behavior. |
| Use existing Left/Right, Shift/Ctrl+Left/Right, Space, Delete, or position-entry keys | Existing behavior remains unchanged. |

The three point buttons are planned for a compact row directly below the existing time/frame seek row. A visible shortcut hint beside them should use the same style as the current seek-control hint. Disable Previous/Next when no point is available in that direction and Selected when no action is selected; update availability when the playhead, selection, or script changes.

The shortcuts and selection/playhead semantics are proposals from [PLAN.md](PLAN.md). M1.1 found no app-level conflicts for the shortcuts, but final binding and semantics confirmation remain open in M1.

## Automated coverage to add

- Previous/next choose the nearest strictly earlier/later action, including when the playhead equals an action timestamp.
- Selected-action navigation targets the selected timestamp; no selection and stale selection are no-ops.
- Empty scripts and both navigation boundaries are no-ops.
- Successful commands update the playhead and emit exactly one seek request at the expected timestamp.
- Every successful and no-op navigation preserves `PixelsPerSecond`, action contents, selection, and undo/redo history.
- Focused timeline shortcuts route to the matching navigation command; text inputs and other controls are not affected.
- The Previous/Selected/Next buttons and their corresponding shortcuts invoke the same commands. Button enablement tracks selection and eligible actions in each direction.
- The shortcut hint is visible next to the point-navigation row, which sits below the unchanged time/frame seek row.
- Existing playback-step, play/pause, deletion, and physical-position-key tests continue to pass.

Prefer direct `TimelineControl` tests for command semantics and WinForms STA tests for preprocessing/focus routing. Add a form-level test only if required to verify that navigation requests reach `MainForm`'s mpv seek path.

## Planned commands

Run with isolated build output to avoid sharing test binaries with an open editor process:

```powershell
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests" --verbosity minimal
dotnet test src/ScriptSyncPilot.slnx --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal
git diff --check
```

The exact focused filter should be updated to match the final test class/name. These commands have not yet been run for issue #53.

## Manual check

If a Windows editor session is available, check previous/next and selected-point commands with the timeline focused, then focus a text field and verify its normal input behavior. Confirm the displayed playhead seeks to each action while the zoom level remains fixed. Record separately whether a live mpv session was attached; UI event tests alone do not prove external-player transport behavior.

## Results

No implementation, test command, build, or manual check has been performed for issue #53 yet.
