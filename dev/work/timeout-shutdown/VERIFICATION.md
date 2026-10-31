# HandySync Request Timeouts and Shutdown Verification

**Status:** Not started. This document records acceptance evidence for the [plan](PLAN.md); the production-readiness reliability gate remains open until evidence is reviewed there.

## Automated verification

No timeout/shutdown verification has been run for this work item yet.

Record each run with the exact command, environment, result, and relevant test names. Use fake transports and controlled delays; do not use real connection keys, hosted-script URLs, or live device/service dependencies.

| Scenario | Evidence required | Result |
| --- | --- | --- |
| REST request exceeds its budget | Operation fails within the configured bound; timeout is distinguishable from caller cancellation where applicable; message is secret-free. | Pending |
| Hosting upload exceeds its budget | Upload and response read are bounded; dependent HSSP setup/play does not run. | Pending |
| REST/upload response body stalls | Body read is bounded and reported as failure. | Pending |
| Caller cancels before budget | Cancellation remains cancellation and is not mislabeled as timeout. | Pending |
| Clock sampling | Each sample is bounded; total estimate duration matches documented sample-count bound. | Pending |
| Disconnect with blocked serialized command | Teardown progresses within its bound; no stale play/seek follows disconnect; client disposal waits for or observes canceled work. | Pending |
| Disconnect with installed script and available service | One stop attempt is made before disposal; teardown completes; result is accurately represented. | Pending |
| Disconnect/close with unavailable service | Stop attempt times out within budget, failure is logged safely without a shutdown dialog, cleanup completes, and no confirmed-stop claim is made. | Pending |
| Repeated disconnect/close path | No duplicate unsafe disposal, unobserved task failure, or command after teardown. | Pending |

### Commands and results

Pending implementation. Add exact focused and full-suite commands and pass/fail counts here; do not infer results from compilation alone.

## Manual verification

Pending. If performed, record OS/runtime, app build, mpv state, and whether the service/device was reachable. Verify the UI remains usable or closes within the documented bound and that stop failure is logged without a shutdown dialog or exposure of credentials/private URLs. A manual check is supplementary; simulated failure coverage is sufficient where practical.

## Limitations and release-gate linkage

- A timeout bounds the client's wait; it cannot prove the remote service did not process a request after the client stopped waiting.
- If communication is lost, HandySync cannot guarantee that the physical device stopped. User-facing limitations must continue to say this clearly.
- No verification evidence is recorded yet. Once completed, link this evidence from the Reliability and data preservation gate and priority 1 row in [production-readiness PLAN](../production-readiness/PLAN.md).

