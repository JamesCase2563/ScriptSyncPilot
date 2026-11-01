# HandySync Request Timeouts and Shutdown Verification

**Status:** Focused request-timeout, connect-flow, play-timeout, and M3.1–M3.4 behavior passed; remaining teardown and full acceptance verification are incomplete. This document records acceptance evidence for the [plan](PLAN.md); the production-readiness reliability gate remains open until evidence is reviewed there.

## Automated verification

`HandyApiTimeoutTests` covers a stalled REST request, stalled hosting upload, stalled response-body read, caller cancellation, transport cancellation observation, safe timeout messages, connect aborts on firmware-read/upload timeout, and play-timeout state reporting. The tests use short injected budgets and synthetic secret text. All seven timeout tests passed in the out-of-sandbox run recorded below.

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
| Clock sampling | Each sample is bounded; total estimate duration matches documented sample-count bound. | Pending |
| Disconnect with blocked serialized command | Teardown progresses within its bound; no stale play/seek follows disconnect; client disposal waits for or observes canceled work. | Passed for blocked play and queued seek |
| Disconnect with installed script and available service | One stop attempt is made before disposal; teardown completes; result is accurately represented. | Passed for the M3.2 blocked-play flow |
| Disconnect/close with unavailable service | Stop attempt times out within budget, failure is logged safely without a shutdown dialog, cleanup completes, and no confirmed-stop claim is made. | Pending |
| Repeated disconnect/close path | No duplicate unsafe disposal, unobserved task failure, or command after teardown. | Pending |

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

## Manual verification

Pending. If performed, record OS/runtime, app build, mpv state, and whether the service/device was reachable. Verify the UI remains usable or closes within the documented bound and that stop failure is logged without a shutdown dialog or exposure of credentials/private URLs. A manual check is supplementary; simulated failure coverage is sufficient where practical.

## Limitations and release-gate linkage

- A timeout bounds the client's wait; it cannot prove the remote service did not process a request after the client stopped waiting.
- If communication is lost, HandySync cannot guarantee that the physical device stopped. User-facing limitations must continue to say this clearly.
- No verification evidence is recorded yet. Once completed, link this evidence from the Reliability and data preservation gate and priority 1 row in [production-readiness PLAN](../production-readiness/PLAN.md).

