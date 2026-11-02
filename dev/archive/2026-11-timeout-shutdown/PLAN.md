# HandySync Request Timeouts and Shutdown Plan

**Status:** Complete 2026-11-01. All milestones are done: request budgets, bounded teardown, and the full automated acceptance verification passed. The latest recorded focused `HandyApiTimeoutTests` run is 24/24 (at `64b96b6`); the latest recorded full solution run is 281/281 (Common 88/88; FunscriptEditor 193/193). Earlier milestone counts below are historical focused runs, not the final totals. Approved by the maintainer on 2026-10-31; initial design decisions recorded 2026-10-31.
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
- [x] Specify the request bounds: clock estimation is at most 15 × 5 seconds = 75 seconds, excluding local overhead; disconnect/close waits at most 60 seconds for the command gate. The stop token has a 10-second cancellation budget, with a separate 2-second caller hard wait; if it remains pending, disconnect remains pending and client disposal is deferred until the stop task completes. If the command gate does not drain, close proceeds at the gate deadline while stop/disposal are deferred until safe. Implement the cancellation/drain mechanism safely in M2/M3.
- [x] Prefer active request cancellation if it improves lifecycle stability; coordinate cancellation, drain, and client disposal so no stale work can continue.
- [x] Record decisions in [DESIGN.md](DESIGN.md).

### 2. Implement bounded requests

- [x] Apply explicit bounded cancellation to REST and hosting sends and response reads.
- [x] Preserve caller cancellation semantics and distinguish timeout from caller cancellation.
- [x] Keep timeout/error messages free of credentials, hosted URLs, response bodies, and unsafe exception text.
- [x] Ensure a timed-out operation cannot be reported as successful or trigger dependent setup/play steps.

### 3. Implement predictable teardown

- [x] Stop accepting commands and cancel pending debounced offset/stroke writes before teardown. Verified with focused disconnect lifecycle tests.
- [x] Cancel in-flight Handy requests before draining the command gate; enforce a 60-second hard gate-wait deadline, retain the client and defer stop/disposal if work remains, and prevent stale play/seek success after late transport completion.
- [x] Attempt HSSP stop with its own bounded token, independent of canceled app lifetime; verified that canceled client work does not cancel the stop and that the control timeout still cancels a stalled stop request.
- [x] Continue cleanup after stop failure/timeout, observing canceled work before disposing the captured client; disconnect waits for the command gate, disposes in a `finally`, and completes local state cleanup after a stop failure.
- [x] Ensure close flow completes predictably and logs stop failure without showing a shutdown error dialog; close is allowed in a `finally` after best-effort mpv and Handy cleanup.

### 4. Verify and close

- [x] Add fake-handler tests for REST, upload, response-read, and caller-cancellation behavior.
- [x] Add lifecycle tests for blocked in-flight commands, stop success/failure/timeout, cleanup, and no stale post-disconnect commands.
- [x] Verify a stop transport that ignores cancellation past the 2-second caller hard wait; disconnect remains pending and retains the client/gate until deferred teardown finishes. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify periodic `hssp/synctime` timeout clears readiness and stops periodic sync. See [VERIFICATION.md](VERIFICATION.md).
- [x] Initial M2 focused HandyApi timeout/connect-flow run: 7/7 passed (historical milestone result). The latest complete `HandyApiTimeoutTests` class run passed 24/24; see [VERIFICATION.md](VERIFICATION.md).
- [x] Verify disconnect command admission and pending-write cancellation; 2/2 focused lifecycle tests passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify in-flight command cancellation, queued seek suppression, and the post-drain stop attempt; 1/1 focused M3.2 test passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify the disconnect stop bypasses canceled request lifetime while remaining bounded by the HSSP control timeout; 1/1 focused test passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify cleanup and safe logging continue after a stop failure; 1/1 focused lifecycle test passed. M3.2 coverage also verifies canceled play work completes before the post-drain stop attempt. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify application close completes after a stop failure without invoking the shutdown error callback; 1/1 focused WinForms lifecycle test passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Verify the remaining acceptance scenarios: per-sample clock-sampling bounds, disconnect/close against an unavailable service (stop timeout), and repeated disconnect; 4/4 focused tests passed. See [VERIFICATION.md](VERIFICATION.md).
- [x] Run full solution tests/build and record exact commands and results in [VERIFICATION.md](VERIFICATION.md); the latest isolated-output solution build succeeded and 281/281 tests passed on 2026-11-01.
- [x] Move any newly found independent defects to the owning backlog and link them here. Final review replaced message-suffix timeout detection with a typed `HandyApiTimeoutException`; the timeout-wrapper overloads were later consolidated into one implementation.
- [x] Update [HandySync architecture](../../HandySync/ARCHITECTURE.md) if implemented behavior changes its lifecycle contract. The lifecycle section now documents the request budgets and bounded disconnect/close contract.
- [x] Record completion and any remaining limitations here; archive the work folder only when all required work is complete.

## Acceptance criteria

- Every Handy HTTP operation and response read has a documented finite bound matching its operation class.
- A request timeout is surfaced as a failed operation with safe, actionable text; explicit caller cancellation remains cancellation.
- Setup/play does not proceed after a prerequisite request times out or fails.
- Disconnect and application close wait no longer than 60 seconds for the command gate and at most 2 seconds for a final stop task. A stop token is canceled after 10 seconds; if its transport remains pending after the caller hard wait, disconnect remains pending and safe disposal is deferred until the task completes. If the gate does not drain, UI close completes at the gate deadline while safe stop/disposal remain deferred until in-flight work releases the gate.
- When appropriate, teardown makes one bounded stop attempt; failure is visible and never reported as confirmed stop.
- No queued or in-flight stale play/seek is issued after disconnect begins; existing command serialization remains intact.
- The captured client is disposed after in-flight work is complete or canceled and observed.
- Automated verification passes with fake transports; no live network/device is required for these criteria.
- No automatic retry, reconnection, or command-coalescing behavior is introduced by this change.

## Status and outcome

Completed 2026-11-01 on branch `work/handysync-timeout-shutdown`. All acceptance criteria are verified with fake transports: every Handy HTTP operation and response read has an explicit timeout/cancellation budget (10 s control/status, 5 s per time sample, 60 s upload), timeouts surface as failed operations distinct from caller cancellation with secret-free text, dependent setup/play never proceeds after a timed-out prerequisite, disconnect/close stop waiting for the command gate at 60 seconds, and disconnect waits at most 2 seconds for the final stop task before retaining it for deferred teardown. Stop/disposal follows only after in-flight work is observed, exactly one stop attempt is made when safe and never reported as confirmed on failure, and no stale play/seek is issued after disconnect. If a transport never completes despite cancellation, UI close remains bounded while the captured client is retained and stop/disposal remain deferred. Latest full suite: 281/281 passed. No automatic retry, reconnect, or coalescing behavior was introduced. Full evidence, exact commands, and results are in [VERIFICATION.md](VERIFICATION.md); the [HandySync architecture](../../HandySync/ARCHITECTURE.md) lifecycle contract and the [HandySync backlog](../../HandySync/BACKLOG.md#request-timeouts-and-retries) status were updated.

Remaining limitations: a timeout bounds only the client's wait and cannot guarantee a physical device stop under communication loss; if a command transport never returns after cancellation, final stop and disposal remain deferred indefinitely to avoid disposing a client still in use. UI close returns after the bounded gate and stop waits, while an interactive disconnect remains pending if the final stop task never completes; reconnect stays blocked until teardown finishes. A real Windows session-end notification and live service outage were not exercised. Manual UI observation is supplementary, not an acceptance gate. Expanded retries/backoff and reconnect/HSSP recovery remain deferred backlog items.

