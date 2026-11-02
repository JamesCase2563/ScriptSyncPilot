# Error Reporting Plan

**Status:** Approved by the maintainer on 2026-11-02; no implementation or verification claimed.
**Goal:** Close production-readiness reliability priority 2 by verifying safe, truthful, useful reporting of failed operations.
**Owner/source of truth:** This work item coordinates verification; component behavior remains owned by the [HandySync backlog](../../HandySync/BACKLOG.md).

## Scope

- Review failed connect/setup, reads, upload, playback/sync, device-control writes, disconnect/stop, and shutdown.
- Confirm visible outcome, safe diagnostic text, and suppression of dependent operations after failure.
- Fix concrete defects in the supported baseline and add focused regression coverage.

Out of scope: retries/reconnect, a diagnostics overhaul, telemetry, additional APIs/devices, and public support-channel setup (tracked by the production-readiness privacy/support work).

## Milestones

### 1. Characterize

- [ ] Read current HandySync error handling and related tests; record paths and existing coverage in [DESIGN.md](DESIGN.md).
- [ ] Reuse the archived credential/URL and timeout/shutdown evidence where applicable; identify cases it does not cover.
- [ ] Build a failure matrix covering user-visible result, log text, readiness/state outcome, dependent work, and sensitive-data risk.

### 2. Resolve verified gaps

- [ ] Fix only confirmed misleading, hidden, unsafe, or non-actionable failure behavior within this scope.
- [ ] Keep timeout distinct from caller/lifecycle cancellation and keep unknown remote state explicit.
- [ ] Add focused regression tests for each changed behavior.

### 3. Verify and close

- [ ] Run focused tests for changed paths and record exact commands/results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Review messages for credentials, private hosted-script URLs, raw response bodies, and unsafe exception text.
- [ ] Record unresolved limits and link completed evidence from the [production-readiness plan](../production-readiness/PLAN.md).
- [ ] Mark complete only when the planned inventory and acceptance criteria are satisfied; archive only after completion.

## Acceptance criteria

- Failures are apparent to the user or operator through the existing appropriate UI/log path.
- No reviewed failure path reports success or permits dependent work to proceed as if a prerequisite succeeded.
- Diagnostics contain only safe, bounded, useful information; no keys, hosted-script URLs, raw bodies, or arbitrary exception text.
- Changed behavior has automated regression coverage, and evidence is recorded in `VERIFICATION.md`.
- Existing successful behavior and deferred recovery scope are preserved.
