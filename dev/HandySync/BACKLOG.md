# HandySync Backlog

Future and active work is tracked in [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues).
GitHub Issues are the source of truth for priorities, status, labels, epics, sub-issues, and implementation progress.

This file retains HandySync release boundaries and completed baselines that are not duplicated in issue tracking.
Implemented command serialization, version-aware setup, session checks, awaited teardown, and main-form device controls are described in [architecture](ARCHITECTURE.md).

Intermittent Handy disconnects have also been observed with other services. Wi-Fi instability is a working hypothesis, not a confirmed cause. Diagnostics should distinguish network, service, firmware, credential, and local lifecycle failures.

## Initial-release scope classification

Reviewed against the [approved support scope](../archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
The labels below apply to remaining proposals, not completed baselines. GitHub Issues
owns active priorities and status; these classifications do not make an excluded or
deferred feature an initial-release requirement.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| <a id="multiple-saved-connection-keys"></a>Multiple Saved Connection Keys | Deferred | The protected storage implementation is complete, but the key-management UI remains deferred; retain protected storage and current first-key behavior |
| Authoring Review Comments During Playback | Deferred | Local authoring review comments remain deferred; project/comment-track format and persistence are not selected |
| <a id="reconnect-and-hssp-recovery"></a>Reconnect and HSSP Recovery | Deferred | Automatic recovery and takeover restoration are not promised; existing manual reconnect, visible failures and controlled teardown remain in scope |
| <a id="request-timeouts-and-retries"></a>Request Timeouts and Retries | In scope / Deferred | Bounded requests and shutdown are implemented and verified 2026-11-01 (see the [archived work plan](../archive/2026-11-timeout-shutdown/PLAN.md)); expanded automatic retries/backoff remain deferred |
| Desired State and Command Coalescing | Deferred; lifecycle verification complete 2026-11-08 | Command serialization and lifecycle ordering are verified baselines; stale-command/session-replacement evidence is recorded in the completed [command-ordering plan](../archive/2026-11-command-ordering/PLAN.md) and [verification](../archive/2026-11-command-ordering/VERIFICATION.md); desired-state machinery and command coalescing remain deferred |
| <a id="connection-events"></a>Connection Events | Deferred | SSE and additional event-driven status remain deferred; preserve current polling unless a concrete design replaces it |
| <a id="firmware-and-capabilities"></a>Firmware and Capabilities | In scope / Deferred / Excluded; baseline verification complete | Initial firmware gates and failure reporting are verified in the archived [firmware-capabilities work](../archive/2026-11-firmware-capabilities/PLAN.md); additional model-specific validation remains deferred; firmware installation/update flows are excluded |
| Timing and Diagnostics | In scope / Deferred | Preserve secret-free diagnostics and connection hardware information; the initial-release error-reporting review is recorded in the [archived plan](../archive/2026-11-error-reporting/PLAN.md) and [verification](../archive/2026-11-error-reporting/VERIFICATION.md); expanded drift/RTT metrics and clock-refresh strategies remain deferred |
| <a id="offset-feedback"></a>Offset Feedback | Deferred | Offset reset/range extensions remain deferred while preserving existing device controls |
| Playback Speed Sync | Excluded | Non-1x hardware playback compensation is outside initial scope; existing stop/suppress behavior and resumption at confirmed 1x remain in scope |
| Local Buttplug/Intiface Output | Excluded from initial release; high-priority follow-up | Local Handy BLE/Intiface evaluation is a high-priority follow-up; initial-release boundaries and support for other vendors remain unchanged |
| Verification | In scope | Verify the supported baseline with fake transports and recorded manual checks |
| Completed baselines | In scope | Preserve device controls, protected storage and lifecycle guarantees |

## Completed

- **Main-form device controls:** offset/stroke controls, external-change polling, firmware/capability gates for those controls, and read-only device status. Archived [design](../archive/2026-10-main-form-device-controls/DESIGN.md). Broader capability validation, offset reset/range changes, and connection events remain in GitHub Issues.
- **Protected multi-key storage:** ordered, labeled, per-key DPAPI protection with add, first-key retrieval, and delete operations; atomic persistence and safe failure handling implemented. The key-management UI remains deferred. Task closed by the maintainer on 2026-10-23; archived [design](../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md) preserve results and verification limitations.
- **Device lifecycle baseline:** command serialization, version-aware setup, session checks, and awaited teardown are implemented; see [architecture](ARCHITECTURE.md). Recovery and command coalescing remain tracked in GitHub Issues, not completed by this baseline.
