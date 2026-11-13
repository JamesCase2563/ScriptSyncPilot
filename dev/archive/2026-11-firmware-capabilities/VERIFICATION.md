# Firmware and Capabilities Verification

**Status:** Complete 2026-11-13; focused simulated verification passed. No new physical-device compatibility claim is made.

## Scope and result

Production-readiness priority 4: existing controls must respect firmware/capabilities, and missing or failed reads must be reported clearly. See [design](DESIGN.md), [plan](PLAN.md), the [production-readiness plan](../../work/production-readiness/PLAN.md), [support scope](../../work/production-readiness/SCOPE-AND-LIMITATIONS.md), and [tested environments](../../work/production-readiness/TESTED-ENVIRONMENTS.md).

## Gate matrix

| Input/scenario | Expected behavior | Result | Evidence/notes |
| --- | --- | --- | --- |
| Firmware v4 boundary and later | Firmware v4+ with status 0/1 is accepted; v5/status 0 also accepted by the gate | Passed | `DeviceSliderControlTests.FirmwareGateRequiresVersionFourAndNotUpdateRequired` |
| Firmware below v4 or invalid/unknown status | Connection stops after `/connected` and `/info`; no `/mode`, clock, upload, setup, or playback call follows | Passed | `HandyApiTimeoutTests.UnsupportedOrUnverifiedFirmwareStopsBeforeFurtherApiV3Operations`; status 3, below v4, malformed version/status cases. |
| Firmware status update-required | Connection stops with an update-required status; no update is initiated | Passed | Same connection test; status 2 case. |
| Missing/failed/malformed `/info` | Firmware remains unknown; connection stops before further API v3 operations. Info timeout also reports unverified firmware. | Passed | Missing status, device error, invalid JSON, and timeout cases. |
| Zero slider capability | Stroke UI/read/write are suppressed | Passed | `DeviceControlPollingTests.MissingInvalidOrZeroSliderCapabilitySuppressesStrokeOperations` (`slider: 0`) |
| Missing/failed/malformed `/capabilities` | Capability stays unknown; stroke UI/read/write fail closed, while offset control can remain usable | Passed | Same test for missing/negative/wrong-type values and `DeviceControlPollingTests.FailedCapabilityReadDisablesStrokeWhileOptionalSettingFailuresLeaveOtherControlsUsable` for HTTP 503. |
| Script remains loaded when device setup is gated | Existing local script remains present; no upload occurs for unsupported/unverified firmware | Passed | Firmware connection integration cases assert local script retention and no request to the hosting handler. Existing local script loading tests remain unchanged. |

## Runs

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| 2026-11-13 | Windows; .NET SDK 10.0.401; simulated HTTP handlers | `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~DeviceSliderControlTests|FullyQualifiedName~DeviceControlPollingTests" --verbosity normal` | Passed 66/66; build succeeded with 0 warnings/errors. |
| 2026-11-13 | Windows; .NET SDK 10.0.401; simulated HTTP handlers | `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~UnsupportedOrUnverifiedFirmwareStopsBeforeFurtherApiV3Operations|FullyQualifiedName~FirmwareReadTimeout_AbortsConnectBeforeModeUploadSetupOrPlay|FullyQualifiedName~UploadTimeout_DoesNotSendHsspSetupOrPlay_AndDoesNotLeaveHsspReady" --verbosity minimal` | Passed 8/8; build succeeded. |
| 2026-11-13 | Windows; .NET SDK 10.0.401; simulated HTTP handlers | `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~UnsupportedOrUnverifiedFirmwareStopsBeforeFurtherApiV3Operations|FullyQualifiedName~FirmwareReadTimeout_AbortsConnectBeforeModeUploadSetupOrPlay" --verbosity minimal` | Passed 7/7 after adding the final script-retention assertion. |

## Existing compatibility evidence

See ENV-001 in [TESTED-ENVIRONMENTS.md](../../work/production-readiness/TESTED-ENVIRONMENTS.md#env-001). It records two H01 device firmware builds and maintainer-reported successful workflows, with connection/setup corroborated by logs. It does not establish broad model compatibility and does not replace the simulated boundary/failure checks above.

## Findings and closure

- Milestone 1 findings: official API v3 docs require firmware v4+ for device control and enumerate status 0/1/2. Previously, unknown status values passed the firmware gate, and failed slider-capability reads assumed a slider was present.
- Confirmed defects and fixes: unknown firmware status now fails closed; below-v4, update-required, and unverified firmware stop connection before mode, clock, capability/control reads, upload, and HSSP setup. Failed, missing, malformed, or invalid slider capability data now disables stroke operations without disabling independently gated offset behavior. Timeout and read failures receive explicit unknown/unverified status. No firmware update is performed.
- Changes and regression tests: firmware gate boundary tests; connect-flow tests for below-v4, status 2, unknown status, missing data, malformed JSON, device error and timeout; slider capability tests for zero, missing, negative, wrong-type and failed reads. M2 focused tests passed as listed above.
- Limitations: all new boundary/failure evidence is simulated. Physical-device evidence remains limited to ENV-001 and its two recorded H01 firmware builds; no broader compatibility claim is made.
- Release-gate conclusion: priority 4 verification is complete for the documented Handy API v3/HSSP baseline. Overall production-readiness decision remains open; simulated tests do not expand ENV-001 hardware compatibility evidence.
