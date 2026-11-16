# Firmware and Capabilities Plan

**Status:** Complete 2026-11-13; focused simulated verification passed. Overall production-readiness decision remains open.
**Goal:** Close production-readiness reliability priority 4 by verifying existing firmware/capability gates and clear handling of unavailable data.

## Scope

- Verify firmware version/status gates for existing controls and setup operations.
- Verify capability-based suppression/enabling of existing slider/stroke behavior.
- Verify missing, malformed, and failed reads leave safe and clearly reported state.
- Correct confirmed defects and add focused automated tests.

Out of scope: firmware update UX, new device/model/protocol support, broad hardware certification, generic capability frameworks, and automatic recovery.

## Milestones

### 1. Characterize contracts

- [x] Map firmware and capability reads and all dependent controls/requests in [DESIGN.md](DESIGN.md) (M1.1).
- [x] Reconcile implementation with the [HandySync backlog](../../HandySync/BACKLOG.md#firmware-and-capabilities), approved [support scope](../2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md), and official API v3 requirement (M1.2).
- [x] Define expected behavior for supported, unsupported, update-required, absent, malformed, and failed values without assuming undocumented status meanings (M1.3).

### 2. Verify and repair

- [x] Add focused fake-response tests for version boundaries/status and capability boundaries/read failures.
- [x] Confirm unsupported/unknown firmware cannot proceed to API v3 control/setup calls and the UI status explains the limitation.
- [x] Confirm local script availability is preserved when device setup is gated; firmware/capability failures do not initiate firmware updates.
- [x] Fix confirmed fail-open/unknown-status behavior and add regression coverage.

### 3. Close

- [x] Run focused tests and record exact commands/results in [VERIFICATION.md](VERIFICATION.md).
- [x] Keep compatibility evidence limited to actually verified results; preserve the distinction between simulated tests and physical ENV-001 evidence.
- [x] Link conclusions from the [production-readiness plan](../2026-11-production-readiness/PLAN.md); acceptance criteria were met before archiving.

## Acceptance criteria

- Existing controls respect the implemented firmware and capability gates.
- Missing/failed/invalid reads fail safely, do not produce unsupported writes, and are reported clearly.
- Firmware v4+ vendor minimum is described as a requirement, not proof that every v4+ device is compatible.
- Script loading and unrelated operations are not unnecessarily coupled to device-control capability.
- No firmware update flow or expanded compatibility promise is introduced.
- Changed behavior has automated regression coverage and evidence is recorded.
