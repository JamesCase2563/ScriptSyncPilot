# HandySync Request Timeouts and Shutdown Design

**Status:** Approved by the maintainer on 2026-10-31; request budgets implemented and focused timeout behavior verified. Shutdown verification remains pending.
**Affected projects:** HandySync. Common and FunscriptEditor are out of scope unless investigation finds a shared HTTP or lifecycle defect that blocks this work.

## Goal

Bound Handy REST and script-hosting requests so a slow or unavailable service cannot hold connection setup, synchronization, disconnect, or application shutdown indefinitely. Preserve the existing device-command serialization and teardown order.

This work addresses the production-readiness reliability priority for timeouts and shutdown. It does not implement automatic reconnect, expanded retries/backoff, command coalescing, or a new recovery state machine. The separate [Reconnect and HSSP Recovery backlog item](../../HandySync/BACKLOG.md#reconnect-and-hssp-recovery) remains deferred for the initial release; when selected, its network operations should use the bounded request behavior established here. The production-readiness plan remains the release-gate source of truth.

## Current implementation

- `HandyApiClient` owns separate `HttpClient` instances for REST and hosting requests. The clients have framework timeouts disabled; explicit operation budgets use linked cancellation tokens: 10 seconds for status/control, 5 seconds per server-time sample, and 60 seconds for upload.
- Sends use `ResponseHeadersRead`; the same operation token bounds sending and subsequent response-body reads. Caller cancellation propagates as cancellation; expiration of the operation budget is surfaced as a safe `TaskCanceledException` with operation-specific timeout text.
- `MainForm` cancels its lifetime token during close, then detaches mpv and calls `DisconnectHandyAsync`.
- Disconnect suspends device work, cancels debounced writes, clears the active client, waits for the serialized device-command gate, attempts HSSP stop with `CancellationToken.None`, then disposes the captured client.
- The stop request has no caller cancellation and is now independently bounded by the 10-second HSSP control budget. The shutdown path must keep that final stop attempt independent of the already-canceled application lifetime token.

## Request-path characterization (M1.1)

This map is based on the current `MainForm` call paths. No extra polling or runtime instrumentation is needed to establish which operations occur; the logs provide qualitative progress/failure messages, while source inspection identifies the exact request sequence. Existing logs do not record per-request elapsed time, so they cannot establish latency distributions or prove timeout bounds.

| Phase | Current request sequence |
| --- | --- |
| Connect | `GET connected`; `GET info`; `PUT mode2`; 15 sequential `GET servertime` samples; when firmware supports controls: `GET capabilities`, `GET settings/slider`, `GET hstp/offset`, and if a slider exists `GET slider/stroke`; then hosting CSV upload and `PUT hssp/setup`; if mpv is already playing at 1x, `PUT hssp/play`. Each operation is serialized through the device-command gate, with setup/upload performed while holding it. |
| Steady state | mpv pause/resume sends `PUT hssp/stop` or `PUT hssp/play`; seek sends `PUT hssp/play`; periodic drift correction sends `PUT hssp/synctime`. Device polling reads offset and (when supported) stroke; temperature reads `GET slider/state`. Offset/stroke edits issue debounced `PUT` requests. |
| Disconnect | Stop polling and cancel pending debounced writes; clear current-client state; wait for the command gate; if the client has an installed script, attempt `PUT hssp/stop` with `CancellationToken.None`; then dispose the client. |
| Application close | Save settings; cancel the lifetime token; detach mpv without a stop; disconnect Handy, which makes the final stop attempt when appropriate and disposes the client; then close the form. |

### Adopted request budgets

For an explicit finite bound, use the upper end of each backlog range: 10 seconds for connectivity/status and HSSP control, 5 seconds per server-time sample, and 60 seconds for upload. The 15-sample clock estimate therefore has a request-time upper bound of 75 seconds, excluding local overhead. Connect is a sequence of these independently bounded requests, not one atomic operation with a single aggregate timeout. Set the conservative disconnect/close bound to at most 60 seconds to drain/cancel one already-running operation, followed by a 10-second final stop attempt and local cleanup. Active request cancellation should normally shorten the drain, while the bound must remain safe if cancellation does not complete immediately.

Relevant implementation: [HandyApiClient](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs), [HandySync MainForm](../../../src/ScriptSyncPilot.HandySync/MainForm.cs), and [HandySync architecture](../../HandySync/ARCHITECTURE.md).

## Request budgets

Use the existing HandySync backlog budgets as the initial implementation values:

| Operation group | Budget | Notes |
| --- | --- | --- |
| Connectivity/status and HSSP control | 5–10 seconds | Includes `connected`, device status/capability reads, mode/setup/play/synctime/stop, and device controls. Use the backlog range for this operation group. |
| Server time samples | 3–5 seconds per sample | Each sample is independently bounded; the full clock-offset estimate is bounded by sample count multiplied by its per-sample limit. Use the backlog range. |
| Script CSV upload | 30–60 seconds | Covers upload response and response-body read. Use the backlog range. |

The shutdown stop attempt uses the bounded HSSP control budget. Waiting for already-running serialized work also needs a defined bound or a cancellation strategy that guarantees teardown progress without disposing a client still in use. If canceling the active request improves lifecycle stability and allows safe cleanup, implement that cancellation; do not silently detach work that may still issue commands after disconnect.

Prefer operation-scoped timeout cancellation linked with the caller token so user/lifecycle cancellation remains distinguishable from an elapsed budget. Keep timeout errors safe and actionable; do not include connection keys, hosted-script URLs, raw response bodies, or arbitrary exception messages.

## Shutdown behavior

1. Mark disconnect/close in progress and stop polling or scheduling device writes.
2. Cancel pending debounced writes and prevent new commands from entering for the captured client.
3. Drain serialized work within the documented teardown bound, preserving command ordering and preventing stale play/seek work after disconnect.
4. If the captured client owns an installed/running HSSP script, make one bounded stop attempt using a token that is not canceled merely because application lifetime has ended.
5. Record a safe log message if stop cannot be confirmed. Do not show a shutdown error dialog. Continue cleanup and dispose the client only after its in-flight request work has completed or been canceled and observed.
6. Complete disconnect/close predictably. The UI must not report a confirmed physical stop when the request failed or timed out.

Communication loss can prevent a physical stop; the software can promise only a bounded attempt and honest reporting.

## Error and retry policy

- Report timeout separately from explicit user/lifecycle cancellation where the UI or logs distinguish them.
- Treat timeout as a failed operation; never convert it to a success-shaped response or silently continue setup/play as if the operation completed.
- Connect aborts on timeout during firmware or initial device-control reads, upload, or HSSP setup. If the initial HSSP play/stop request times out, HandySync clears the ready state, stops periodic sync, and reports that device state is unknown.
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

