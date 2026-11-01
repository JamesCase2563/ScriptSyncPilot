# HandySync Request Timeouts and Shutdown Plan

**Status:** Approved by the maintainer on 2026-10-31; implementation in progress. Request budgets and focused timeout behavior are verified; teardown and full acceptance verification remain pending. Initial design decisions recorded 2026-10-31.
**Affected projects:** HandySync.
**Goal:** Ensure HandySync requests and shutdown are bounded, stop is attempted safely, failures are visible, and no stale device command is issued after disconnect.

This work was selected from the [production-readiness plan](../production-readiness/PLAN.md), initial-release reliability priority 1. It follows the request-budget ranges in the [HandySync backlog](../../HandySync/BACKLOG.md#request-timeouts-and-retries). Expanded retries and automatic recovery remain deferred.

## Scope

- Add explicit operation budgets to Handy REST and Hosting API requests, including response-body reads.
- Ensure clock samples are individually bounded and the aggregate operation behavior is documented.
- Bound device-command drain and the final HSSP stop attempt during disconnect/application close.
- Preserve serialized command ordering, safe cancellation/error reporting, and cleanup/disposal guarantees.
- Add automated tests with fake transports for timeout and shutdown failure paths; record manual UI observations only if available.

Out of scope: automatic retries/backoff, reconnect/session restoration, command coalescing, broader diagnostics, API/device expansion, and physical-stop guarantees under network loss. The separate [Reconnect and HSSP Recovery backlog item](../../HandySync/BACKLOG.md#reconnect-and-hssp-recovery) remains deferred; if that work is later selected, it should build on the bounded request behavior established here rather than duplicate timeout policy.

## Milestones

### 1. Characterize current lifecycle and choose budgets

- [x] Confirm which requests occur during connect, steady-state sync, controls, disconnect, and close. See the request-path map in [DESIGN.md](DESIGN.md).
- [x] Adopt concrete values from the backlog ranges: control/status 10 seconds, each time sample 5 seconds, upload 60 seconds.
- [x] Specify the request bounds: clock estimation is at most 15 × 5 seconds = 75 seconds, excluding local overhead; disconnect/close is at most 60 seconds to drain/cancel one active operation, followed by the 10-second stop attempt and local cleanup. Implement the cancellation/drain mechanism safely in M2/M3.
- [x] Prefer active request cancellation if it improves lifecycle stability; coordinate cancellation, drain, and client disposal so no stale work can continue.
- [x] Record decisions in [DESIGN.md](DESIGN.md).

### 2. Implement bounded requests

- [x] Apply explicit bounded cancellation to REST and hosting sends and response reads.
- [x] Preserve caller cancellation semantics and distinguish timeout from caller cancellation.
- [x] Keep timeout/error messages free of credentials, hosted URLs, response bodies, and unsafe exception text.
- [x] Ensure a timed-out operation cannot be reported as successful or trigger dependent setup/play steps.

### 3. Implement predictable teardown

- [x] Stop accepting commands and cancel pending debounced offset/stroke writes before teardown. Verified with focused disconnect lifecycle tests.
- [x] Cancel in-flight Handy requests before draining the command gate; queued commands recheck the active client/readiness and cannot send stale play/seek requests after disconnect begins.
- [x] Attempt HSSP stop with its own bounded token, independent of canceled app lifetime; verified that canceled client work does not cancel the stop and that the control timeout still cancels a stalled stop request.
- [ ] Continue cleanup after stop failure/timeout, observing canceled work before disposing the captured client.
- [ ] Ensure close flow completes predictably and logs stop failure without showing a shutdown error dialog.

### 4. Verify and close

- [x] Add fake-handler tests for REST, upload, response-read, and caller-cancellation behavior.
- [ ] Add lifecycle tests for blocked in-flight commands, stop success/failure/timeout, cleanup, and no stale post-disconnect commands.
- [x] Run focused HandyApi timeout/connect-flow tests; 7/7 passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify disconnect command admission and pending-write cancellation; 2/2 focused lifecycle tests passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify in-flight command cancellation, queued seek suppression, and the post-drain stop attempt; 1/1 focused M3.2 test passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify the disconnect stop bypasses canceled request lifetime while remaining bounded by the HSSP control timeout; 1/1 focused test passed. See [VERIFICATION.md](VERIFICATION.md).
- [ ] Run full solution tests/build and record exact commands and results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Move any newly found independent defects to the owning backlog and link them here.
- [ ] Update [HandySync architecture](../../HandySync/ARCHITECTURE.md) if implemented behavior changes its lifecycle contract.
- [ ] Record completion and any remaining limitations here; archive the work folder only when all required work is complete.

## Acceptance criteria

- Every Handy HTTP operation and response read has a documented finite bound matching its operation class.
- A request timeout is surfaced as a failed operation with safe, actionable text; explicit caller cancellation remains cancellation.
- Setup/play does not proceed after a prerequisite request times out or fails.
- Disconnect and application close finish within the documented drain-plus-stop bound, even when the service is unavailable.
- When appropriate, teardown makes one bounded stop attempt; failure is visible and never reported as confirmed stop.
- No queued or in-flight stale play/seek is issued after disconnect begins; existing command serialization remains intact.
- The captured client is disposed after in-flight work is complete or canceled and observed.
- Automated verification passes with fake transports; no live network/device is required for these criteria.
- No automatic retry, reconnection, or command-coalescing behavior is introduced by this change.

## Status and outcome

M2 bounded requests and timeout failure handling are implemented and focused tests pass. M3 teardown behavior, full solution verification, and the remaining acceptance criteria are still open. Keep detailed evidence and unresolved limitations in [VERIFICATION.md](VERIFICATION.md); record the completion date, change/PR reference, and architecture/backlog updates here when the full work item is done.

