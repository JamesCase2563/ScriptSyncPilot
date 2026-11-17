# HandySync Backlog

## Table of Contents

- [Multiple Saved Connection Keys](#multiple-saved-connection-keys)
- [Authoring Review Comments During Playback](#authoring-review-comments-during-playback)
- [Reconnect and HSSP Recovery](#reconnect-and-hssp-recovery)
- [Request Timeouts and Retries](#request-timeouts-and-retries)
- [Desired State and Command Coalescing](#desired-state-and-command-coalescing)
- [Connection Events](#connection-events)
- [Firmware and Capabilities](#firmware-and-capabilities)
- [Timing and Diagnostics](#timing-and-diagnostics)
- [Offset Feedback](#offset-feedback)
- [Playback Speed Sync](#playback-speed-sync)
- [Local Buttplug/Intiface Output](#local-buttplugintiface-output)
- [Verification and Order](#verification-and-order)
- [Completed](#completed)

Entries below link to GitHub issues for future work; completed baselines and scope classifications remain summarized here. Implemented command serialization, version-aware setup, session checks, awaited teardown, and main-form device controls are described in [architecture](ARCHITECTURE.md), not listed as wholly unfinished features.

Intermittent Handy disconnects have also been observed with other services. Wi-Fi instability is a working hypothesis, not a confirmed cause. Diagnostics should distinguish network, service, firmware, credential, and local lifecycle failures.

## Initial-release scope classification

Reviewed against the [approved support scope](../archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
Labels below apply to remaining proposals, not completed baselines. Future priorities
do not make an excluded or deferred feature an initial-release requirement.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| Multiple Saved Connection Keys | Deferred | New management UI is not required; retain protected storage and current first-key behavior |
| Authoring Review Comments During Playback | Deferred | New local review workflow; project/comment-track format and persistence remain to be designed |
| Reconnect and HSSP Recovery | Deferred | Automatic recovery/takeover restoration is not promised; existing manual reconnect, visible failures and controlled teardown remain in scope |
| Request Timeouts and Retries | In scope / Deferred | Bounded requests and shutdown implemented and verified 2026-11-01 (see the [archived work plan](../archive/2026-11-timeout-shutdown/PLAN.md)). Expanded automatic retries/backoff are deferred |
| Desired State and Command Coalescing | Deferred; lifecycle verification complete 2026-11-08 | Preserve command serialization and existing debouncing. Stale-command/session-replacement evidence is recorded in the completed [command-ordering plan](../archive/2026-11-command-ordering/PLAN.md) and [verification](../archive/2026-11-command-ordering/VERIFICATION.md); command coalescing and desired-state machinery remain deferred |
| Connection Events | Deferred | SSE and additional event-driven status are not release requirements; preserve current polling |
| Firmware and Capabilities | In scope / Deferred / Excluded; baseline verification complete | Initial-release gates and failure reporting verified in the archived [firmware-capabilities work](../archive/2026-11-firmware-capabilities/PLAN.md); additional model-specific validation is deferred. Firmware installation/update flows are excluded |
| Timing and Diagnostics | In scope / Deferred | Preserve useful, secret-free diagnostics, including connection hardware information; initial-release error-reporting review completed 2026-11-03 ([archived plan](../archive/2026-11-error-reporting/PLAN.md), [verification](../archive/2026-11-error-reporting/VERIFICATION.md)). Expanded drift/RTT metrics and clock-refresh strategies are deferred |
| Offset Feedback | Deferred | Reset/range extensions are not required; preserve existing device-side offset controls |
| Playback Speed Sync | Excluded | Non-1x device playback is outside initial scope; existing stop/suppress behavior and resumption at confirmed 1x remain in scope |
| Local Buttplug/Intiface Output | Excluded from initial release; high-priority follow-up | Prioritize local Handy BLE evaluation for privacy; initial-release boundaries and support for other vendors remain unchanged |
| Verification and Order | In scope | Verify the supported baseline with fake transports and recorded manual checks; the suggested future implementation order is not a release commitment |
| Completed baselines | In scope | Preserve device controls, protected storage and lifecycle guarantees |

## Multiple Saved Connection Keys

Tracked in GitHub issue [#39](https://github.com/JamesCase2563/ScriptSyncPilot/issues/39).

## Authoring Review Comments During Playback

Tracked in GitHub issue [#40](https://github.com/JamesCase2563/ScriptSyncPilot/issues/40).

## Reconnect and HSSP Recovery

Tracked in GitHub issue [#41](https://github.com/JamesCase2563/ScriptSyncPilot/issues/41).

## Request Timeouts and Retries

Tracked in GitHub issue [#42](https://github.com/JamesCase2563/ScriptSyncPilot/issues/42).

## Desired State and Command Coalescing

Tracked in GitHub issue [#43](https://github.com/JamesCase2563/ScriptSyncPilot/issues/43).

## Connection Events

Tracked in GitHub issue [#44](https://github.com/JamesCase2563/ScriptSyncPilot/issues/44).

## Firmware and Capabilities

Tracked in GitHub issue [#45](https://github.com/JamesCase2563/ScriptSyncPilot/issues/45).

## Timing and Diagnostics

Tracked in GitHub issue [#46](https://github.com/JamesCase2563/ScriptSyncPilot/issues/46).

## Offset Feedback

Tracked in GitHub issue [#47](https://github.com/JamesCase2563/ScriptSyncPilot/issues/47).

## Playback Speed Sync

Tracked in GitHub issue [#48](https://github.com/JamesCase2563/ScriptSyncPilot/issues/48).

## Local Buttplug/Intiface Output

Tracked in GitHub issue [#49](https://github.com/JamesCase2563/ScriptSyncPilot/issues/49).

## Verification and Order

Tracked in GitHub issue [#50](https://github.com/JamesCase2563/ScriptSyncPilot/issues/50).

## Completed

- **Main-form device controls:** offset/stroke controls, external-change polling, firmware/capability gates for those controls, and read-only device status. Archived [design](../archive/2026-10-main-form-device-controls/DESIGN.md). Broader capability validation, offset reset/range changes, and connection events remain in the active entries above.
- **Protected multi-key storage:** ordered, labeled, per-key DPAPI protection with add, first-key retrieval, and delete operations; atomic persistence and safe failure handling implemented. Key-management UI remains [proposed](#multiple-saved-connection-keys). Task closed by the maintainer on 2026-10-23; archived [design](../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md) preserve results and verification limitations.
- **Device lifecycle baseline:** command serialization, version-aware setup, session checks, and awaited teardown are implemented; see [architecture](ARCHITECTURE.md). Recovery and command coalescing remain active work, not completed by this baseline.
