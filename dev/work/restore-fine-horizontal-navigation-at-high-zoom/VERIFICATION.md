# Fine Horizontal Navigation at High Zoom — Verification

**Status:** Not started. No implementation or verification results are recorded yet.
**Issue:** [#54 — Restore fine horizontal navigation at high zoom](https://github.com/JamesCase2563/ScriptSyncPilot/issues/54).
See the [design](DESIGN.md) and [implementation plan](PLAN.md).

## Scope

Verify reliable horizontal navigation at high zoom, including middle-button drag, Shift+left-drag on empty track space, viewport boundaries, and playback-follow behavior after a manual pan. Preserve existing timeline editing, scrubbing, zoom, and scrollbar behavior.

## Existing coverage

[`NumericPositionEntryTests.MiddleDrag_PansAndClampsWithoutSeekingOrEditing`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) directly invokes protected mouse handlers and checks pan/clamping, capture cleanup, unchanged playhead/actions/history, and no seek or edit event. This is useful state-level coverage, but it does not establish that real mouse input is delivered correctly by the running WinForms application.

[`NumericPositionEntryTests.PlaybackFollow_RecentersOutsideEitherEdgeWithoutSeekingOrChangingZoom`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs) covers the existing follow behavior and zoom preservation. It does not cover manual-pan suppression and resumption.

## Required automated scenarios

| Scenario | Expected result | Status |
| --- | --- | --- |
| Middle-button drag in timeline area | View pans; zoom, playhead, actions, selection/history, and seek count remain unchanged | Not run |
| Middle-button drag at either duration boundary | View start clamps to the valid minimum/maximum | Not run |
| Middle-button capture loss | Pan ends cleanly and later mouse movement does not continue panning | Not run |
| Shift+left-drag on empty track space | View follows horizontal pointer movement; zoom is unchanged and no seek/edit occurs | Not run |
| Shift+left-drag at either duration boundary | View clamps without an exception or invalid scrollbar value | Not run |
| Shift+left-drag on an action point | Existing point editing remains available; pan does not start | Not run |
| Unmodified left-drag on empty track space | Existing playhead scrubbing remains available | Not run |
| Unmodified wheel | Existing cursor-anchored zoom behavior remains unchanged | Not run |
| Timeline receives keyboard focus | A clear focus cue appears without obscuring timeline content or relying on color alone | Not run |
| Focus moves from timeline to another control | Timeline focus cue clears; the receiving control shows its normal focus state | Not run |
| Playback follow during manual pan, playhead outside view | View remains at the user's chosen position instead of snapping back | Not run |
| Playhead re-enters manually panned view | Follow suppression clears; later playback follows normally | Not run |
| Pan while paused, then resume | Paused view is stable; resume follows the documented behavior without changing script data | Not run |
| Existing left click, point drag, double-click insertion, value-bar insertion, and scrollbar | Existing behavior remains unchanged | Not run |

## Required manual scenarios

- Reproduce the original middle-button failure in the Windows app and verify the corrected path with an actual mouse.
- At high zoom, pan earlier and later with middle drag and Shift+left-drag; check precision and that pan does not zoom. Repeat Shift+left-drag with the wheel unavailable.
- While playing, pan far enough that the playhead is outside the viewport; confirm the view does not immediately jump back. Let playback reach the visible range and confirm follow resumes.
- Repeat while paused, then resume playback. Check both ends of the script and verify no accidental scrubbing or action edits.
- Move focus between the timeline and standard controls using mouse and keyboard navigation; confirm the timeline focus cue tracks focus and remains perceivable with the applicable Windows theme/high-contrast settings.

## Runs and findings

| Date | Revision/environment | Command or manual check | Result |
| --- | --- | --- | --- |
| — | — | No implementation or checks performed yet | Pending |

## Closure

Do not mark this work complete until the failure has been reproduced or its cause explained, focused automated tests pass, and the required manual scenarios are recorded. Document any scenario that cannot be exercised and preserve the distinction between synthetic event-handler tests and real UI input verification.
