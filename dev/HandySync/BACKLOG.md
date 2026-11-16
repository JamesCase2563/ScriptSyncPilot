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

Entries below track future work; status notes call out partial implementations. Implemented command serialization, version-aware setup, session checks, awaited teardown, and main-form device controls are described in [architecture](ARCHITECTURE.md), not listed as wholly unfinished features.

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

**Priority:** Low. **Status:** Key-management UI proposed; storage implementation is recorded under [Completed](#completed).

Add a HandySync UI for managing multiple saved connection keys. Let users assign a label to each saved key, choose the active key from a dropdown, add keys, and delete unused keys with clear confirmation. Do not display the key value after it has been saved. Until this UI is implemented, HandySync uses the first key in the protected saved-key collection; the implemented storage preserves order and supports adding, retrieving, and deleting entries.

The GUI change should build on the protected multi-key storage/API rather than introduce a second persistence format. Keep key values protected at rest, do not log them, and make the empty-key state explicit after deleting the last key. See the [archived design](../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) for the implemented storage behavior and security constraints.

## Authoring Review Comments During Playback

**Priority:** To be decided. **Status:** Proposed idea; coordinate with editor multi-track work.

Let users record comments about script-authoring improvements in HandySync while
using/reviewing a script. Capture the playback timestamp when the comment is initiated
so typing time does not move the note away from the moment being reviewed.

- Allow comments to be written to a project's notes/review track or a separate comment track associated with the script, for later review in FunscriptEditor.
- Keep comments separate from movement actions and device output; recording feedback must not alter the script being played or upload comments to Handy services.
- Coordinate the representation with the editor's [track types](../FunscriptEditor/BACKLOG.md#track-types). Project integration versus standalone comment storage, file association, and time/offset mapping remain design decisions.
- Preserve existing project data and unrelated tracks. Define safe save/conflict handling when the editor and HandySync access the same project, and surface save failures without losing entered comments.

## Reconnect and HSSP Recovery

**Priority:** High. **Status:** Deferred; not selected for the initial release. Only the separate bounded-request and shutdown prerequisite is implemented (see [Request Timeouts and Retries](#request-timeouts-and-retries) and the [archived completed work plan](../archive/2026-11-timeout-shutdown/PLAN.md)). Unavailable/recovering states, takeover/reboot detection, automatic reconnect, HSSP restoration and retries/backoff remain unimplemented; future recovery should build on the existing request budgets.

Introduce explicit unavailable/recovering/ready states for transient HTTP failures, device disconnects, failed play, failed synctime, and takeover by another app. Keep observing mpv throughout recovery.

Online players can switch the device mode or replace the HSSP script while HandySync is connected; HandySync currently sets mode and script only on connect and does not notice. Record `mode_session_id` from the `PUT /mode2` response, then check `GET /mode` periodically, potentially combined with future offset/stroke polling. A different mode or `mode_session_id` means another client took over: log it and enter recovery to restore HSSP mode and the script. A script replaced within HSSP mode may not change `mode_session_id`; verify whether `GET /hssp/state` (for example `stream_id`) exposes it. Decide whether takeover recovers automatically or waits for the user, to avoid fighting an online player that is still open.

Device reboots and server reconnects can also silently reset device state. Record `boot_session_id` and `socket_session_id` from `GET /sids` after setup and check them in the same periodic poll. A changed `boot_session_id` means the device rebooted; a changed `socket_session_id` within the same boot means it reconnected to the server. Either way, treat mode, HSSP setup, and the device-to-server clock as lost: enter recovery, re-estimate the client/server clock offset, re-read and adopt offset/stroke using the main-form device-control policy, and log which ID changed. Verify whether HSTP offset and stroke survive a reboot or reconnect.

1. Suspend normal device synchronization.
2. Retry connectivity with exponential backoff, for example 1, 2, 4, 8, 15, and 30 seconds.
3. Recheck information, firmware, and capabilities.
4. Inspect HSSP state: reuse valid setup or recreate it with the hosted script URL, rehosting if necessary.
5. Resume at current mpv position only when playing; remain stopped while paused.
6. Return to periodic synchronization without resetting video.

Do not depend on synctime to restart stopped playback. Log whether setup was reused/recreated. Evaluate buffering and seek behavior for large scripts against current API documentation.

## Request Timeouts and Retries

**Priority:** High. **Status:** Timeout and shutdown bounds implemented and verified 2026-11-01; see the [archived completed work plan](../archive/2026-11-timeout-shutdown/PLAN.md). Automatic retries/backoff remain proposed and deferred.

Implemented budgets use the upper end of the planned ranges: connectivity/status and HSSP control 10 seconds, each time sample 5 seconds, upload 60 seconds. Shutdown finishes if the network is unavailable, making one bounded stop attempt.

Deferred retry work: retry transient resets, timeouts, DNS failures, HTTP 408/429/5xx. Respect Retry-After/rate-limit information. Do not blindly retry authentication failures, invalid requests, or malformed scripts.

## Desired State and Command Coalescing

**Priority:** Medium. **Status:** Coalescing proposed and deferred; lifecycle/order verification completed 2026-11-08 in [command-ordering](../archive/2026-11-command-ordering/PLAN.md). Serialization is implemented.

Keep the single device-command gate. Add newest-desired-state handling to suppress duplicate play/pause, coalesce rapid seeks, supersede stale sync/offset requests, and reduce unnecessary traffic. Record command type/result. A semaphore alone does not provide coalescing or a recovery state machine; no separate controller host is required.

## Connection Events

**Priority:** Medium-high. **Status:** Proposed.

Evaluate SSE connection/error events with connectivity polling fallback. Verify event names against the current API reference. Reconnect the SSE reader with backoff and cancel it cleanly when the device connection ends.

Feed device warnings into the main-form device status area from [main-form device controls](../archive/2026-10-main-form-device-controls/DESIGN.md#device-status-read-only): `temp_high`/`temp_ok`, `slider_blocked`/`slider_unblocked`, and `battery_changed` (battery level only when capabilities report a battery). `stroke_changed` can replace stroke polling. Never log the SSE URL, which carries the API key.

## Firmware and Capabilities

**Priority:** Medium. **Status:** Baseline gate verification complete in the archived [firmware-capabilities work](../archive/2026-11-firmware-capabilities/PLAN.md) and [verification record](../archive/2026-11-firmware-capabilities/VERIFICATION.md). Unknown firmware status and slider capability fail closed; unsupported/unverified firmware stops before further API v3 setup/control operations. Additional model-specific validation remains deferred; firmware update flows remain excluded.

The main-form offset/stroke controls call `/info` and are enabled only for firmware 4+ with `fw_status` other than update-required. `/capabilities` is read on connect; stroke controls are disabled and stroke requests are suppressed when `slider` is zero. This is a control-specific gate, not a gate on the rest of HSSP setup. Remaining work is broader firmware/capability validation before setup, if required, and handling additional model-specific capabilities. Vendor documentation requires firmware 4 or newer for API v3. Script loading stays independent of device availability.

Firmware updating is out of scope for the entire ScriptSyncPilot application. Do not implement update UI/flows, download or distribute firmware, or initiate updates. Apps may report compatibility requirements; user documentation should link to the manufacturer's official [firmware guidance](https://links.handyfeeling.com/docs-api-handy-rest-v3-fw3). Firmware installation remains the user's action through official vendor channels.

## Timing and Diagnostics

**Priority:** Medium. **Status:** Proposed.

- Refresh clock offset every 5-10 minutes and after recovery/network changes. Consider lowest-latency samples rather than averaging every sample equally.
- Log offset changes and minimum/maximum/median round-trip time, request durations, HTTP error classification, and last successful request/HSSP state.
- Track reconnect count and disconnected time; report firmware/transport only where exposed by the API.
- Add structured transition/recovery logging, with debug verbosity for successful periodic requests. Never log application or connection keys.
- Apply updated estimates to subsequent sync/recovery without restarting playback solely because clock offset changed.
- Log the drift observed at each synctime correction. If it stays noisy on unstable networks, evaluate other `hssp/synctime` `filter` values (currently fixed at 0.5) and expose it only as an advanced/diagnostic setting. Tune after [Playback Speed Sync](#playback-speed-sync) has verified synctime behavior at non-1x rates.
- Read and log the device-to-server round-trip delay (`rtd` from `GET /hstp/info`) on connect and after recovery, alongside HandySync's own client/server round-trip times, so device Wi-Fi problems can be told apart from PC network problems. If it is high, suggest running device clock sync in handyverse.com. Do not trigger `/hstp/clocksync` from HandySync; device clock sync is device-wide and handled by the device or other apps, while HandySync's own client/server offset estimation stays as is.

## Offset Feedback

**Priority:** Medium. **Status:** Partial; basic device-side offset control is implemented with the completed main-form device controls.

The main form displays raw HSTP offset, adopts the device value on connect, polls for external changes, and debounces user writes. Remaining work is an explicit reset-to-zero and any range extension beyond the current +/- 1000 ms / 10 ms steps, if device limits and user need justify it. Do not restore a local mpv-time offset or reapply stale saved values on connect.

## Playback Speed Sync

**Priority:** High. **Status:** Proposed; follow-up to the editor's [video playback speed](../archive/2026-10-video-playback-speed/PLAN.md) work.

Non-1x device playback is currently unsupported. HandySync observes mpv speed and, while the rate is unknown or not 1x, stops HSSP playback, suppresses seek/periodic/offset synchronization, and warns the user. Confirmed 1x playback resumes synchronization from the current media position. This avoids sending mismatched device timing, but does not drive the device at non-1x rates. HandySync does not write mpv speed or provide speed controls.

- Common's `MpvSession` speed observation/change events and rate-aware seek detection are implemented; keep IPC observation in Common and never write speed to mpv.
- Verify whether HSSP supports non-1x `playback_rate` and how `hssp/synctime` `current_time` behaves at non-1x rates (v3 lists `playbackrate` for HSP, not HSSP).
- If the API supports it, resume non-1x device playback by passing the observed rate as `playback_rate` in `hssp/play`. On a speed change while playing, re-issue play at the current media position through the device-command gate, debounced and latest-wins. While paused, retain the observed rate for the next play.
- Device time stays in media time; the device-side HSTP offset is unaffected by rate.
- Fast rates can exceed the slider's speed limits (`/settings/slider` `x_max_speed`); decide whether to warn above a threshold or only log. Ignore invalid or nonpositive observations.
- Test non-1x HSSP playback only after API support is confirmed: rate changes while playing/paused, seeks, drift at 0.5x and 1.5x, reconnect with non-1x already set, and unchanged 1x behavior.

## Local Buttplug/Intiface Output

**Priority:** High; privacy-focused follow-up, raised by the maintainer on 2026-10-25. **Status:** Proposed alternative; not an initial-release requirement.

Evaluate `HandySync -> Buttplug WebSocket -> Intiface Central -> local Bluetooth` as a separate backend behind a suitable output interface, not mixed into the HSSP client.

This avoids cloud hosting, application-key dependency, and server-time sync. It requires local funscript scheduling, latency/command-loss/back-pressure handling, and compatibility checks, and does not provide HSSP buffering. A separate ControllerHost is not a prerequisite.

The privacy goal is to keep script content and playback commands on the local
machine/Bluetooth link rather than sending them to Handy's hosting and REST services.
First evaluate the available H01 devices and their recorded firmware builds; this
does not authorize expanding support to other vendors.

- Verify BLE discovery, connection and position/duration control through a locally bound Intiface endpoint before designing the full backend.
- Verify operation with internet access disabled after prerequisites are installed. Local mode must not silently fall back to cloud hosting or REST control.
- Identify any independent device, Intiface or dependency network activity before claiming fully offline/private operation; local control alone does not prove the absence of telemetry.
- Preserve explicit backend selection, pause/seek behavior, normal-speed safety gates, stop/disconnect and shutdown guarantees. Validate timing and command-loss behavior before claiming parity with HSSP.

## Verification and Order

Use the [shared test backlog](../BACKLOG.md) for fake-transport ordering, replacement, recovery, and idempotent teardown tests. Preserve current lifecycle guarantees when adding SSE/retries; background failures must not escape after close.

The initial-release failure-reporting, command-ordering, and firmware/capability verification work is tracked in the archived [error-reporting plan](../archive/2026-11-error-reporting/PLAN.md), [command-ordering](../archive/2026-11-command-ordering/PLAN.md), and [firmware-capabilities](../archive/2026-11-firmware-capabilities/PLAN.md), respectively. These are bounded release-gate verification efforts; they do not select automatic recovery, coalescing, SSE, or richer timing diagnostics.

Suggested later order: prioritize local Handy BLE/Intiface feasibility and backend work for privacy ahead of deferred automatic HSSP recovery, expanded coalescing, SSE and richer timing diagnostics/offset feedback. The local backend remains outside the initial-release gate unless separately approved. This is not a dated delivery commitment.

## Completed

- **Main-form device controls:** offset/stroke controls, external-change polling, firmware/capability gates for those controls, and read-only device status. Archived [design](../archive/2026-10-main-form-device-controls/DESIGN.md). Broader capability validation, offset reset/range changes, and connection events remain in the active entries above.
- **Protected multi-key storage:** ordered, labeled, per-key DPAPI protection with add, first-key retrieval, and delete operations; atomic persistence and safe failure handling implemented. Key-management UI remains [proposed](#multiple-saved-connection-keys). Task closed by the maintainer on 2026-10-23; archived [design](../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md) preserve results and verification limitations.
- **Device lifecycle baseline:** command serialization, version-aware setup, session checks, and awaited teardown are implemented; see [architecture](ARCHITECTURE.md). Recovery and command coalescing remain active work, not completed by this baseline.
