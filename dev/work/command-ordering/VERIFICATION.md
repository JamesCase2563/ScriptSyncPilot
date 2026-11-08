# Command Ordering Verification

**Status:** M2.3 focused verification complete 2026-11-08; full verification and closure remain open.

## Scope and result

Production-readiness priority 3: suppress stale commands after disconnect or session replacement while preserving serialization and safe in-flight handling. See [design](DESIGN.md), [plan](PLAN.md), [production-readiness plan](../production-readiness/PLAN.md), and [HandySync architecture](../../HandySync/ARCHITECTURE.md).

## Existing evidence to reuse

- [Timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md): disconnect rejects writes, cancels pending debounced writes and in-flight play, suppresses queued seek, prevents late stale success, and handles deferred drain/stop teardown.
- This evidence does not by itself establish every mpv session replacement race; characterize coverage before treating priority 3 as complete.

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

## Findings and closure

- Confirmed defects: stale play/resync success reporting after mpv replacement and use of the prior session's cached position for sync were fixed under M2.1; their regressions pass.
- Changes and regression tests: queued callback suppression for three HSSP producers, late successful play/resync suppression, replacement-session sync position, and stale timeout/failure preservation passed in the M2.3 focused run.
- Limitations: mpv IPC waveform seek replacement is not covered; queued/in-flight replacement tests assign the current session directly rather than driving a full attach/detach transition. The full solution suite remains to be run for M3.
- Release-gate conclusion: command-ordering work remains open pending M3 verification and release-plan linkage.
