# Firmware and Capabilities Plan

**Status:** In progress; Milestone 1 completed 2026-11-13. Milestone 2 verification and any confirmed repairs remain.
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
- [x] Reconcile implementation with the [HandySync backlog](../../HandySync/BACKLOG.md#firmware-and-capabilities), approved [support scope](../production-readiness/SCOPE-AND-LIMITATIONS.md), and official API v3 requirement (M1.2).
- [x] Define expected behavior for supported, unsupported, update-required, absent, malformed, and failed values without assuming undocumented status meanings (M1.3).

### 2. Verify and repair

- [ ] Add focused fake-response tests for version boundaries/status and capability boundaries/read failures.
- [ ] Confirm unsupported/unknown controls cannot issue device writes and UI status explains the limitation.
- [ ] Confirm setup/script loading behavior remains consistent with its intended gate and documented scope.
- [ ] Fix only confirmed defects and add regression coverage.

### 3. Close

- [ ] Run focused tests and record exact commands/results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Update compatibility evidence only with actually verified results; keep physical and simulated evidence distinct.
- [ ] Link conclusions from the [production-readiness plan](../production-readiness/PLAN.md); archive only after acceptance criteria are met.

## Acceptance criteria

- Existing controls respect the implemented firmware and capability gates.
- Missing/failed/invalid reads fail safely, do not produce unsupported writes, and are reported clearly.
- Firmware v4+ vendor minimum is described as a requirement, not proof that every v4+ device is compatible.
- Script loading and unrelated operations are not unnecessarily coupled to device-control capability.
- No firmware update flow or expanded compatibility promise is introduced.
- Changed behavior has automated regression coverage and evidence is recorded.
