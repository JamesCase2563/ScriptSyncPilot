# HandySync Request Timeouts and Shutdown Verification

**Status:** Focused request-timeout behavior passed; teardown and full acceptance verification remain incomplete. This document records acceptance evidence for the [plan](PLAN.md); the production-readiness reliability gate remains open until evidence is reviewed there.

## Automated verification

`HandyApiTimeoutTests` covers a stalled REST request, stalled hosting upload, stalled response-body read, caller cancellation, transport cancellation observation, and safe timeout messages. The tests use short injected budgets and synthetic secret text. All four tests passed in the out-of-sandbox run recorded below.

Record each run with the exact command, environment, result, and relevant test names. Use fake transports and controlled delays; do not use real connection keys, hosted-script URLs, or live device/service dependencies.

| Scenario | Evidence required | Result |
| --- | --- | --- |
| REST request exceeds its budget | Operation fails within the configured bound; timeout is distinguishable from caller cancellation where applicable; message is secret-free. | Passed |
| Hosting upload exceeds its budget | Upload and response read are bounded; dependent HSSP setup/play does not run. | Upload timeout passed; downstream setup/play gating not separately tested |
| REST/upload response body stalls | Body read is bounded and reported as failure. | Passed |
| Caller cancels before budget | Cancellation remains cancellation and is not mislabeled as timeout. | Passed |
| Clock sampling | Each sample is bounded; total estimate duration matches documented sample-count bound. | Pending |
| Disconnect with blocked serialized command | Teardown progresses within its bound; no stale play/seek follows disconnect; client disposal waits for or observes canceled work. | Pending |
| Disconnect with installed script and available service | One stop attempt is made before disposal; teardown completes; result is accurately represented. | Pending |
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

Result: **Passed, 4/4** (REST timeout, hosting upload timeout, response-body read timeout, caller cancellation). The test run completed in 331 ms. An earlier normal-output build attempt had encountered a file lock on `src/ScriptSyncPilot.FunscriptEditor/bin/Debug/net9.0-windows/ScriptSyncPilot.Common.dll`; that lock was later released and did not prevent the successful run.

## Manual verification

Pending. If performed, record OS/runtime, app build, mpv state, and whether the service/device was reachable. Verify the UI remains usable or closes within the documented bound and that stop failure is logged without a shutdown dialog or exposure of credentials/private URLs. A manual check is supplementary; simulated failure coverage is sufficient where practical.

## Limitations and release-gate linkage

- A timeout bounds the client's wait; it cannot prove the remote service did not process a request after the client stopped waiting.
- If communication is lost, HandySync cannot guarantee that the physical device stopped. User-facing limitations must continue to say this clearly.
- No verification evidence is recorded yet. Once completed, link this evidence from the Reliability and data preservation gate and priority 1 row in [production-readiness PLAN](../production-readiness/PLAN.md).

