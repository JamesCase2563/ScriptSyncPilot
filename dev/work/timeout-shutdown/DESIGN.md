# HandySync Request Timeouts and Shutdown Design

**Status:** Implemented and verified; the work item completed 2026-11-01 (see [PLAN.md](PLAN.md) and [VERIFICATION.md](VERIFICATION.md)). Approved by the maintainer on 2026-10-31.
**Affected projects:** HandySync. Common and FunscriptEditor are out of scope unless investigation finds a shared HTTP or lifecycle defect that blocks this work.

## Goal

Bound Handy REST and script-hosting requests so a slow or unavailable service cannot hold connection setup, synchronization, disconnect, or application shutdown indefinitely. Preserve the existing device-command serialization and teardown order.

This work addresses the production-readiness reliability priority for timeouts and shutdown. It does not implement automatic reconnect, expanded retries/backoff, command coalescing, or a new recovery state machine. The separate [Reconnect and HSSP Recovery backlog item](../../HandySync/BACKLOG.md#reconnect-and-hssp-recovery) remains deferred for the initial release; when selected, its network operations should use the bounded request behavior established here. The production-readiness plan remains the release-gate source of truth.

## Current implementation

- `HandyApiClient` owns separate `HttpClient` instances for REST and hosting requests. The clients have framework timeouts disabled; explicit operation budgets use linked cancellation tokens: 10 seconds for status/control, 5 seconds per server-time sample, and 60 seconds for upload.
- Sends use `ResponseHeadersRead`; the same operation token bounds sending and subsequent response-body reads. Caller cancellation propagates as cancellation; expiration of the operation budget is surfaced as a safe `HandyApiTimeoutException` (a `TaskCanceledException`) with operation-specific timeout text.
- `MainForm` cancels its lifetime token during close, then detaches mpv and calls `DisconnectHandyAsync`.
- Disconnect suspends device work, cancels debounced writes and pending requests, and waits at most 60 seconds for the serialized device-command gate. If it drains, disconnect attempts HSSP stop with a separately bounded token and disposes the captured client even if stop fails. If it does not drain by the deadline, disconnect/close completes locally while a deferred continuation retains the client, waits for the gate, then attempts stop and disposes after the work is observed. Reconnect remains blocked during deferred teardown.
- The stop request has no caller cancellation and retains the 10-second HSSP control cancellation budget. Disconnect waits at most 2 seconds for that stop task; if the transport has not completed, it returns with teardown pending and a deferred observer retains the client and command gate until the stop task actually completes. The shutdown path keeps that final stop attempt independent of the already-canceled application lifetime token.

## Request-path characterization (M1.1)

This map is based on the current `MainForm` call paths. No extra polling or runtime instrumentation is needed to establish which operations occur; the logs provide qualitative progress/failure messages, while source inspection identifies the exact request sequence. Existing logs do not record per-request elapsed time, so they cannot establish latency distributions or prove timeout bounds.

| Phase | Current request sequence |
| --- | --- |
| Connect | `GET connected`; `GET info`; `PUT mode2`; 15 sequential `GET servertime` samples; when firmware supports controls: `GET capabilities`, `GET settings/slider`, `GET hstp/offset`, and if a slider exists `GET slider/stroke`; then hosting CSV upload and `PUT hssp/setup`; if mpv is already playing at 1x, `PUT hssp/play`. Each operation is serialized through the device-command gate, with setup/upload performed while holding it. |
| Steady state | mpv pause/resume sends `PUT hssp/stop` or `PUT hssp/play`; seek sends `PUT hssp/play`; periodic drift correction sends `PUT hssp/synctime`. Device polling reads offset and (when supported) stroke; temperature reads `GET slider/state`. Offset/stroke edits issue debounced `PUT` requests. |
| Disconnect | Stop polling and cancel pending debounced writes; clear current-client state; wait up to 60 seconds for the command gate; if it drains and the client has an installed script, attempt `PUT hssp/stop` with the separate bounded token and wait at most 2 seconds for its task. If stop remains pending, keep disconnect pending and retain the client/gate until deferred teardown observes completion. If the gate misses its deadline, return locally and defer stop/disposal until the gate is safely acquired. |
| Application close | Save settings; cancel the lifetime token; bound the mpv transition wait to 2 seconds; wait up to 60 seconds for Handy command drain; if drained, wait at most 2 seconds for the final stop task and close while any remaining teardown continues with the client retained. If the gate misses its deadline, close while deferred teardown retains the client and waits to stop/dispose safely. |

### Adopted request budgets

For an explicit finite bound, use the upper end of each backlog range: 10 seconds for connectivity/status and HSSP control, 5 seconds per server-time sample, and 60 seconds for upload. The 15-sample clock estimate therefore has a request-time upper bound of 75 seconds, excluding local overhead. Connect is a sequence of these independently bounded requests, not one atomic operation with a single aggregate timeout. Disconnect/close waits at most 60 seconds for the command gate. After gate acquisition, the stop request's token is canceled at 10 seconds, while the caller's independent hard wait is 2 seconds. If stop remains pending, disconnect stays pending and close proceeds while deferred teardown retains the client and gate until the stop task completes. If a non-cooperative operation still holds the gate, local disconnect/close proceeds at the 60-second gate deadline and stop/disposal are deferred until the gate is safely acquired. A permanently stuck operation therefore prevents final stop/disposal, but cannot hold the close flow indefinitely and its client is not disposed while in use.

Relevant implementation: [HandyApiClient](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs), [HandySync MainForm](../../../src/ScriptSyncPilot.HandySync/MainForm.cs), and [HandySync architecture](../../HandySync/ARCHITECTURE.md).

## Request budgets

Use the existing HandySync backlog budgets as the initial implementation values:

| Operation group | Budget | Notes |
| --- | --- | --- |
| Connectivity/status and HSSP control | 5–10 seconds | Includes `connected`, device status/capability reads, mode/setup/play/synctime/stop, and device controls. Use the backlog range for this operation group. |
| Server time samples | 3–5 seconds per sample | Each sample is independently bounded; the full clock-offset estimate is bounded by sample count multiplied by its per-sample limit. Use the backlog range. |
| Script CSV upload | 30–60 seconds | Covers upload response and response-body read. Use the backlog range. |

The shutdown stop attempt uses the bounded HSSP control cancellation budget and a separate 2-second caller wait. Waiting for already-running serialized work uses a hard UI wait deadline plus deferred teardown; do not dispose the client while work may still use it. A request that eventually returns after cancellation must still fail as canceled rather than report success or trigger follow-up setup/play. If the stop operation never returns, keep the client and command gate retained and disconnect pending rather than racing disposal against active use.

Prefer operation-scoped timeout cancellation linked with the caller token so user/lifecycle cancellation remains distinguishable from an elapsed budget. Keep timeout errors safe and actionable; do not include connection keys, hosted-script URLs, raw response bodies, or arbitrary exception messages.

## Shutdown behavior

1. Mark disconnect/close in progress, stop polling, clear the active client/readiness state, and stop scheduling device writes.
2. Cancel pending debounced writes and cancel active Handy REST/hosting operations for the captured client before waiting on the command gate.
3. Wait up to 60 seconds for serialized work to drain. Active HTTP operations observe cancellation; if a transport does not cooperate by the deadline, continue local disconnect/close and retain a deferred gate waiter. Queued commands recheck client identity/readiness after acquiring the gate and exit without sending stale play/seek requests. A late transport completion checks its canceled linked token and cannot report success or continue dependent setup/play.
4. If the captured client owns an installed/running HSSP script, make one bounded stop attempt using a separate token that bypasses the canceled request lifetime and remains bounded by the HSSP control budget.
5. Record a safe log message if stop cannot be confirmed, including unexpected stop failures without logging raw transport exception text. Do not show a shutdown error dialog. Continue cleanup and dispose the client only after its in-flight request work has completed or been canceled and observed.
6. Complete disconnect/close predictably. The close handler permits final close in a `finally` after best-effort teardown, logging unexpected teardown failures without showing a shutdown dialog. The UI must not report a confirmed physical stop when the request failed or timed out.

Communication loss can prevent a physical stop; the software can promise only a bounded attempt and honest reporting.

## Error and retry policy

- Report timeout separately from explicit user/lifecycle cancellation where the UI or logs distinguish them.
- Treat timeout as a failed operation; never convert it to a success-shaped response or silently continue setup/play as if the operation completed.
- Connect aborts on timeout during firmware or initial device-control reads, upload, or HSSP setup. If the initial HSSP play/stop request times out, HandySync clears the ready state, stops periodic sync, and reports that device state is unknown.
- Disconnect cancels all pending HTTP work for the captured client, waits at most 60 seconds for the serialized command gate, and then uses the separate bounded HSSP stop request. If gate acquisition times out, a deferred continuation retains the client and completes stop/disposal once work is observed. `HandyApiClient.Dispose` is safe to call repeatedly.
- Do not add automatic retries in this work. Retry policy for transient resets, DNS failures, HTTP 408/429/5xx and `Retry-After` remains separately deferred in the HandySync backlog.
- Preserve existing useful, secret-free diagnostics and existing response validation.

## Compatibility and constraints

- Preserve the single device-command gate, captured-client identity checks, manual reconnect flow, and existing HSSP behavior.
- No new package, protocol, device support, logging overhaul, or command-coalescing machinery is required.
- Keep timeout defaults and rationale centralized enough to review and test. REST and hosting transports may need different values.
- Tests should use injected `HttpMessageHandler` implementations and controlled tasks; no physical device or live service is required.

## Decisions recorded 2026-10-31

- Use the upper end of each timeout range in the [HandySync backlog](../../HandySync/BACKLOG.md#request-timeouts-and-retries): connectivity/status and HSSP control 10 seconds, each time sample 5 seconds, upload 60 seconds. Preserve the per-sample/aggregate distinction.
- Use active request cancellation if it makes lifecycle handling more stable; cancellation and client disposal must still be coordinated so no request continues issuing work after teardown.
- During WinForms shutdown, log a safe stop failure and continue cleanup without presenting an error dialog.
- Characterize request order through source inspection and existing logs; do not add polling or request-timing instrumentation solely for this milestone. Runtime timing measurements can be added if verification later needs them.

