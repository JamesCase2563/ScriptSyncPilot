# HandySync Request Timeouts and Shutdown Verification

**Status:** Complete 2026-11-01. The latest current-code isolated-output build succeeded with 0 warnings/errors, and the full solution test suite passed (281/281). Coverage includes clock sampling, sync/play timeout state resets, unavailable-service disconnect/close, non-cooperative drain and stop transports, deferred teardown, and repeated disconnect. Earlier 279/279 runs below are retained as historical runs and superseded by the latest result. This document records acceptance evidence for the [plan](PLAN.md); the production-readiness reliability gate remains open until evidence is reviewed there.

## Automated verification

`HandyApiTimeoutTests` covers a stalled REST request, stalled hosting upload, stalled response-body read, caller cancellation, transport cancellation observation, typed timeout classification, safe timeout messages, per-sample clock-sampling bounds, connect aborts on firmware-read/upload timeout, and play-timeout state reporting. The tests use short injected budgets and synthetic secret text.

Record each run with the exact command, environment, result, and relevant test names. Use fake transports and controlled delays; do not use real connection keys, hosted-script URLs, or live device/service dependencies.

| Scenario | Evidence required | Result |
| --- | --- | --- |
| REST request exceeds its budget | Operation fails within the configured bound; timeout is distinguishable from caller cancellation where applicable; message is secret-free. | Passed |
| Firmware prerequisite read times out during connect | Connect aborts before mode change, upload, setup, or play. | Passed |
| Hosting upload exceeds its budget | Upload is bounded and dependent HSSP setup/play does not run. | Passed |
| HSSP play times out | Ready state and periodic sync are cleared; status reports unknown device state, not successful playback. | Passed |
| Periodic HSSP sync times out | Ready state is cleared, periodic sync stops, and status reports unknown device state. | Passed |
| REST/upload response body stalls | Body read is bounded and reported as failure. | Passed |
| Caller cancels before budget | Cancellation remains cancellation and is not mislabeled as timeout. | Passed |
| Pending offset/stroke writes at disconnect | Both debounced writes are canceled before they send. | Passed |
| New offset/stroke writes while stop is in progress | Disconnect does not admit new device-control writes. | Passed |
| In-flight play and queued seek at disconnect | Active request is canceled, queued seek is skipped, and stop is attempted after the gate drains. | Passed |
| Non-cooperative transport exceeds drain deadline | Disconnect returns at the configured hard gate-wait deadline; captured client remains alive, reconnect is blocked, and deferred stop/disposal occur after the operation releases the gate. Late request completion is not reported as successful. | Passed |
| Disconnect stop after request lifetime cancellation | Stop starts despite canceled client request lifetime and is canceled by its own control timeout if stalled. | Passed |
| Stop transport ignores cancellation past the caller hard wait | Disconnect returns from the stop wait but remains pending; the captured client and command gate stay retained until the stop task completes, then teardown disposes/releases. | Passed |
| Stop failure during disconnect | Failure is logged without transport exception details; client and local disconnect cleanup still complete. | Passed |
| Application close during stop failure | Close completes after best-effort teardown; no shutdown error dialog is shown. | Passed |
| Clock sampling | Each sample is bounded; total estimate duration matches documented sample-count bound. | Passed |
| Disconnect with blocked serialized command | Teardown progresses within its bound; no stale play/seek follows disconnect; client disposal waits for or observes canceled work. | Passed for cooperative cancellation and a non-cooperative late completion |
| Disconnect with installed script and available service | One stop attempt is made before disposal; teardown completes; result is accurately represented. | Passed for the M3.2 blocked-play flow |
| Disconnect/close with unavailable service | Stop attempt times out within budget, failure is logged safely without a shutdown dialog, cleanup completes, and no confirmed-stop claim is made. | Passed |
| Repeated disconnect/close path | No duplicate unsafe disposal, unobserved task failure, or command after teardown. | Passed |

### Commands and results

M2.1 compile check:

```powershell
dotnet build src/ScriptSyncPilot.HandySync/ScriptSyncPilot.HandySync.csproj --no-restore --verbosity minimal
```

Result: **Build succeeded, 0 warnings, 0 errors**. This does not verify that requests cancel at their budgets; fake-handler timeout, response-read, and caller-cancellation cases remain pending.

Focused test execution inside the sandbox stalled after launching the test host. The diagnostic log showed VSTest listening on `127.0.0.1` while waiting for the test host to connect. Running the same focused test command outside the sandbox succeeded, indicating sandbox process/loopback isolation was the likely cause rather than a port collision.

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~HandyApiTimeoutTests" --verbosity minimal
```

Result: **Passed, 7/7** (REST timeout, hosting upload timeout, response-body read timeout, caller cancellation, firmware-read timeout aborts connect, upload timeout prevents setup/play, and play timeout clears ready state and avoids success status). The final focused run completed in 909 ms. An earlier normal-output build attempt had encountered a file lock on `src/ScriptSyncPilot.FunscriptEditor/bin/Debug/net9.0-windows/ScriptSyncPilot.Common.dll`; that lock was later released and did not prevent the successful run.

M3.1 focused lifecycle command:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~PendingOffsetAndStrokeWritesAreCancelledWhenDisconnectBegins|FullyQualifiedName~DisconnectRejectsNewWritesWhileStopIsInProgress" --verbosity minimal
```

Result: **Passed, 2/2**. Both pending debounced writes were absent from the request log after disconnect, and no new control writes were issued while the stop request was held open.

M3.2 focused serialized-command drain command:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~PendingOffsetAndStrokeWritesAreCancelledWhenDisconnectBegins|FullyQualifiedName~DisconnectRejectsNewWritesWhileStopIsInProgress|FullyQualifiedName~DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop" --verbosity minimal
```

Result: **Passed, 3/3**. Disconnect canceled the blocked play operation, the queued seek sent no request after disconnect began, and the bounded stop request ran after the command gate drained.

Hard drain-deadline command (review follow-up):

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~DisconnectDrainHasHardDeadlineAndRetainsClientUntilLateWorkCompletes" --verbosity minimal
```

Result: **Passed, 1/1**. A fake play transport ignored cancellation and remained blocked past an injected 100 ms drain deadline. Disconnect returned within the hard bound, kept reconnect disabled and the client undisposed, then after release rejected the late play completion as canceled, attempted stop, and disposed only after the gate drained.

M3.3 bounded independent disconnect-stop command:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~DisconnectStop_IgnoresCanceledRequestLifetime_ButKeepsControlTimeout" --verbosity minimal
```

Result: **Passed, 1/1**. After canceling the client's normal request lifetime, the stop request still started; the injected control timeout canceled its stalled transport and returned the safe `hssp/stop: request timed out.` failure.

M3.4 cleanup after stop failure command:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~DisconnectContinuesCleanupAfterUnexpectedStopFailure" --verbosity minimal
```

Result: **Passed, 1/1**. The stop transport threw an unexpected exception containing synthetic secret text; disconnect logged a safe failure, cleared the active client and UI disconnect state, and completed normally. The M3.2 blocked-play test separately verifies that canceled serialized work completes before stop begins and before disconnect completes.

M3.5 close-after-stop-failure command:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~ClosingCompletesAfterStopFailureWithoutShowingShutdownDialog" --verbosity minimal
```

Result: **Passed, 1/1**. The first close event was deferred for asynchronous teardown; despite a failed stop request the form closed, and the shutdown error callback was never invoked.

Typed timeout classification follow-up (original run):

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~HandyApiTimeoutTests" --verbosity minimal
```

Result: **Passed, 9/9** on 2026-11-01. This run predates the later `SyncTimeout_ClearsReadyStateAndStopsPeriodicSync` test. Budget expiration now raises `HandyApiTimeoutException`, while caller cancellation remains an ordinary `TaskCanceledException`; `MainForm` classifies the typed exception without inspecting message text.

Latest current `HandyApiTimeoutTests` class rerun:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~HandyApiTimeoutTests" --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\
```

Result: **Passed, 10/10** on 2026-11-01. This rerun includes `DisconnectStop_IgnoresCanceledRequestLifetime_ButKeepsControlTimeout` and the later-added `SyncTimeout_ClearsReadyStateAndStopsPeriodicSync`.

M4 remaining-scenario command (per-sample clock bound, disconnect/close against an unavailable service, repeated disconnect):

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj -m:1 --filter "FullyQualifiedName~ClockSampling_BoundsEachSampleIndividually|FullyQualifiedName~DisconnectStopTimeoutAgainstUnavailableService|FullyQualifiedName~ClosingCompletesWhenStopTimesOutAgainstUnavailableService|FullyQualifiedName~RepeatedDisconnectSharesOneTeardown" --verbosity minimal
```

Result: **Passed, 4/4** (2026-11-01, Windows, .NET 9, Debug).

- `ClockSampling_BoundsEachSampleIndividually_AndAbortsAtTheStalledSample`: a 15-sample estimate with the third server-time sample stalled failed with the safe `servertime: request timed out.` message within one per-sample budget; exactly 3 requests were sent, confirming the per-sample (not aggregate) bound and that the remaining samples are never issued.
- `DisconnectStopTimeoutAgainstUnavailableServiceCompletesCleanupWithinBudget`: the stalled stop request was canceled by the injected control timeout, disconnect logged `Could not stop Handy playback: hssp/stop: request timed out.` without transport exception text, and local cleanup (cleared client, idle status, re-enabled connect) completed within the test bound. No confirmed-stop claim was made.
- `ClosingCompletesWhenStopTimesOutAgainstUnavailableServiceWithoutShutdownDialog`: application close deferred the first close event, the stalled stop was canceled by its control timeout, the form closed, and the shutdown error callback was never invoked.
- `RepeatedDisconnectSharesOneTeardownAndALaterDisconnectIsSafe`: a disconnect requested during teardown returned the same in-progress task (one stop request total), and a later disconnect after completion finished without issuing further device requests, duplicate disposal, or unobserved task failures (`HandyApiClient.Dispose` is idempotent).

M4 full solution build and test run:

```powershell
dotnet build ScriptSyncPilot.slnx --verbosity minimal
dotnet test ScriptSyncPilot.slnx --no-build -m:1 --verbosity minimal
```

Result: **Build succeeded, 0 warnings, 0 errors. Tests passed 279/279** (ScriptSyncPilot.Common.Tests 88/88; ScriptSyncPilot.FunscriptEditor.Tests 191/191). Run 2026-11-01 on Windows, .NET 9, Debug configuration, outside the sandbox.

Hard-drain review follow-up full solution build/test command:

```powershell
dotnet test ScriptSyncPilot.slnx --no-restore -m:1 --verbosity minimal
```

Result: **Passed, 279/279** (Common.Tests 88/88; FunscriptEditor.Tests 191/191). The solution test command built all projects successfully before running tests; this run includes the non-cooperative transport deadline test.

Non-cooperative stop hard-wait regression test:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~DisconnectStopThatIgnoresCancellationRemainsPendingUntilDeferredTeardownReleasesGate" --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\
```

Result: **Passed, 1/1**. The fake stop transport ignored cancellation and stayed pending beyond the 2-second caller wait. Disconnect remained pending, the client stayed undisposed, and the command gate remained held. Releasing the transport completed deferred teardown, disposed the client, released the gate, and cleared the pending state.

Periodic sync-timeout state regression test:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~SyncTimeout_ClearsReadyStateAndStopsPeriodicSync" --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\
```

Result: **Passed, 1/1**. A stalled `hssp/synctime` request timed out, cleared `_hsspReady`, stopped `_syncTimer`, and reported unknown device state.

Latest current-code isolated-output solution build and test:

```powershell
dotnet build ScriptSyncPilot.slnx --no-restore -m:1 --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\
dotnet test ScriptSyncPilot.slnx --no-build --no-restore -m:1 --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\
```

Result: **Build succeeded, 0 warnings, 0 errors. Tests passed 281/281** (Common.Tests 88/88; FunscriptEditor.Tests 193/193). Run 2026-11-01 on Windows, .NET 9, Debug configuration, with output isolated under `.verification/bin/` to avoid overwriting assemblies loaded by the running application. This supersedes the earlier 279/279 full runs above.

## Manual verification

Not performed. Reviewed against the shutdown acceptance criteria: manual observation is supplementary, not required, because automated WinForms close tests exercise asynchronous close completion after stop failure/timeout with bounded fake transports. These tests do not exercise a real Windows session-end notification (`CloseReason.WindowsShutDown`), which is outside the documented application-close guarantee.

## Limitations and release-gate linkage

- A timeout bounds the client's wait; it cannot prove the remote service did not process a request after the client stopped waiting.
- If communication is lost, HandySync cannot guarantee that the physical device stopped. User-facing limitations must continue to say this clearly.
- If a non-cooperative command holds the gate, UI disconnect/close proceeds after the 60-second gate-wait deadline and final stop/disposal remain deferred. If the stop transport itself never completes after the gate drains, disconnect remains pending, while close proceeds after the independent 2-second stop-task wait; in both cases the captured client and command gate remain retained rather than being disposed while in use. The new fake test verifies eventual stop-task completion releases the gate and permits disposal.
- The lifecycle tests drive `MainForm` through its private disconnect/close entry points with fake transports; they do not exercise a real Windows session close (`CloseReason.WindowsShutDown`) or a live service outage. Those cases are outside the documented application-close guarantee and are not release blockers for this work item.
- Automated verification is complete; this evidence is linked from the priority 1 row in the [production-readiness PLAN](../production-readiness/PLAN.md). The Reliability and data preservation gate itself remains open until its full scope is reviewed there.

