# Command Ordering Verification

**Status:** Original verification complete 2026-11-08; code-review follow-up fix verified 2026-11-13.

## Scope and result

Production-readiness priority 3: suppress stale commands after disconnect or session replacement while preserving serialization and safe in-flight handling. See [design](DESIGN.md), [plan](PLAN.md), [production-readiness plan](../2026-11-production-readiness/PLAN.md), and [HandySync architecture](../../HandySync/ARCHITECTURE.md).

## Existing evidence to reuse

- [Timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md): disconnect rejects writes, cancels pending debounced writes and in-flight play, suppresses queued seek, prevents late stale success, and handles deferred drain/stop teardown.
- This evidence does not by itself establish every mpv session replacement race; characterize coverage before treating priority 3 as complete.
- M2.3 adds focused queued/in-flight HSSP session-replacement tests. Remaining limits are explicit in the scenario matrix; see the linked [design](DESIGN.md).

## Scenario matrix

| Transition/scenario | Result | Evidence/notes |
| --- | --- | --- |
| Disconnect with queued or in-flight command | Passed; prior evidence reused | `DeviceControlPollingTests.DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop`; archived timeout/shutdown verification covers pending writes, gate drain, deferred cleanup, and non-cooperative completion. |
| Handy client replacement | Partial; reconnect is lifecycle-blocked | `HandyApiTimeoutTests.StaleSyncTimeout_AfterClientReplaced_DoesNotAlterCurrentClient` checks a late stale failure after direct target replacement. Disconnect tests establish reconnect remains blocked while teardown drains; no supported queued command crossing reconnect is identified. |
| mpv session replacement with queued HSSP commands | Passed for position-resync, play/pause, and sync callbacks | `DeviceControlPollingTests.QueuedPlaybackCallbackIsSuppressedAfterMpvSessionReplacement` holds the device gate, swaps the current session, then verifies no request is sent and replacement readiness/status remain unchanged. The test directly replaces the field rather than running a full attach/detach transition. |
| Late completion after target replacement | Passed for late successful play/resync and stale timeout/failure | `DeviceControlPollingTests.LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession` verifies no stale success log/status and preserves readiness. `HandyApiTimeoutTests` stale seek/play/sync timeout tests verify status, log, readiness, and sync timer are unchanged after session replacement; stale sync failure after client replacement is also covered. |
| First sync after mpv session replacement | Passed | `DeviceControlPollingTests.FirstSyncAfterMpvSessionReplacementUsesReplacementPosition` asserts the request's `current_time` is from the replacement session, not the prior cached position. |
| mpv IPC waveform seek during session replacement | Not covered | Existing form-level seek tests cover failure reporting only; no test races the IPC write with attach/detach replacement. |
| Command serialization and cleanup | Prior evidence reused | Archived timeout/shutdown verification and `DeviceControlPollingTests` cover ordering, disconnect drain, and deferred teardown. |

## Runs

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| 2026-11-08 | Windows, .NET SDK 10.0.401; isolated output under `artifacts/command-ordering-m23/bin/` | `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --filter "FullyQualifiedName~QueuedPlaybackCallbackIsSuppressedAfterMpvSessionReplacement|FullyQualifiedName~LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession|FullyQualifiedName~FirstSyncAfterMpvSessionReplacementUsesReplacementPosition|FullyQualifiedName~StaleSeekTimeout_AfterSessionReplaced|FullyQualifiedName~StalePlayTimeout_AfterSessionReplaced|FullyQualifiedName~StaleSyncTimeout_AfterSessionReplaced|FullyQualifiedName~StaleSyncTimeout_AfterClientReplaced|FullyQualifiedName~DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop" --verbosity minimal -p:BaseOutputPath=artifacts/command-ordering-m23/bin/` | Passed: 11/11; build succeeded. |
| 2026-11-08 | Windows, .NET SDK 10.0.401; branch `work/command-ordering`, revision `bfd0022`; isolated output under `artifacts/command-ordering-m3/bin/` | `dotnet test ScriptSyncPilot.slnx --verbosity minimal -p:BaseOutputPath=artifacts/command-ordering-m3/bin/` | Passed: 326/326; build succeeded. |

## Findings and closure

- Code-review follow-up: an older playback failure could run after the device gate was released and suspend synchronization even if a newer same-target callback had already started. Playback failure updates now require the originating command generation to remain current. Regression test `DeviceControlPollingTests.StalePlaybackFailureDoesNotSuspendNewerQueuedCommand` passed 1/1 on 2026-11-13 with `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~StalePlaybackFailureDoesNotSuspendNewerQueuedCommand" --verbosity minimal`. The prior 326-test full-suite result predates this follow-up.

- PR review follow-up: a non-1x stop queued suspension feedback before awaiting the request, leaving stale status/log text when either the mpv session or Handy client was replaced in flight. Both new `LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession` rate-stop cases reproduced this; feedback now follows the successful stop and current-target check. `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --filter "FullyQualifiedName~LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession|FullyQualifiedName~PlaybackSpeedCommandTests" --verbosity minimal -p:BaseOutputPath=artifacts/rate-stop-verification/bin/` passed 13/13 on 2026-11-08 (Windows; current worktree). The 326-test full-suite result above predates this follow-up.
- Confirmed defects: stale play/resync success reporting after mpv replacement and use of the prior session's cached position for sync were fixed under M2.1; their regressions pass.
- Changes and regression tests: queued callback suppression for three HSSP producers, late successful play/resync suppression, replacement-session sync position, and stale timeout/failure preservation passed in the M2.3 focused run.
- Limitations: mpv IPC waveform seek replacement is not covered; queued/in-flight replacement tests assign the current session directly rather than driving a full attach/detach transition. Reused timeout/shutdown evidence covers cooperative cancellation and deferred retention for non-cooperative transports; a transport that never settles can leave disconnect pending, and physical stop cannot be guaranteed during communication loss.
- Release-gate conclusion: command-ordering verification is complete and linked from the parent priority-3 row. The overall production-readiness decision remains open.
