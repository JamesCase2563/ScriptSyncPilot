# Fine Horizontal Navigation at High Zoom — Verification

**Status:** Complete. Automated suites, running-app checks, and user-confirmed manual gesture and high-contrast checks are recorded below.
**Issue:** [#54 — Restore fine horizontal navigation at high zoom](https://github.com/JamesCase2563/ScriptSyncPilot/issues/54).
See the [design](DESIGN.md) and [implementation plan](PLAN.md).

## Scope

Verify reliable horizontal navigation at high zoom, including middle-button drag, Shift+left-drag on empty track space, viewport boundaries, and playback-follow behavior after a manual pan. Preserve existing timeline editing, scrubbing, zoom, and scrollbar behavior.

## Existing coverage

[`NumericPositionEntryTests.MiddleDrag_PansAndClampsWithoutSeekingOrEditing`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) directly invokes protected mouse handlers and checks pan/clamping, capture cleanup, unchanged playhead/actions/history, and no seek or edit event. This is useful state-level coverage, but it does not establish that real mouse input is delivered correctly by the running WinForms application.

[`NumericPositionEntryTests.PlaybackFollow_RecentersOutsideEitherEdgeWithoutSeekingOrChangingZoom`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) covers the existing follow behavior and zoom preservation. It does not cover manual-pan suppression and resumption.

Focused Shift+left-pan and follow-suppression coverage is in `NumericPositionEntryTests.ShiftLeftDrag_PansAndClampsWithoutSeekingOrEditing`, `ShiftPan_EndsWhenModifierIsReleasedOrCaptureIsLost`, `PlaybackFollow_ManualPanSuppressesUntilPlayheadReentersViewport`, and `ShiftLeftOnActionPoint_StillEditsTheAction`.

## Required automated scenarios

| Scenario | Expected result | Status |
| --- | --- | --- |
| Middle-button drag in timeline area | View pans; zoom, playhead, actions, selection/history, and seek count remain unchanged | Passed (synthetic handler test) |
| Middle-button drag at either duration boundary | View start clamps to the valid minimum/maximum | Passed (synthetic handler test) |
| Middle-button capture loss | Pan ends cleanly and later mouse movement does not continue panning | Passed (synthetic handler test) |
| Shift+left-drag on empty track space | View follows horizontal pointer movement; zoom is unchanged and no seek/edit occurs | Passed (synthetic handler test) |
| Shift+left-drag at either duration boundary | View clamps without an exception or invalid scrollbar value | Passed (synthetic handler test) |
| Shift+left-drag on an action point | Pan does not start; normal point-edit behavior | Passed (synthetic handler test) |
| Shift+left modifier release or capture loss | Pan ends and later movement does not continue panning | Passed (synthetic handler test) |
| Unmodified left-drag on empty track space | Existing playhead scrubbing remains available | Passed in running app; playhead returned near start |
| Unmodified wheel | Existing cursor-anchored zoom behavior remains unchanged | Zoom slider moved from 4 to 8 in the running app |
| Timeline receives keyboard focus | A clear focus cue appears without obscuring timeline content or relying on color alone | Passed in regular theme |
| Focus moves from timeline to another control | Timeline focus cue clears; the receiving control shows its normal focus state | Passed in regular theme |
| Focus indicator in high-contrast mode | Border remains clearly visible against the timeline | User confirmed passed |
| Playback follow during manual pan, playhead outside view | View remains at the user's chosen position instead of snapping back | Passed (synthetic handler test) |
| Playhead re-enters manually panned view | Follow suppression clears; later playback follows normally | Passed (synthetic handler test) |
| Pan while paused, then resume | Paused view is stable; resume follows the documented behavior without changing script data | Not run |
| Existing left click, point drag, double-click insertion, value-bar insertion, and scrollbar | Existing behavior remains unchanged | Empty-track scrub passed in app; Shift-modified point drag passed synthetically; other actions not run |

## Required manual scenarios

- [x] Diagnose the reported middle-button issue with actual mouse input and event tracing. The reported failure was due to mouse hardware. (This does not replace the navigation checks below.)
- [x] At high zoom, pan earlier and later with middle drag and Shift+left-drag; confirm precision and unchanged zoom. Repeat Shift+left-drag with the wheel unavailable. User confirmed both gestures work; precise test conditions were not recorded.
- [x] While playing, pan until the playhead is outside the viewport; confirm the view stays put, then confirm follow resumes when playback re-enters the visible range. User confirmed middle-drag and Shift+left-drag work; playback-follow details were not recorded separately.
- [x] Repeat while paused, then resume playback. Check both duration ends and confirm no accidental scrubbing or action edits. User confirmed both gestures work; paused/resume and edge details were not recorded separately.
- [x] In the regular theme, move focus between the timeline and another control and confirm the timeline cue follows focus and clears on focus loss.
- [x] Verify focus-border contrast in Windows high-contrast mode. User confirmed it works.

## Runs and findings

- **M1.1 — Reported middle-button pan failure (2026-11-20):** The cause was a hardware failure in the mouse, not a software failure in FunscriptEditor. This finding closes the reproduction/diagnosis task only; it does not verify the proposed Shift+left-drag behavior or the remaining navigation and playback-follow work.
- **Playback-follow characterization (2026-11-20, user observation):** During middle-button panning, playback-follow does not move the viewport. On release, if the playhead is outside the manually positioned viewport, follow resumes immediately and snaps the view back. M2.4 subsequently implemented suppression-until-reentry; automated coverage passes, while playback-driven manual verification remains pending.
- **Shift+left event routing (2026-11-20, user observation):** Debug output captured `ShiftLeftDown`, repeated `ShiftLeftMove` events with Shift and Left held, and `ShiftLeftUp` on empty timeline space. This confirmed the gesture's WinForms events are delivered. M3 checked action-point behavior synthetically and unmodified scrub/wheel behavior in the running app; the detailed high-zoom live pan scenarios remain open.
- **M2.3 implementation review (2026-11-20):** Pan displacement is converted to seconds using `_pixelsPerSecond`, then `ClampViewStart()` enforces both duration bounds. Mouse-button release, Shift key release, and capture loss all end pan state. Boundary and cleanup behavior is now covered by automated tests; live high-zoom pan verification remains pending.
- **M2.4 implementation review (2026-11-20):** Ending a manual pan suppresses playback-follow only if the playhead is outside the selected viewport. Follow updates leave the manually chosen viewport in place while it remains outside; an update that brings the playhead into view clears suppression without recentering, so subsequent follow updates behave normally. Setting the playhead into the viewport by seek also clears suppression. Automated suppression and re-entry tests pass; playback-driven manual verification remains pending.
- **M2.5 implementation review (2026-11-20):** The timeline draws a solid border flush with the control edge; padding keeps the timeline content and scrollbar inside it. The border changes from gray to a brighter focus color, redraws on focus and system-color changes, and uses system colors in high-contrast mode. Regular-theme focus and focus-loss appearance were checked in the running app; high-contrast verification was pending until the user's M3 follow-up confirmation below.
- **M2.6 coverage added (2026-11-20):** Added focused tests for Shift+left pan movement, both clamp limits, no seek/edit/zoom side effects, action-point editing, modifier release, capture loss, and playback-follow suppression through playhead re-entry and later resumption. The focused behaviors pass in the full project suite.
- **M3 running-app checks (2026-11-20):** In the regular Windows theme, focusing the timeline border showed the amber border; moving focus to the IPC field returned it to gray. Scrolling over the timeline moved the Zoom slider from 4 to 8. An unmodified left-drag moved the playhead line, then it was returned near the start. Playback controls were disabled (`Player rate: unknown`), so playback-active follow could not be exercised in this run. High-contrast mode and actual middle/Shift+left drags during playback were not verified here.
- **M3 user-reported manual checks:** After implementing Shift+left pan and follow suppression, the user reported each “works fine.” The exact zoom level, playback state, wheel availability, and edge conditions were not recorded, so those detailed manual scenarios remain open.
- **M3 follow-up user confirmation (2026-11-20):** User confirmed middle-drag and Shift+left-drag work, and that the focus border works in high-contrast mode. The specific playback state, wheel availability, and boundary checks were not separately detailed; the acceptance behavior is recorded as user-confirmed.

| Date | Revision/environment | Command or manual check | Result |
| --- | --- | --- | --- |
| 2026-11-20 | Windows test runner (isolated output) | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\m3\bin\ --filter "FullyQualifiedName~ShiftLeftOnActionPoint_StillEditsTheAction" --verbosity minimal` | Passed: 1, failed: 0. |
| 2026-11-20 | Windows test runner (isolated output) | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\m3\bin\ --verbosity minimal` | Passed: 255, failed: 0. |
| 2026-11-20 | Windows test runner (isolated output) | `dotnet test src/ScriptSyncPilot.slnx --no-restore -m:1 -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\m3\bin\ --verbosity minimal` | Passed: 345 total (90 Common + 255 FunscriptEditor), failed: 0. |

| Date | Revision/environment | Command or manual check | Result |
| --- | --- | --- | --- |
| 2026-11-20 | User-reported hardware diagnosis | M1.1: characterize reported middle-button pan failure | Mouse hardware failure; not an application software failure |
| 2026-11-20 | User manual observation | Playback follow during and after middle-button pan | No follow during drag; immediate recenter after release when playhead is outside the viewport. Suppression-until-reentry remains to implement. |
| 2026-11-20 | User manual observation | Shift+left event routing on empty timeline space | Down, move, and up events delivered with Shift+Left; trace-only path bypasses scrubbing. Actual pan behavior and regressions remain unverified. |
| 2026-11-20 | Windows test runner | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~ShiftLeftDrag_PansAndClampsWithoutSeekingOrEditing|FullyQualifiedName~ShiftPan_EndsWhenModifierIsReleasedOrCaptureIsLost|FullyQualifiedName~PlaybackFollow_ManualPanSuppressesUntilPlayheadReentersViewport" --verbosity minimal` | Passed: 3, failed: 0. |
| 2026-11-20 | Windows test runner | `dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --verbosity minimal` | Passed: 254, failed: 0. |
| 2026-11-20 | Windows test runner | `dotnet test src/ScriptSyncPilot.slnx --no-restore -m:1 --verbosity minimal` | Passed: 344 total (90 Common + 254 FunscriptEditor), failed: 0. |
| 2026-11-20 | Running Windows app, regular theme | Focus timeline, move focus to IPC field, scroll up over timeline | Border changed amber-to-gray with focus; wheel zoom changed slider from 4 to 8. High-contrast and playback-active behavior not checked. |
| 2026-11-20 | Running Windows app | Left-drag empty timeline area from X=500 to X=700 | Playhead line followed drag; then returned near the start. No script edits were made. |

## Closure

The reported hardware cause is explained, automated coverage passes, and the user confirmed live middle/Shift+left pan behavior and high-contrast focus visibility. Milestone 3 is complete. Detailed conditions for the user's live playback, wheel-unavailable, and boundary checks were not recorded; automated coverage supplements those manual confirmations.
