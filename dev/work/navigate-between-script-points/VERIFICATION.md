# Navigate Between Script Points — Verification

**Status:** Baseline recorded; implementation and verification have not started.
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

## Proposed behavior to verify

| User action | Expected result |
| --- | --- |
| Previous action (proposed Ctrl+Up) | Seek to the closest action strictly earlier than the current playhead. |
| Next action (proposed Ctrl+Down) | Seek to the closest action strictly later than the current playhead. |
| Selected action (proposed Enter) | Seek to the selected action; do nothing if there is no current selection. |
| Navigate before the first or after the last action | No wrap; leave playhead, viewport, selection, script, and history unchanged. |
| Navigate an empty script | No-op. |
| Successful navigation | Set playhead and raise one seek request for the target timestamp; preserve zoom and script data/history. |
| Focus an editable input or another control | Navigation shortcuts are not consumed; the control retains normal keyboard behavior. |
| Use existing Left/Right, Shift/Ctrl+Left/Right, Space, Delete, or position-entry keys | Existing behavior remains unchanged. |

The shortcuts and selection/playhead semantics are proposals from [PLAN.md](PLAN.md); confirm them against issue scope and the complete UI before implementation.

## Automated coverage to add

- Previous/next choose the nearest strictly earlier/later action, including when the playhead equals an action timestamp.
- Selected-action navigation targets the selected timestamp; no selection and stale selection are no-ops.
- Empty scripts and both navigation boundaries are no-ops.
- Successful commands update the playhead and emit exactly one seek request at the expected timestamp.
- Every successful and no-op navigation preserves `PixelsPerSecond`, action contents, selection, and undo/redo history.
- Focused timeline shortcuts route to the matching navigation command; text inputs and other controls are not affected.
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
