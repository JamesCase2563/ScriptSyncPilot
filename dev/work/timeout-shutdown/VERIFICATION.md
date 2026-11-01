# HandySync Request Timeouts and Shutdown Verification

**Status:** Complete 2026-11-01. All planned fake-transport scenarios passed, including clock sampling, unavailable-service disconnect/close, and repeated disconnect; the full solution build and test suite passed (278/278). This document records acceptance evidence for the [plan](PLAN.md); the production-readiness reliability gate remains open until evidence is reviewed there.

## Automated verification

`HandyApiTimeoutTests` covers a stalled REST request, stalled hosting upload, stalled response-body read, caller cancellation, transport cancellation observation, safe timeout messages, per-sample clock-sampling bounds, connect aborts on firmware-read/upload timeout, and play-timeout state reporting. The tests use short injected budgets and synthetic secret text. All seven timeout tests passed in the out-of-sandbox run recorded below; the later clock-sampling test passed in the M4 run.

Record each run with the exact command, environment, result, and relevant test names. Use fake transports and controlled delays; do not use real connection keys, hosted-script URLs, or live device/service dependencies.

| Scenario | Evidence required | Result |
| --- | --- | --- |
| REST request exceeds its budget | Operation fails within the configured bound; timeout is distinguishable from caller cancellation where applicable; message is secret-free. | Passed |
| Firmware prerequisite read times out during connect | Connect aborts before mode change, upload, setup, or play. | Passed |
| Hosting upload exceeds its budget | Upload is bounded and dependent HSSP setup/play does not run. | Passed |
| HSSP play times out | Ready state and periodic sync are cleared; status reports unknown device state, not successful playback. | Passed |
| REST/upload response body stalls | Body read is bounded and reported as failure. | Passed |
| Caller cancels before budget | Cancellation remains cancellation and is not mislabeled as timeout. | Passed |
| Pending offset/stroke writes at disconnect | Both debounced writes are canceled before they send. | Passed |
| New offset/stroke writes while stop is in progress | Disconnect does not admit new device-control writes. | Passed |
| In-flight play and queued seek at disconnect | Active request is canceled, queued seek is skipped, and stop is attempted after the gate drains. | Passed |
| Disconnect stop after request lifetime cancellation | Stop starts despite canceled client request lifetime and is canceled by its own control timeout if stalled. | Passed |
| Stop failure during disconnect | Failure is logged without transport exception details; client and local disconnect cleanup still complete. | Passed |
| Application close during stop failure | Close completes after best-effort teardown; no shutdown error dialog is shown. | Passed |
| Clock sampling | Each sample is bounded; total estimate duration matches documented sample-count bound. | Passed |
| Disconnect with blocked serialized command | Teardown progresses within its bound; no stale play/seek follows disconnect; client disposal waits for or observes canceled work. | Passed for blocked play and queued seek |
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

Result: **Build succeeded, 0 warnings, 0 errors. Tests passed 278/278** (ScriptSyncPilot.Common.Tests 88/88 in 460 ms; ScriptSyncPilot.FunscriptEditor.Tests 190/190 in 5 s). Run 2026-11-01 on Windows, .NET 9, Debug configuration, outside the sandbox.

## Manual verification

Pending. If performed, record OS/runtime, app build, mpv state, and whether the service/device was reachable. Verify the UI remains usable or closes within the documented bound and that stop failure is logged without a shutdown dialog or exposure of credentials/private URLs. A manual check is supplementary; simulated failure coverage is sufficient where practical.

## Limitations and release-gate linkage

- A timeout bounds the client's wait; it cannot prove the remote service did not process a request after the client stopped waiting.
- If communication is lost, HandySync cannot guarantee that the physical device stopped. User-facing limitations must continue to say this clearly.
- The lifecycle tests drive `MainForm` through its private disconnect/close entry points with fake transports; they do not exercise a real Windows session close (`CloseReason.WindowsShutDown`) or a live service outage. Manual verification remains supplementary and was not performed.
- Automated verification is complete; this evidence is linked from the priority 1 row in the [production-readiness PLAN](../production-readiness/PLAN.md). The Reliability and data preservation gate itself remains open until its full scope is reviewed there.

