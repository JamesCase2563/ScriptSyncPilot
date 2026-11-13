# Firmware and Capabilities Design

**Status:** Complete 2026-11-13; focused simulated verification passed. Overall production-readiness decision remains open.
**Affected projects:** HandySync.
**Production-readiness priority:** 4, firmware and capabilities.

## Goal

Verify that existing firmware and capability gates prevent unsupported device controls, that setup behavior respects the documented Handy REST API v3 firmware requirement, and that missing or failed reads are reported clearly without unsafe commands or misleading state.

This is a bounded review of the currently supported Handy API v3/HSSP baseline. Firmware installation/update flows, additional device models, alternate protocols, and broader compatibility claims are out of scope.

## Existing baseline and inputs

- At the start of this review, the [HandySync backlog entry](../../HandySync/BACKLOG.md#firmware-and-capabilities) documented firmware-gated offset/stroke controls and zero-slider suppression, while broader API v3 setup validation and model-specific capabilities remained open. This work closes the generic firmware/setup boundary; model-specific capability review remains deferred.
- The [support scope](../../work/production-readiness/SCOPE-AND-LIMITATIONS.md) limits support claims to recorded tests and states vendor firmware v4+ requirement; firmware updating is excluded.
- [ENV-001](../../work/production-readiness/TESTED-ENVIRONMENTS.md#env-001) records two H01 devices/firmware combinations and limited successful workflows. It is not broad device certification.
- Current implementation uses `MainForm` firmware validation and `GetCapabilitiesAsync`; inspect current request ordering and failure behavior before deciding a change is required.

## M1.1 Request and gate map

Reviewed `MainForm`, `HandyApiClient`, and the existing device-control tests. Device REST calls share `_deviceCommandGate`; local editor script loading is independent of a Handy connection. The connection path requires a loaded script, then performs device calls in this order:

| Stage / request | Current behavior and gate | M1 observation |
| --- | --- | --- |
| `GET /connected` | Serialized; false aborts connection. | Connection prerequisite, independent of firmware-control gate. |
| `GET /info` | Serialized in `CheckDeviceFirmwareAsync`. Parses a major version and firmware status; determines `_deviceControlsSupported`. Non-timeout errors disable offset/stroke controls and permit connection to continue; timeout aborts connection. | Missing version/status fails the control gate. Current predicate rejects only status `2`; other integer values are accepted. Vendor schema only defines 0, 1, and 2. |
| `PUT /mode` (HSSP) | Sent after `/info` regardless of `_deviceControlsSupported`. | Vendor v3 docs say devices below firmware v4 cannot be controlled with API v3. Whether connection should stop before device-control operations, while leaving local script editing available, needs resolution in M2. |
| Clock samples (`GET /servertime`) | Serialized after setting mode, regardless of `_deviceControlsSupported`. | Same firmware-contract question as mode/setup; these are device synchronization prerequisites, not local script loading. |
| `GET /capabilities` | Read only when `_deviceControlsSupported`. Positive `slider` count means present; zero or a missing property deserializes as zero and disables stroke. A non-timeout read error is logged but defaults `_hasDeviceSlider = true`. | Failed reads currently fail open for stroke controls; malformed/incomplete result shapes need explicit coverage and safe semantics. |
| `GET /settings/slider` | Read when firmware gate passes; failure clears temperature thresholds/tooltip, but does not disable unrelated offset/stroke controls. | Optional physical/temperature details; failure is surfaced in the log. |
| `GET /hstp/offset`, `GET /slider/stroke` | Read when firmware gate passes; stroke read only if capability count is positive or capability read failed. Failures mark the corresponding initial state not applied. | Existing control-specific gates are present. |
| Debounced `PUT /hstp/offset` | Requires `_deviceControlsSupported`, current client, active write token, and no disconnect; rechecked after gate acquisition. | Firmware-gated and target/lifecycle guarded. |
| Debounced `PUT /slider/stroke` | Requires firmware gate and `_hasDeviceSlider`, plus current client/write token and no disconnect; rechecked after gate acquisition. | Zero capability suppresses reads/writes. Capability read failure currently allows writes. |
| Upload and HSSP setup | After control reads, `EnsureCurrentScriptInstalledAsync` uploads the CSV and calls HSSP setup; it does not check `_deviceControlsSupported`. | Existing design intentionally keeps script data handling separate from control-specific gates, but actual remote upload/setup are device/API operations. Reconcile with the v4 minimum before accepting this boundary. |

## M1.2 Contract and scope reconciliation

- The official [Handy REST API v3 OpenAPI specification](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml) says firmware older than v4 must be updated to v4 before the device can be controlled through API v3. This is a vendor requirement, not a claim that every v4+ combination is supported by this project.
- Its `FirmwareStatus` enum is `0` up-to-date, `1` update-available, and `2` update-required. Therefore status `1` is distinct from update-required; the existing gate accepting `1` is consistent with the enum. Values outside the enum are not documented and should not be treated as known-compatible without evidence.
- The same spec defines `DeviceCapabilities.slider` as an integer count and gives `0` as the no-slider value in the example. It does not define missing/invalid capability data as equivalent to a positive slider count.
- Project [scope](../../work/production-readiness/SCOPE-AND-LIMITATIONS.md) excludes firmware installation and treats vendor minimums separately from project-tested compatibility. [ENV-001](../../work/production-readiness/TESTED-ENVIRONMENTS.md#env-001) records two H01 builds (4.3.0, status 0; and 4.2.2, status 1) with maintainer-reported workflow success. It provides no below-v4, unknown-status, absent-capability, or failed-capability-read evidence.
- Existing tests include the v4/status gate helper, a zero-slider suppression case, read failures, and a firmware-info timeout. They do not cover unknown status codes or connect-time behavior below v4. The existing failed-capability test currently expects stroke control to remain usable after `/capabilities` returns 503; that expectation conflicts with a fail-closed interpretation and must be deliberately resolved, not mistaken for vendor guidance.

## M1.3 Expected behavior for verification

These are the review's proposed test expectations, not new runtime behavior:

| Input | Expected outcome to test |
| --- | --- |
| Firmware v4+, status 0 | Existing supported API v3 workflows may proceed; this does not imply untested model compatibility. |
| Firmware v4+, status 1 (update available) | Continue to allow current controls; do not describe this as update-required. |
| Firmware below v4 | Do not issue API v3 device-control/setup requests that assume compatibility. Keep local script editing/loading independent. Verify user-facing status and connect cleanup. |
| Status 2 (update required) | Offset/stroke controls remain disabled. Decide whether all API v3 control/setup work is also blocked based on the vendor contract. Never initiate firmware update. |
| Missing/malformed version or missing status | Treat firmware compatibility as unknown; do not enable firmware-gated controls. Report the reason without exposing credentials. |
| Status outside documented enum | Treat as unknown and fail closed for gated device operations; do not infer undocumented status semantics. |
| `/info` failed or malformed | Do not enable firmware-gated controls. Preserve local script editing. Verify whether timeout vs other failure should abort device connection, while avoiding later API v3 setup on unverified firmware. |
| `/capabilities` returns `slider: 0` | Disable stroke UI and suppress stroke read/write; unrelated offset behavior follows its own gate. |
| `/capabilities` missing, malformed, or failed | Represent capability as unknown, report it, and suppress stroke-only operations until slider presence is established; do not convert failure to “slider present.” |
| `/settings/slider` failed | Keep unrelated controls usable; clear stale optional physical/temperature display data and report the failed read. |

No firmware-status meaning beyond the official enum is assumed. M2 should first settle the connection/setup boundary against the vendor statement and then implement regression tests for these cases.

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
- Compatibility claims remain limited to documented evidence; findings link to the [production-readiness plan](../../work/production-readiness/PLAN.md).
