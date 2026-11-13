# Command Ordering Plan

**Status:** Complete 2026-11-08; full solution suite passed 326/326. Residual test boundaries are in [VERIFICATION.md](VERIFICATION.md).
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

- [x] Add focused fake-transport tests for uncovered transitions and late completions (queued callbacks, late play/resync success, and first sync after replacement; see [DESIGN.md](DESIGN.md)).
- [x] Fix the confirmed stale-success reporting and stale sync-position defects while preserving the single command gate and manual reconnect flow.
- [x] Verify no old-target command is sent and no late old-target completion alters current UI/readiness (M2.3; see [VERIFICATION.md](VERIFICATION.md)).

### 3. Close

- [x] Run focused and full tests and record exact commands/results in [VERIFICATION.md](VERIFICATION.md) (M2.3 focused run: 11/11; M3 full suite: 326/326).
- [x] Record residual limits, including waveform IPC seek replacement and guarantees that depend on cooperative transport cancellation, in [VERIFICATION.md](VERIFICATION.md).
- [x] Link evidence from the [production-readiness plan](../production-readiness/PLAN.md); the parent release decision remains open.

## Acceptance criteria

- Commands remain serialized for a given client.
- No queued stale play/seek/control command is issued after disconnect or target replacement.
- Late responses from prior Handy/mpv targets do not mark the new target ready or produce misleading UI/log outcomes.
- Teardown/disposal remains safe and follows the documented bounded/deferred lifecycle.
- New or changed behavior has deterministic automated regression coverage.
- Deferred coalescing, recovery, and takeover restoration remain out of scope.
