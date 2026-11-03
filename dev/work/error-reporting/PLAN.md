# Error Reporting Plan

**Status:** Approved by the maintainer on 2026-11-02; M2.1–M2.3 are complete and the full automated suite passes; final message-safety and release-gate review remain open.
**Goal:** Close production-readiness reliability priority 2 by verifying safe, truthful, useful reporting of failed operations.
**Owner/source of truth:** This work item coordinates verification; component behavior remains owned by the [HandySync backlog](../../HandySync/BACKLOG.md).

## Scope

- Review failed connect/setup, reads, upload, playback/sync, device-control writes, disconnect/stop, and shutdown.
- Confirm visible outcome, safe diagnostic text, and suppression of dependent operations after failure.
- Fix concrete defects in the supported baseline and add focused regression coverage.

Out of scope: retries/reconnect, a diagnostics overhaul, telemetry, additional APIs/devices, and public support-channel setup (tracked by the production-readiness privacy/support work).

## Milestones

### 1. Characterize

- [x] Read current HandySync error handling and related tests; record implementation paths and existing coverage in [DESIGN.md](DESIGN.md) (M1.1).
- [x] Reuse the archived credential/URL and timeout/shutdown evidence where applicable; record coverage boundaries and remaining cases in [DESIGN.md](DESIGN.md) (M1.2).
- [x] Fill out the grouped [error-message inventory](ERROR-MESSAGE-INVENTORY.md), covering user-visible result, log text, readiness/state outcome, dependent work, next step, and sensitive-data risk (M1.3).

### 2. Resolve verified gaps

- [x] Fix confirmed misleading, hidden, unsafe, or non-actionable failure behavior within this scope (M2.1; see current behavior in the [inventory](ERROR-MESSAGE-INVENTORY.md)).
- [x] Keep timeout distinct from caller/lifecycle cancellation and keep unknown remote state explicit (M2.2; timeouts retain their dedicated status, caller cancellation reports canceled with unknown device state, and lifecycle cancellation is silent).
- [x] Add focused regression tests for each changed behavior (M2.3; see [`HandyApiTimeoutTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/HandyApiTimeoutTests.cs) and [`DeviceControlPollingTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/DeviceControlPollingTests.cs)).

### 3. Verify and close

- [x] Run tests for changed paths and record exact commands/results in [VERIFICATION.md](VERIFICATION.md) (full suite: 304 passed, 0 failed).
- [ ] Review messages for credentials, private hosted-script URLs, raw response bodies, and unsafe exception text.
- [ ] Record unresolved limits and link completed evidence from the [production-readiness plan](../production-readiness/PLAN.md).
- [ ] Mark complete only when the planned inventory and acceptance criteria are satisfied; archive only after completion.

## Acceptance criteria

- Failures are apparent to the user or operator through the existing appropriate UI/log path.
- Failure feedback is nonblocking and stays on the current form's log, status, or relevant control; error message boxes are not used.
- No reviewed failure path reports success or permits dependent work to proceed as if a prerequisite succeeded.
- Diagnostics contain only safe, bounded, useful information; no keys, hosted-script URLs, raw bodies, or arbitrary exception text.
- Changed behavior has automated regression coverage, and evidence is recorded in `VERIFICATION.md`.
- Existing successful behavior and deferred recovery scope are preserved.
