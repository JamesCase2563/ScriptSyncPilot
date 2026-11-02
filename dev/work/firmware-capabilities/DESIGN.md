# Firmware and Capabilities Design

**Status:** Proposed; investigation and verification not started.
**Affected projects:** HandySync.
**Production-readiness priority:** 4, firmware and capabilities.

## Goal

Verify that existing firmware and capability gates prevent unsupported device controls, that setup behavior respects the documented Handy REST API v3 firmware requirement, and that missing or failed reads are reported clearly without unsafe commands or misleading state.

This is a bounded review of the currently supported Handy API v3/HSSP baseline. Firmware installation/update flows, additional device models, alternate protocols, and broader compatibility claims are out of scope.

## Existing baseline and inputs

- The [HandySync backlog entry](../../HandySync/BACKLOG.md#firmware-and-capabilities) says offset/stroke controls are enabled for firmware 4+ with `fw_status` other than update-required, capabilities are read on connect, and stroke operations are suppressed when `slider` is zero. Broader setup validation and model-specific capabilities remain open.
- The [support scope](../production-readiness/SCOPE-AND-LIMITATIONS.md) limits support claims to recorded tests and states vendor firmware v4+ requirement; firmware updating is excluded.
- [ENV-001](../production-readiness/TESTED-ENVIRONMENTS.md#env-001) records two H01 devices/firmware combinations and limited successful workflows. It is not broad device certification.
- Current implementation uses `MainForm` firmware validation and `GetCapabilitiesAsync`; inspect current request ordering and failure behavior before deciding a change is required.

## Proposed approach

1. Map `/info` firmware version/status, `/capabilities`, slider settings, offset/stroke reads and writes, and HSSP setup/play prerequisites.
2. Compare actual behavior with official API v3 requirements and the approved product scope. Distinguish a vendor requirement from project-tested compatibility.
3. Verify firmware version parsing, missing/unknown status, update-required status, capability values (including zero sliders), and failed/malformed reads with fake responses.
4. Confirm each gate affects only the operation/control it is intended to protect. Preserve script loading where device controls are unavailable, as documented.
5. Fix only confirmed unsafe, misleading, or out-of-contract behavior; document uncertainty rather than guessing unknown firmware status semantics.

## Constraints

- Never install, download, distribute, or initiate firmware updates. Direct users to official manufacturer guidance.
- Do not claim compatibility beyond the approved matrix or require unsupported physical devices for verification.
- Do not block unrelated script loading or supported operations unless the API contract or a demonstrated safety requirement requires it.
- Do not add new model support, a generalized capability framework, retries, or a recovery state machine.

## Completion criteria

- A gate matrix records firmware/status/capability inputs, expected enabled operations, actual behavior, and failure reporting.
- Tests cover boundary, absent, malformed, unsupported, and failed-read cases relevant to existing gates.
- Any changed behavior is covered by regression tests and exact evidence is recorded in [VERIFICATION.md](VERIFICATION.md).
- Compatibility claims remain limited to documented evidence; findings link to the [production-readiness plan](../production-readiness/PLAN.md).
