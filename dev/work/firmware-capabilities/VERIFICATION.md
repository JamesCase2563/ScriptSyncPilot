# Firmware and Capabilities Verification

**Status:** Milestone 1 characterization completed 2026-11-13; no focused verification runs have been made yet. Existing compatibility records are background evidence, not verification of this work item.

## Scope and result

Production-readiness priority 4: existing controls must respect firmware/capabilities, and missing or failed reads must be reported clearly. See [design](DESIGN.md), [plan](PLAN.md), the [production-readiness plan](../production-readiness/PLAN.md), [support scope](../production-readiness/SCOPE-AND-LIMITATIONS.md), and [tested environments](../production-readiness/TESTED-ENVIRONMENTS.md).

## Gate matrix

| Input/scenario | Expected behavior | Result | Evidence/notes |
| --- | --- | --- | --- |
| Firmware v4 boundary and later | Existing supported control behavior follows the vendor API requirement and app gate | Not run | |
| Firmware below v4 or invalid version | Device controls remain unavailable; no unsupported writes | Not run | |
| Firmware status update-required | Controls follow the existing update-required gate; message is clear | Not run | Do not infer other status meanings. |
| Missing/failed `/info` | Controls fail closed and failure is reported | Not run | |
| Zero slider capability | Stroke UI/requests are suppressed | Not run | |
| Missing/failed/malformed `/capabilities` | No unsafe stroke request; state/reporting is explicit | Not run | |
| Script loading when device controls unavailable | Matches intended independent behavior | Not run | Confirm against implementation and API contract. |

## Runs

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| — | — | No verification run for this work item yet | — |

## Existing compatibility evidence

See ENV-001 in [TESTED-ENVIRONMENTS.md](../production-readiness/TESTED-ENVIRONMENTS.md#env-001). It records two H01 device firmware builds and maintainer-reported successful workflows, with connection/setup corroborated by logs. It does not establish broad model compatibility and does not replace the simulated boundary/failure checks above.

## Findings and closure

- Milestone 1 findings: official API v3 docs require firmware v4+ for device control and enumerate firmware status 0/1/2. Current `IsDeviceControlsFirmwareSupported` accepts undocumented status values other than 2, while connection still proceeds to `/mode`, clock sampling, upload, and HSSP setup when the control gate is false. Capability read failure currently sets `_hasDeviceSlider = true`; an existing test asserts this fail-open behavior. These are characterization findings; confirm expected boundaries and add focused tests during M2 before changing behavior.
- Confirmed defects: none closed in this work item; M2 verification has not started.
- Changes and regression tests: none recorded.
- Limitations: boundary and failure behavior remain unverified; support claims remain limited to ENV-001 and the approved scope.
- Release-gate conclusion: open; this work item is not verified complete.
