# Command Ordering Plan

**Status:** M1 characterization complete 2026-11-07; M2 implementation and new verification have not started.
**Goal:** Close production-readiness reliability priority 3 by verifying stale-command suppression across disconnect and session replacement.

## Scope

- Characterize Handy command serialization and lifecycle transition checks.
- Verify queued/in-flight commands across disconnect, Handy client replacement, and mpv session replacement.
- Fix confirmed stale-command or stale-completion defects and add regression tests.

Out of scope: desired-state/coalescing machinery, automatic recovery, device takeover/session restoration, new protocol support, and physical-stop guarantees under network loss.

## Milestones

### 1. Characterize transitions

- [x] Map command producers, captured targets, and lifecycle gates in [DESIGN.md](DESIGN.md) (M1.1).
- [x] Review existing lifecycle tests and archived [timeout/shutdown evidence](../../archive/2026-11-timeout-shutdown/VERIFICATION.md) (M1.2; reuse boundaries are recorded in [DESIGN.md](DESIGN.md)).
- [x] Identify missing deterministic scenarios, especially mpv session replacement during queued/in-flight work (M1.3; see [DESIGN.md](DESIGN.md)).

### 2. Verify and repair

- [ ] Add focused fake-transport tests for uncovered transitions and late completions.
- [ ] Fix only confirmed ordering/identity defects while preserving the single command gate and manual reconnect flow.
- [ ] Verify no old-target command is sent and no late old-target completion alters current UI/readiness.

### 3. Close

- [ ] Run focused tests and record exact commands/results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Record residual limits, including any guarantees that depend on cooperative transport cancellation.
- [ ] Link evidence from the [production-readiness plan](../production-readiness/PLAN.md); archive only after acceptance criteria are met.

## Acceptance criteria

- Commands remain serialized for a given client.
- No queued stale play/seek/control command is issued after disconnect or target replacement.
- Late responses from prior Handy/mpv targets do not mark the new target ready or produce misleading UI/log outcomes.
- Teardown/disposal remains safe and follows the documented bounded/deferred lifecycle.
- New or changed behavior has deterministic automated regression coverage.
- Deferred coalescing, recovery, and takeover restoration remain out of scope.
