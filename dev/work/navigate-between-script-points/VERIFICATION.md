# Navigate Between Script Points — Verification

**Status:** Complete; automated verification passed. No interactive editor/mpv smoke check was performed in this session.
**Issue:** [#53 — Navigate between script points](https://github.com/JamesCase2563/ScriptSyncPilot/issues/53).
See the [plan](PLAN.md) for confirmed command semantics and milestones.

## Scope

Verify direct navigation to previous, next, and selected script actions. Preserve timeline zoom and action data, use the existing seek request path, and keep the shortcuts scoped to timeline focus.

## Initial baseline (before implementation)

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
| Ctrl+N / Ctrl+O / Ctrl+S | MainForm menu accelerators for New/Open/Save; PatternInsertionDialog forwards these file shortcuts to the owner. | No collision with the confirmed point-navigation shortcuts. |
| Ctrl+Z / Ctrl+Y | MainForm menu accelerators for Undo/Redo. | No collision. |
| Ctrl+Up / Ctrl+Down | No application-level handler or menu binding found in the inspected editor code. Up/Down are not currently claimed by `TimelineControl.IsInputKey`. | Available in the main editor when the timeline owns focus, but implementation must explicitly route them to the timeline rather than relying on current input-key behavior. Never handle them while a NumericUpDown or other input owns focus. |
| Enter | No MainForm or TimelineControl shortcut handler found. PatternInsertionDialog has an AcceptButton and standard controls may use Enter for their normal action. | Available in the main editor while the timeline owns focus; scope the selected-point command to that focus and leave dialogs/other controls unchanged. |

**M1.1 result:** No additional main-editor shortcut conflict was found for Ctrl+Up, Ctrl+Down, and Enter. Keep point navigation independent of mpv attachment so it can update the timeline playhead even when `OnSeekRequested` has no session to send to. Ctrl+Up/Down require explicit timeline input routing; Enter must be consumed only in timeline focus. The existing form's arrow-key playback commands already require timeline focus and an attached mpv session. This is a source audit, not a runtime test of the bindings.

## M1.2 — Next/previous reference and comparison

Confirmed the intended rule: next and previous navigation are relative to `TimelineControl.PlayheadSeconds`, never relative to the selected action. Compare action timestamps strictly against the playhead time:

- Previous chooses the action with the greatest timestamp **less than** the playhead.
- Next chooses the action with the least timestamp **greater than** the playhead.
- If the playhead is exactly on an action timestamp, that action is skipped by both commands. The previous command chooses the preceding action; next chooses the following action.
- If the playhead is between actions, previous chooses the lower timestamp and next chooses the higher timestamp.
- Selection is used only by the separate Selected point command.

The comparison should use the actual playhead time at sufficient precision against action timestamps in milliseconds; do not round the playhead to a millisecond before deciding which side an action is on. M1.2 is a confirmed behavior contract; code and automated tests remain for M2.

## M1.3 — Boundaries and no-selection behavior

Confirmed no-wrap behavior for next/previous navigation. A command with no eligible action is a no-op: it does not move the playhead, change the viewport or zoom, alter selection, edit the script, or add history. The corresponding Previous/Next button is disabled whenever no eligible action exists in its direction.

- Before the first action, Previous is disabled/no-op; Next targets the first action.
- After the last action, Next is disabled/no-op; Previous targets the last action.
- Exactly on the first action, Previous is disabled/no-op; Next targets the following action if one exists.
- Exactly on the last action, Next is disabled/no-op; Previous targets the preceding action if one exists.
- With one action, both direction buttons are disabled when the playhead is exactly on it; on either side, only the direction with an eligible action is enabled.
- With an empty script, all three point-navigation buttons are disabled and every point-navigation command is a no-op.
- With no selected action, Selected is disabled and Enter does nothing. The Selected point button and Enter become available only while a current action is selected; both invoke the same selected-action command.

These are confirmed behavior decisions for M1.3. Automated implementation and regression checks remain for M2.

## M1.4 — Shortcut bindings

Confirmed the planned bindings without an alternative:

- **Ctrl+Up:** Previous point.
- **Ctrl+Down:** Next point.
- **Enter:** Selected point.

These commands are available only while the timeline itself has focus. They must not be handled while a text/numeric input, dialog, or other control owns focus. The bindings are independent of mpv attachment so the playhead can still move locally; the existing seek handler sends the request only when a session is available. Keep the displayed shortcut hint beside the point-navigation buttons and ensure button clicks use the same operations.

The source audit found no conflicting main-editor command. The timeline does not currently claim Up/Down as input keys, so implementation must explicitly route Ctrl+Up/Down and test WinForms focus behavior. M1.4 confirms the design choices; it does not yet verify the implemented routing.

## M1.5 — Point-navigation controls layout

Confirmed with the user: put **Previous point**, **Selected point**, and **Next point** in a compact row immediately below the existing time/frame seek controls. Keep the existing seek row unchanged. Show the three shortcuts beside the point buttons in a subdued hint styled like the existing seek-shortcut hint. The proposed hint is `Shortcuts: Ctrl+Up = previous, Ctrl+Down = next, Enter = selected`.

Button and shortcut actions must call the same commands. The button row and hint are planned layout requirements; visual rendering and button enablement remain to be verified during M2/M3 implementation.

## Confirmed behavior to verify

| User action | Expected result |
| --- | --- |
| Previous point button or Ctrl+Up | Seek to the closest action strictly earlier than the current playhead. |
| Next point button or Ctrl+Down | Seek to the closest action strictly later than the current playhead. |
| Selected point button or Enter | Seek to the selected action; do nothing if there is no current selection. |
| Previous before first / Next after last | No wrap; button disabled and keyboard command is a no-op, leaving playhead, viewport, zoom, selection, script, and history unchanged. |
| Navigate an empty script | All point buttons disabled; commands are no-ops. |
| Selected point with no selection | Selected button disabled; Enter is a no-op. |
| Successful navigation | Set playhead and raise one seek request for the target timestamp; preserve zoom and script data/history. |
| Focus an editable input or another control | Navigation shortcuts are not consumed; the control retains normal keyboard behavior. |
| Use existing Left/Right, Shift/Ctrl+Left/Right, Space, Delete, or position-entry keys | Existing behavior remains unchanged. |

The three point buttons are planned for a compact row directly below the existing time/frame seek row. A visible shortcut hint beside them should use the same style as the current seek-control hint. Disable Previous/Next when no point is available in that direction and Selected when no action is selected; update availability when the playhead, selection, or script changes.

The shortcut and selection/playhead semantics are confirmed in [PLAN.md](PLAN.md) and M1.1–M1.4 above. Runtime behavior remains to be implemented and verified.

## Automated coverage to add

- Previous/next choose the nearest strictly earlier/later action, including when the playhead equals an action timestamp.
- Previous/next behavior is based on playhead time even when another point is selected; add cases for exact timestamp and between-action playhead positions.
- Selected-action navigation targets the selected timestamp; no selection is a no-op and the button is disabled.
- Empty scripts, both navigation boundaries, first/last action positions, and a one-action script have the specified no-wrap/button enablement behavior.
- Successful commands update the playhead and emit exactly one seek request at the expected timestamp.
- Every successful and no-op navigation preserves `PixelsPerSecond`, action contents, selection, and undo/redo history.
- Focused timeline shortcuts route to the matching navigation command; text inputs and other controls are not affected.
- The Previous/Selected/Next buttons and their corresponding shortcuts invoke the same commands. Button enablement tracks selection and eligible actions in each direction.
- The shortcut hint is visible next to the point-navigation row, which sits below the unchanged time/frame seek row.
- Existing playback-step, play/pause, deletion, and physical-position-key tests continue to pass.

Prefer direct `TimelineControl` tests for command semantics and WinForms STA tests for preprocessing/focus routing. Add a form-level test only if required to verify that navigation requests reach `MainForm`'s mpv seek path.

## M2.1 — Timeline navigation operations

Implemented three public timeline commands: `NavigateToPreviousAction`, `NavigateToNextAction`, and `NavigateToSelectedAction`. Previous/Next compare sorted action timestamps directly against the current playhead using the confirmed strict rule. A successful command sets the playhead to the target timestamp and raises one `SeekRequested` event; it does not edit the script, change zoom, alter selection, or add history. Each command returns `false` without changing state or raising a seek request when there is no target.

`TimelineNavigationTests` verifies strict previous/next navigation at exact and fractional playhead positions, no-wrap boundaries, empty scripts, selected-action navigation, no-selection behavior, seek event counts, and preservation of zoom/script/history.

## M2.2 — Focused shortcut routing

`TimelineControl.IsInputKey` now claims Ctrl+Up, Ctrl+Down, and unmodified Enter so WinForms routes these keys to the timeline. `OnKeyDown` invokes the matching operation only when the timeline is focused, consumes the key even when there is no eligible target, and suppresses the keypress. Other modifier combinations are left alone. This keeps navigation independent of mpv attachment and avoids consuming keys in text/numeric inputs or dialogs, which do not route their keys through the timeline.

The new `TimelineNavigationTests.FocusedTimeline_RoutesConfirmedShortcutsWithoutMpv` checks the three bindings, no-mpv operation, no-selection Enter behavior, exact modifier acceptance, and continued Left input-key routing. `UnfocusedTimeline_DoesNotConsumePointShortcuts` gives a sibling text field focus and verifies synthetic keydown calls do not navigate or mark keys handled. Existing `NumericPositionEntryTests` continue to cover physical position keys and Space/play-pause routing.

## M2.5 — Point-navigation buttons and availability

Added Previous point, Selected point, and Next point buttons in a compact row directly below the existing time/frame seek row, with the agreed shortcut hint. Each button uses the same public timeline navigation operation as its shortcut. `TimelineControl` exposes matching availability properties and raises `NavigationAvailabilityChanged` when the playhead, selection, or script changes; `MainForm` updates button enabled states from those properties. Previous/Next availability follows the same strict timestamp comparisons as the commands, while Selected is enabled only when a valid action is selected.

`TimelineNavigationTests.PointNavigationButtons_TrackShortcutAvailabilityAndSitBelowSeekRow` covers initial states, exact-timestamp availability, selection, the end boundary, reset after loading an empty script, and placement below the seek row. Button-click command parity and shortcut-hint visibility are covered in M2.6 below.

## M2.6 — Button and shortcut parity

`TimelineNavigationTests.PointNavigationButtons_MatchShortcutDestinationsAndShowHint` clicks each button and then invokes its matching shortcut from the same starting state. Previous, Next, and Selected each reach the same timestamp by both routes and emit one seek request per action. The form-level test also confirms that the visible shortcut hint names Ctrl+Up, Ctrl+Down, and Enter.

## Follow-up — Repeated navigation with mpv attached

The timeline commands already advance strictly from their current playhead. The editor now also protects that local target from stale mpv `time-pos` notifications while an absolute seek is pending: notifications are applied once they reach or pass the target, with a 5 ms acknowledgment tolerance, or after a 2 second fallback timeout. This prevents an older reported position from resetting the playhead between repeated point-navigation commands. `TimelineNavigationTests.RepeatedPreviousAndNext_NavigateOnePointPerCommand` covers repeated commands in both directions; `MpvSessionReplacementTests.Editor_RecognizesWhenMpvPositionHasReachedPendingSeekTarget` covers stale and acknowledged forward/backward positions.

## Planned commands

Run with isolated build output to avoid sharing test binaries with an open editor process:

```powershell
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests" --verbosity minimal
dotnet test src/ScriptSyncPilot.slnx --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal
dotnet build src/ScriptSyncPilot.slnx --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal
git diff --check
```

All four commands were run; the .NET commands used isolated Release output under `.verification/issue-53/bin/`.

## Manual check

No interactive editor/mpv session was used for this verification. The user previously asked that computer control stop, so this session stayed with automated WinForms coverage and did not launch or operate the app UI. No live mpv seek was exercised. The form-level button test confirms the hint is visible and button commands raise the same `SeekRequested` requests as shortcuts; it does not verify transport against an attached mpv process.

## M3 — Verification and acceptance mapping

| Issue #53 acceptance criterion | Evidence | Result |
| --- | --- | --- |
| Navigate to next, previous, and selected points. | `TimelineNavigationTests` covers each public operation and `PointNavigationButtons_MatchShortcutDestinationsAndShowHint` covers the matching buttons and shortcuts. | Passed. |
| Seek the playhead to the target without changing zoom. | Operation tests assert target playhead time and unchanged `PixelsPerSecond`; successful commands raise a seek request. | Passed. |
| Specify shortcuts and whether next/previous follow playhead or selection. | [PLAN.md](PLAN.md) records Ctrl+Up / Ctrl+Down / Enter; M1.2 records strict playhead-relative comparisons. | Specified and tested. |
| Define no-selection and first/last-point behavior. | M1.3 defines no-op/no-wrap behavior; `TimelineNavigationTests` covers no selection, empty script, and both boundaries. | Defined and tested. |
| Avoid conflicts with existing controls and focused inputs. | Shortcut routing tests verify exact modifiers and focused-only behavior; the sibling TextBox case preserves input focus. Existing position-entry and playback routing tests passed in the full suite. | Automated coverage passed; no interactive smoke test. |
| Repeated next/previous commands advance after an mpv seek. | Repeated navigation test plus pending-seek position guard tests; the full FunscriptEditor test project passed after this follow-up. | Passed in automated coverage; no live mpv session was used. |

No acceptance-criterion defect remains open. The remaining limitation is that no live mpv transport or manual visual interaction was exercised in this session.

## Results

M2.1–M2.6 navigation operations, shortcut routing, buttons, enabled-state synchronization, button/shortcut parity, and shortcut-hint visibility are implemented. Focused tests, the full solution Release suite, Release build, and diff check passed. Manual UI/mpv interaction was not performed; see the limitation above.

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests" --verbosity minimal` | Passed: 4/4 focused timeline navigation tests. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests|FullyQualifiedName~NumericPositionEntryTests" --verbosity minimal` | Passed: 21/21; includes all six `TimelineNavigationTests` and existing position-entry and playback-shortcut routing coverage. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests" --verbosity minimal` | Passed: 7/7; includes button enabled-state updates and row placement. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests" --verbosity minimal` | Passed: 8/8; includes all M2 navigation tests, button enabled-state/layout, click/shortcut parity, and visible shortcut hint. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --filter "FullyQualifiedName~TimelineNavigationTests|FullyQualifiedName~Editor_RecognizesWhenMpvPositionHasReachedPendingSeekTarget" --verbosity minimal` | Passed: 10/10; covers repeated next/previous commands and stale/acknowledged mpv position handling. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal` | Passed: 272/272 FunscriptEditor tests after the repeated-navigation follow-up; 0 failed, 0 skipped. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet test src/ScriptSyncPilot.slnx --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal` | Passed: 364 total — 94 Common tests and 270 FunscriptEditor tests; 0 failed, 0 skipped. |
| 2026-11-22 | Windows; .NET 10; isolated Release output under `.verification/issue-53/bin/` | `dotnet build src/ScriptSyncPilot.slnx --configuration Release --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\issue-53\bin\ --verbosity minimal` | Succeeded: 0 warnings, 0 errors. |
| 2026-11-22 | Windows; repository working tree | `git diff --check` | Passed; no whitespace errors. |
| 2026-11-22 | This session | Interactive Windows editor/mpv smoke check | Not performed; no live mpv seek was exercised. Automated WinForms tests cover focus routing, button/shortcut parity, and visible hint. |
