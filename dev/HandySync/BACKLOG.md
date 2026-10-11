# HandySync Backlog

## Table of Contents

- [Reconnect and HSSP Recovery](#reconnect-and-hssp-recovery)
- [Request Timeouts and Retries](#request-timeouts-and-retries)
- [Desired State and Command Coalescing](#desired-state-and-command-coalescing)
- [Connection Events](#connection-events)
- [Firmware and Capabilities](#firmware-and-capabilities)
- [Timing and Diagnostics](#timing-and-diagnostics)
- [Offset Feedback](#offset-feedback)
- [Main-Form Device Controls](#main-form-device-controls)
- [Playback Speed Sync](#playback-speed-sync)
- [Local Buttplug/Intiface Output](#local-buttplugintiface-output)
- [Verification and Order](#verification-and-order)

All entries below are proposed future work. Implemented command serialization, version-aware setup, session checks, and awaited teardown are described in [architecture](ARCHITECTURE.md), not listed as wholly unfinished features.

Intermittent Handy disconnects have also been observed with other services. Wi-Fi instability is a working hypothesis, not a confirmed cause. Diagnostics should distinguish network, service, firmware, credential, and local lifecycle failures.

## Reconnect and HSSP Recovery

**Priority:** High. **Status:** Proposed.

Introduce explicit unavailable/recovering/ready states for transient HTTP failures, device disconnects, failed play, failed synctime, and takeover by another app. Keep observing mpv throughout recovery.

Online players can switch the device mode or replace the HSSP script while HandySync is connected; HandySync currently sets mode and script only on connect and does not notice. Record `mode_session_id` from the `PUT /mode2` response, then check `GET /mode` periodically (cheaply combined with the offset/stroke polling from [main-form device controls](../work/main-form-device-controls/PLAN.md)). A different mode or `mode_session_id` means another client took over: log it and enter recovery to restore HSSP mode and the script. A script replaced within HSSP mode may not change `mode_session_id`; verify whether `GET /hssp/state` (for example `stream_id`) exposes it. Decide whether takeover recovers automatically or waits for the user, to avoid fighting an online player that is still open.

Device reboots and server reconnects can also silently reset device state. Record `boot_session_id` and `socket_session_id` from `GET /sids` after setup and check them in the same periodic poll. A changed `boot_session_id` means the device rebooted; a changed `socket_session_id` within the same boot means it reconnected to the server. Either way, treat mode, HSSP setup, and the device-to-server clock as lost: enter recovery, re-estimate the client/server clock offset, re-read offset and stroke (applying the main-form connect policy), and log which ID changed. Verify whether HSTP offset and stroke survive a reboot or reconnect.

1. Suspend normal device synchronization.
2. Retry connectivity with exponential backoff, for example 1, 2, 4, 8, 15, and 30 seconds.
3. Recheck information, firmware, and capabilities.
4. Inspect HSSP state: reuse valid setup or recreate it with the hosted script URL, rehosting if necessary.
5. Resume at current mpv position only when playing; remain stopped while paused.
6. Return to periodic synchronization without resetting video.

Do not depend on synctime to restart stopped playback. Log whether setup was reused/recreated. Evaluate buffering and seek behavior for large scripts against current API documentation.

## Request Timeouts and Retries

**Priority:** High. **Status:** Proposed.

Use explicit budgets: connectivity/status and HSSP control 5-10 seconds, time samples 3-5 seconds, upload 30-60 seconds. Shutdown must still finish if the network is unavailable.

Retry transient resets, timeouts, DNS failures, HTTP 408/429/5xx. Respect Retry-After/rate-limit information. Do not blindly retry authentication failures, invalid requests, or malformed scripts.

## Desired State and Command Coalescing

**Priority:** Medium. **Status:** Proposed; serialization implemented.

Keep the single device-command gate. Add newest-desired-state handling to suppress duplicate play/pause, coalesce rapid seeks, supersede stale sync/offset requests, and reduce unnecessary traffic. Record command type/result. A semaphore alone does not provide coalescing or a recovery state machine; no separate controller host is required.

## Connection Events

**Priority:** Medium-high. **Status:** Proposed.

Evaluate SSE connection/error events with connectivity polling fallback. Verify event names against the current API reference. Reconnect the SSE reader with backoff and cancel it cleanly when the device connection ends.

Feed device warnings into the main-form device status area from [main-form device controls](../work/main-form-device-controls/DESIGN.md#device-status-read-only): `temp_high`/`temp_ok`, `slider_blocked`/`slider_unblocked`, and `battery_changed` (battery level only when capabilities report a battery). `stroke_changed` can replace stroke polling. Never log the SSE URL, which carries the API key.

## Firmware and Capabilities

**Priority:** Medium. **Status:** Proposed.

Before device setup, check `/connected`, `/info`, firmware status, and `/capabilities` where models vary. Report required updates/incompatibility rather than issuing unsupported commands. Vendor documentation requires firmware 4 or newer for API v3. Script loading stays independent of device availability.

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

**Priority:** Medium. **Status:** Proposed; debounce implemented.

Show syncing/applied/unavailable state for the API-backed offset, retain the selected value during recovery, reapply it when the device is ready, and offer reset-to-zero. Evaluate 10 ms steps and an advanced range beyond +/- 1000 ms if needed.

## Main-Form Device Controls

**Priority:** Medium. **Status:** Proposed.

Keep user-adjustable sync offset and minimum/maximum stroke controls on the main form; do not add a settings dialog. Use Handy REST API v3 only, consistent with the existing client; do not add an API v2 fallback.

- Replace the current local `OffsetMs` adjustment to the mpv timestamp with the API's device-side HSTP offset. Keep client/server clock-offset estimation separate; it is still required to calculate server time for synchronized playback.
- Read and set the device's slider stroke range through the v3 slider settings/stroke endpoints. Reflect the values accepted by the device in the controls.
- Read the current device values on connection and preserve the user's chosen values across app restarts/reconnects where needed. Debounce rapid slider changes and serialize API writes through the existing device-command gate.
- Depend on the Firmware and Capabilities check: require firmware compatible with API v3 (firmware 4 or newer), and show a clear unsupported-firmware state instead of attempting these controls on older devices. Do not silently fall back to v2.
- Verify offset sign/units and slider-range units against the current v3 API contract; test reconnect, rejected/normalized ranges, and API failures without losing the selected UI values.

## Playback Speed Sync

**Priority:** High. **Status:** Proposed; follow-up to the editor's [video playback speed](../work/video-playback-speed/PLAN.md) work.

HandySync always sends `playback_rate` 1.0 in `hssp/play`. When mpv runs at another speed (set in the editor, mpv, or another client), the device plays at normal speed and periodic synctime keeps pulling it back, producing jerky, drifting motion. Follow mpv's rate instead; HandySync observes speed only and gains no speed controls.

- Reuse Common's `MpvSession` speed observation and change events from the editor work; do not duplicate IPC logic or write speed to mpv.
- Pass the observed rate as `playback_rate` in `hssp/play`. On a speed change while playing, re-issue play at the current media position with the new rate through the device-command gate, debounced and latest-wins. While paused, only record the rate for the next play.
- Verify how `hssp/synctime` `current_time` behaves at non-1x rates and whether HSSP accepts rate changes without a new play (v3 lists `playbackrate` for HSP, not HSSP).
- Make the seek-detection heuristic rate-aware: expected position advances by elapsed time multiplied by the rate, or speeds other than 1x cause false seek resyncs.
- Device time stays in media time; the device-side HSTP offset is unaffected by rate.
- Fast rates can exceed the slider's speed limits (`/settings/slider` `x_max_speed`); decide whether to warn above a threshold or only log. Ignore invalid or nonpositive observations.
- Test rate changes while playing/paused, seeks at non-1x rates, drift over several minutes at 0.5x and 1.5x, reconnect with a non-1x rate already set, and that 1x behavior is unchanged.

## Local Buttplug/Intiface Output

**Priority:** Optional. **Status:** Proposed alternative.

Evaluate `HandySync -> Buttplug WebSocket -> Intiface Central -> local Bluetooth` as a separate backend behind a suitable output interface, not mixed into the HSSP client.

This avoids cloud hosting, application-key dependency, and server-time sync. It requires local funscript scheduling, latency/command-loss/back-pressure handling, and compatibility checks, and does not provide HSSP buffering. A separate ControllerHost is not a prerequisite.

## Verification and Order

Use the [shared test backlog](../BACKLOG.md) for fake-transport ordering, replacement, recovery, and idempotent teardown tests. Preserve current lifecycle guarantees when adding SSE/retries; background failures must not escape after close.

Suggested order: explicit recovery state and timeout policy; HSSP recovery; duplicate suppression; SSE/polling; timing diagnostics/offset feedback; optional local backend. This is not a dated delivery commitment.