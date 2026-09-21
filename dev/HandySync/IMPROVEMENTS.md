# HandySync Improvement Notes

The current end-to-end path is working: the application authenticates, uploads the funscript, configures HSSP, attaches to VR-Flat-Player's mpv IPC pipe, and follows play/pause and seek events.

The Handy occasionally loses its connection, also observed with other services. Intermittent Wi-Fi is the leading suspicion. The improvements below are ordered by expected value.

## 1. Add explicit reconnect and recovery

**Priority: high**

At present, a failed Handy request is logged, but the playback session does not have a complete recovery state machine. Add recovery for:

- HTTP timeouts and transient network failures.
- Handy responses reporting disconnected or timed out.
- A device disconnect/reconnect detected through the Handy SSE endpoint or status polling.
- A failed `hssp/synctime` request during playback.
- A failed `hssp/play` after mpv resumes.

Suggested recovery flow:

1. Mark the device unavailable and stop sending normal sync requests.
2. Keep observing mpv position, pause state, and seeks.
3. Retry `/connected` with exponential backoff, for example 1, 2, 4, 8, 15, and 30 seconds.
4. Re-check `/info` and firmware status after reconnection.
5. Re-establish HSSP mode and setup.
6. Start HSSP from the current mpv position if mpv is playing.
7. Remain stopped if mpv is paused.
8. Return to normal periodic synchronization.

The current media position should remain the source of truth. Recovery must not restart the video or reset the mpv playhead.

## 2. Serialize Handy commands

**Priority: high**

The UI and mpv callbacks can produce overlapping commands. This is especially likely when the user pauses, seeks, resumes, and moves the offset slider close together.

Introduce one command queue for all Handy API operations. It should:

- Preserve command order.
- Cancel or supersede stale sync requests.
- Prevent `play` from overtaking `stop`.
- Prevent an old offset update from being applied after a newer one.
- Avoid sending requests while a reconnect/setup sequence is in progress.
- Record the command type and result in the log.

This follows the useful part of ScriptPlayer's `BlockingTaskQueue` design while retaining the current async/await implementation style.

## 3. Add request timeouts and retry policy

**Priority: high**

Use explicit per-request timeouts rather than relying only on the default `HttpClient` timeout. Suggested policy:

- Connectivity and status requests: 5-10 seconds.
- HSSP control requests: 5-10 seconds.
- Server-time samples: 3-5 seconds.
- Script upload: 30-60 seconds depending on script size.

Retry only transient failures:

- Connection reset.
- Timeout.
- DNS or temporary network failure.
- HTTP 408, 429, and 5xx responses.

Do not blindly retry authentication failures, invalid requests, or malformed scripts. Respect `Retry-After` and Handy rate-limit headers when present.

## 4. Use Handy SSE for connection status

**Priority: medium-high**

The Handy API v3 provides an `/sse` endpoint with events including:

- `device_connected`
- `device_disconnected`
- `device_status`
- `device_error`
- `mode_changed`
- `hsp_state_changed`
- `stream_end_reached`

Subscribe to connection and error events while a session is active. SSE gives faster and clearer state changes than waiting for the next periodic request. Keep `/connected` polling as a fallback because the SSE stream itself can also be interrupted.

The SSE reader should reconnect with backoff and must be cancelled cleanly when HandySync stops.

## 5. Improve HSSP recovery after disconnect

**Priority: medium-high**

HSSP v3 can stream script points through the Handy service as playback progresses. After a network interruption, the device may have stopped, be starving, or have lost the active HSSP session.

On recovery:

- Call `hssp/state` before assuming the session is usable.
- If the session is gone, repeat `hssp/setup` with the existing hosted script URL.
- If the session exists, use `hssp/play` with the current video position.
- Do not depend only on `synctime` to recover a stopped device.
- Log whether recovery reused or recreated the HSSP session.

For large scripts, handle the possibility that seeking outside the currently buffered region requires additional network time.

## 6. Refresh clock offset periodically

**Priority: medium**

The current client estimates the client/server clock offset when a session starts. Recalculate it periodically, for example every 5-10 minutes, and after a reconnect or major network change.

Use the existing round-trip-time sampling algorithm, but consider selecting the lowest-latency samples instead of averaging every sample equally. Wi-Fi jitter can bias a simple average.

Log:

- New estimated offset.
- Minimum, maximum, and median round-trip time.
- Difference from the previous estimate.

A clock-offset change should not itself restart playback; it should be applied to the next synchronization or recovery command.

## 7. Add network and device diagnostics

**Priority: medium**

Add a diagnostics area or command that records:

- Handy HTTP status and response error name.
- Request duration.
- Round-trip-time samples.
- Last successful Handy request.
- Last successful HSSP state.
- Reconnect count.
- Time spent disconnected.
- Firmware version and firmware status.
- Whether the device is connected through Wi-Fi or Bluetooth according to `/info`/device status.

This will distinguish Wi-Fi loss from Handy service issues, expired credentials, rate limiting, device firmware problems, and local application bugs.

Avoid logging application keys or device connection keys.

## 8. Make the log useful during recovery

**Priority: medium**

The file log should include structured state transitions rather than only messages such as `Playback resumed`.

Useful entries include:

```text
Handy state: Connected -> Disconnected
Handy request: hssp/synctime, duration=182 ms, result=timeout
Recovery: retry 3 in 8 s
Recovery: connected after 12.4 s
Recovery: HSSP setup restored at video=817.6 s
```

Avoid logging every successful periodic sync at normal verbosity. Keep a debug level for high-volume request tracing.

## 9. Prevent duplicate playback commands

**Priority: medium**

The log has shown duplicate play/pause messages during rapid mpv state changes. Add a playback state gate:

- Track the last requested state: playing, paused, stopped, or recovering.
- Ignore repeated events that do not change the desired state.
- Coalesce play/pause events while a seek is being processed.
- Let the command queue apply only the newest desired position/state.

This reduces API traffic and avoids race conditions during mpv startup, seeks, and reconnects.

## 10. Improve offset adjustment behavior

**Priority: medium**

The offset slider is now debounced, but a user-visible offset change still depends on the next successful HSSP synchronization.

Potential improvements:

- Show `Syncing`, `Applied`, or `Unavailable` beside the offset control.
- Keep the selected offset while disconnected.
- Apply it automatically when recovery completes.
- Provide a reset-to-zero action.
- Consider a coarse range such as +/- 1000 ms with 10 ms steps and an advanced range for unusual setups.

## 11. Check firmware and capabilities at startup

**Priority: medium**

Before loading a script:

1. Call `/connected`.
2. Call `/info`.
3. Reject or warn for `UPDATE_REQUIRED` firmware.
4. Call `/capabilities` if the device model may vary.
5. Continue only after reporting the device state clearly.

The Handy documentation states that most API v3 control requires firmware 4 or newer.

## 12. Consider a local Buttplug/Intiface backend

**Priority: optional alternative**

If the user wants to avoid cloud dependency or Wi-Fi reliability problems, add a second output backend using Buttplug and Intiface Central:

```text
HandySync -> Buttplug WebSocket -> Intiface Central -> local Bluetooth
```

Advantages:

- No Handy application key.
- No Handy cloud script hosting.
- No server-time synchronization.
- Local Bluetooth path may avoid a weak Wi-Fi connection.

Tradeoffs:

- The application must schedule funscript actions locally.
- Bluetooth latency and command loss need handling.
- It does not provide HSSP's device/server buffering.
- Device support depends on the installed Buttplug/Intiface version.

This should be a separate output implementation behind a common device-output interface, not mixed into the HSSP client.

## 13. Make startup and shutdown idempotent

**Priority: medium**

Repeated starts and stops should safely handle:

- An existing Handy API client.
- An existing mpv pipe attachment.
- A pending HTTP request.
- A pending slider sync.
- An active SSE connection.
- A previous HSSP session on the device.

Shutdown should attempt `hssp/stop`, but must still complete if the network is already unavailable. No background task should display an unhandled exception after the form closes.

## Suggested implementation order

1. Central Handy command queue and explicit session state.
2. Request timeout/error classification and exponential reconnect.
3. HSSP state inspection and recovery from the current mpv position.
4. Duplicate play/pause suppression.
5. SSE connection events with polling fallback.
6. Periodic clock-offset refresh and network diagnostics.
7. Optional Buttplug/Intiface output backend.

The first three items address the most likely real-world failure: a temporary Wi-Fi drop while the video continues playing. The user should see the Handy reconnect and resume from the current video position without restarting the video or manually loading the script again.
