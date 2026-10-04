# HandySync Backlog

All entries below are proposed future work. Implemented command serialization, version-aware setup, session checks, and awaited teardown are described in [architecture](ARCHITECTURE.md), not listed as wholly unfinished features.

Intermittent Handy disconnects have also been observed with other services. Wi-Fi instability is a working hypothesis, not a confirmed cause. Diagnostics should distinguish network, service, firmware, credential, and local lifecycle failures.

## Reconnect and HSSP Recovery

**Priority:** High. **Status:** Proposed.

Introduce explicit unavailable/recovering/ready states for transient HTTP failures, device disconnects, failed play, and failed synctime. Keep observing mpv throughout recovery.

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

## Offset Feedback

**Priority:** Medium. **Status:** Proposed; debounce implemented.

Show syncing/applied/unavailable state, retain the selected offset during recovery, apply it when recovery completes, and offer reset-to-zero. Evaluate 10 ms steps and an advanced range beyond +/- 1000 ms if needed.

## Local Buttplug/Intiface Output

**Priority:** Optional. **Status:** Proposed alternative.

Evaluate `HandySync -> Buttplug WebSocket -> Intiface Central -> local Bluetooth` as a separate backend behind a suitable output interface, not mixed into the HSSP client.

This avoids cloud hosting, application-key dependency, and server-time sync. It requires local funscript scheduling, latency/command-loss/back-pressure handling, and compatibility checks, and does not provide HSSP buffering. A separate ControllerHost is not a prerequisite.

## Verification and Order

Use the [shared test backlog](../BACKLOG.md) for fake-transport ordering, replacement, recovery, and idempotent teardown tests. Preserve current lifecycle guarantees when adding SSE/retries; background failures must not escape after close.

Suggested order: explicit recovery state and timeout policy; HSSP recovery; duplicate suppression; SSE/polling; timing diagnostics/offset feedback; optional local backend. This is not a dated delivery commitment.