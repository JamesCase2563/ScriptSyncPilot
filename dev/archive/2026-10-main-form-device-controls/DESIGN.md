# Main-Form Device Controls Design

**Status:** Complete and archived; implementation, verification, and closeout finished. Selected decisions and verified device observations are recorded in the [plan](PLAN.md) and [verification](VERIFICATION.md).

## Motivation

Online players write the HSTP offset and slider stroke range to the device and do not restore them. When the user then plays local video through HandySync, the device may be offset or limited to a narrow stroke. The user needs to see and correct both values from the main form during playback, without stopping the session or opening a dialog.

## API Contract (REST v3)

Taken from the v3 OpenAPI spec (`https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml`, retrieved 2026-10-11). Where the spec does not define a behavior, this design records it as unspecified rather than requiring speculative device testing.

| Endpoint | Request | Response `result` | Notes |
| --- | --- | --- | --- |
| `GET /info` | - | `fw_status` (0 up-to-date, 1 update-available, 2 update-required), `fw_version` string, `hw_model_name`, `session_id` | v3 requires firmware 4 or newer. |
| `GET /hstp/offset` | - | `{ offset }` integer ms | "User adjusted device time offset". Offset units are taken from the v3 contract. |
| `PUT /hstp/offset` | `{ offset }` number ms | `"ok"` | Verified 2026-10-14: positive values make device operation run ahead of video; negative values delay it. A change takes effect immediately during running HSSP playback. Offset bounds are not specified by the contract and are treated as device-defined. |
| `GET /slider/stroke` | - | `min`, `max` (0.0-1.0), `min_absolute`, `max_absolute` (mm) | |
| `PUT /slider/stroke` | `{ min, max }` 0.0-1.0 | same as GET | Device may normalize values; use the response, not the request. Behavior for invalid ranges is unspecified; the UI keeps min <= max and surfaces request errors. |
| `GET /capabilities` | - | `slider` count, `battery`, ... | Disable stroke controls when `slider` is 0. |
| `GET /settings/slider` | - | `x_stroke_min`/`x_stroke_max` (mm), `x_min_speed`/`x_max_speed` (mm/s), `temp_high_trigger`, `temp_hysteresis` (°C), ... | Read-only; changed only in the Handy onboarding app. |
| `GET /slider/state` | - | `motor_temp` (°C), `position`, `speed_absolute`, ... | |

Device errors may be returned in a 200 response as `{ "error": { code, name, message, connected } }` (for example 1001 DeviceNotConnected, 1002 DeviceTimeout). `HandyApiClient` treats a present `error` object as failure and raises `HandyApiException` with code and name; the message does not include response details, connection keys, or application keys.

`/hamp/stroke` is not used: it is relative to the HAMP stroke region and does not affect HSSP. `/settings/slider` is read for display only.

## Offset: Remove Local Controller, Use Device-Side

Before this change, `DeviceTimeMs` added `_offsetSlider.Value` to the mpv position, and offset changes triggered a debounced `hssp/synctime` (`OnOffsetChangedAsync`, `_offsetChangeCts`). That local controller is removed, not deprecated or kept behind an option; there is exactly one offset, stored on the device.

- `DeviceTimeMs(pos)` returns the mpv position only. `EstimatedServerTimeMs` and `_clockOffsetMs` are unchanged.
- `OnOffsetChangedAsync` and `_offsetChangeCts` are deleted, including their cancellation in disconnect and stop. The slider control is kept but rewired to the device write path, which writes `PUT /hstp/offset`. Play/seek/synctime calls no longer depend on the slider.
- A successful offset write takes effect immediately during running HSSP playback (verified 2026-10-14); no follow-up `hssp/synctime` or `hssp/play` is required.
- The slider shows the device's raw HSTP value (per the agreed display convention) so values match those shown by online players. The tooltip states the verified direction.

## Stroke Range

- One owner-drawn `DeviceSliderControl` in HandySync with two thumbs (min and max) on a single track, in whole percent 0-100, mapped to 0.00-1.00 for the API. The selected range is highlighted between the thumbs so the stroke zone is visible at a glance.
- A dragged thumb is clamped at the other thumb (min <= max); it cannot pass or push the other thumb. The thumbs may coincide; device behavior for equal or invalid ranges remains unverified.
- Mouse: press picks the nearest thumb (the lower thumb wins an exact-distance tie); dragging moves it to the pointer value. Clicking the track moves the nearest thumb directly to the clicked value. Dragging the highlighted range between thumbs is not supported in this version.
- Keyboard: Tab focuses the control; Left/Right or Up/Down move the active thumb by 1 %, Page Up/Down by 10 %, Home/End to its limit; Space switches the active thumb. Draw a focus cue on the active thumb.
- The control raises one `ValuesChanged` event per user change and exposes `SetValues` to set both values without raising it, used when applying device-accepted values.
- A label shows the accepted range, for example `20-85 % (24.1-89.6 mm)`, from the PUT/GET response. If the device normalizes the values, the thumbs are updated to the accepted values without triggering another write.
- Keep clamping and hit-testing within the control; tests exercise value ordering and keyboard behavior without relying on rendered pixels.

## Layout and Appearance (GUI Guidance)

Guidance, not a requirement; adjust during implementation if the form works better otherwise.

- Both sliders support horizontal and vertical orientation. Default: Offset horizontal (time axis, earlier/later left-right), Stroke Range vertical (matches the physical slider; max at the top, min at the bottom, as in the API's 0 = bottom, 1 = top).
- Offset and Stroke Range should look alike. The standard WinForms `TrackBar` looks different from an owner-drawn control and themes poorly when vertical, so draw both with one shared owner-drawn slider base: same track, thumb shape, highlight color, tick style, focus cue, and disabled/unsupported rendering. Offset uses one thumb (highlight from 0 to the thumb), Stroke Range uses two thumbs (highlight between them).
- Orientation is a property of the control; hit-testing, keyboard, and clamping logic operate on a value axis independent of orientation. In vertical orientation Up/Page Up increase the value, matching screen direction.
- Place the vertical Stroke Range beside the top settings rows (spanning their height) rather than in its own row, so the form does not grow taller. Value and state labels sit next to each slider in the same style.
- Scale track thickness, thumb size, and hit areas with DPI; keep thumbs large enough to grab during playback.

## Write Path

One small coordinator per value kind (offset, stroke) in MainForm, owned by the UI thread:

1. UI change stores the desired value, marks the control *pending*, and restarts a debounce delay (about 150-250 ms).
2. When the delay expires, the coordinator acquires `_deviceCommandGate`, rechecks that the same `HandyApiClient` is current and the device is not disconnecting, and sends the newest desired value only.
3. On success, update the control from the accepted value (stroke) or the requested value (offset), mark *applied*, and log once.
4. On failure, keep the desired UI value, mark *not applied*, and log the classified error. Do not disconnect the device; the next change or reconnect retries. Retries/backoff are outside this work.
5. If the desired value changed while the request was in flight, run once more with the newest value; intermediate values are dropped.

The debounce token is linked to `_lifetimeCts` and cancelled by disconnect, session stop where relevant, and close. Writes are independent of whether an mpv session is attached: they need only a connected, supported device. Teardown keeps the existing order: cancel pending writes, drain the gate, final stop, dispose.

## Connection Sequence

After `IsConnectedAsync` succeeds:

1. `GET /info`; if `fw_version` major < 4 or `fw_status` is 2, set the controls to *unsupported firmware* (with version text) and skip all offset/stroke calls. Whether the rest of the HSSP connection continues is governed by the separate Firmware and Capabilities item; this work only gates the new controls.
2. Set HSSP mode and estimate clock offset (unchanged).
3. Read `GET /capabilities` and `GET /settings/slider` once; read `GET /hstp/offset` and `GET /slider/stroke`; log the device's current values. If `slider` is 0, show *no slider* and skip stroke calls.
4. Adopt the values returned by the device into the controls and settings. Do not write offset or stroke values during connect; only a user adjustment in HandySync issues a PUT.
5. Install the script and mark HSSP ready (unchanged).

Adopting device values before HSSP ready means the controls never show stale saved values after connect. Failures to read offset or stroke mark the related controls *not applied* but do not fail the connection. Polling (below) starts after step 4.

## Observing External Changes

Online players change offset and stroke at unpredictable times, including while HandySync is connected. HandySync polls the device so the controls show the real values.

**Mechanism.** A WinForms timer (5 s, per the agreed interval) runs while a supported device is connected, independent of the mpv session. Each tick reads `GET /hstp/offset` and `GET /slider/stroke`. The form's `Activated` event triggers one immediate read, so returning from a browser player refreshes the values at once. A tick that finds `_deviceCommandGate` busy is skipped (`WaitAsync(0)`) so polling never delays play, seek, synctime, or setup commands. Only one poll runs at a time.

**Why not SSE now.** The v3 SSE stream has `stroke_changed` but no offset event, so offset needs polling anyway. SSE also requires the API key in the URL query string (which must never be logged) and a reconnecting reader with clean cancellation; that lifecycle belongs to the Connection Events item. When SSE is implemented, `stroke_changed` can replace stroke polling and trigger an immediate offset read.

**Avoiding conflicts with user edits.** Each control keeps an edit version incremented on every user change. A poll records the versions before reading and discards a result for any control whose version changed, whose write is pending or in flight, or which is being dragged (mouse captured) or key-adjusted. Writes and polls share the gate, so a poll never interleaves with a write. Programmatic updates from polls use the no-event setter and never trigger a write.

**Applying a detected change.** When the device value differs from the last known device value:

- Reflect policy (recommended): update the control to the device value, set state *changed by another app*, log old and new values once, and persist the new value as the user's current value. The next user change clears the state.
- Reassert policy (alternative): log the change and enqueue a write of the HandySync value through the normal write path. Limit reassertion (for example once per external change) to avoid fighting an open online player.

**Failures.** A failed poll is logged at most once until the next success and does not change control values, disconnect, or count as a write failure. After several consecutive failures the state shows *device values unknown*. Polling stops before disconnect teardown drains the gate and is cancelled with `_lifetimeCts` on close.

## Device Status (Read-Only)

A compact status area beside the controls shows device information that HandySync reads but never writes.

- **Physical slider settings** (`/settings/slider`, read once per connection): the stroke slider tooltip shows the physical stroke in mm and speed limits, for example `Physical stroke 0-110 mm, speed 10-400 mm/s (set in Handy onboarding app)`. Its temperature thresholds drive the warning below. A failed read only omits the tooltip details.
- **Capabilities** (`/capabilities`, read once per connection): with no slider, the stroke control shows *no slider* and is disabled. `battery` presence is recorded for the future battery indicator; level is not available over REST.
- **Motor temperature** (`/slider/state`, polled every third tick inside the same gate-skipping poll): shown as `Motor 41 °C` with a color band relative to the device's `temp_high_trigger`:

  | Band | Range | Indication |
  | --- | --- | --- |
  | Normal | below `trigger - 10 °C` | default text color (or green) |
  | Warm | `trigger - 10 °C` up to `trigger` | amber text |
  | Hot | at or above `trigger` | red background with "High" text |

  Bands clear downwards only after the temperature falls `temp_hysteresis` below the band's lower edge, so the color does not flicker. Text accompanies color ("Warm", "High") so the state does not rely on color alone. Without slider settings, use a conservative default trigger (for example 75 °C) and note it in the tooltip. Log band transitions, not every reading. HandySync only shows and logs; it never stops playback because of temperature. The 10 °C warm-band width and 75 °C fallback trigger are provisional; adjust them after trial use.
- **Future SSE warnings.** `temp_high`/`temp_ok`, `slider_blocked`/`slider_unblocked`, and `battery_changed` have no REST equivalent (except temperature, covered above). Keep the status area a small model with named indicators (temperature, slider blocked, battery) so Connection Events can feed them later without UI redesign. They are not shown in this work.

Status is cleared on disconnect and is not persisted.

## UI State

Each control group shows one short state next to its value: *not connected* (editable; value saved and applied on connect), *pending*, *applied*, *not applied* (with error in the log), *changed by another app*, *device values unknown*, or *unsupported firmware*. Controls remain enabled when disconnected so values can be prepared before connecting; they are disabled only for unsupported firmware.

## Persistence

`AppSettings` gains `DeviceOffsetMs`, `StrokeMinPercent`, and `StrokeMaxPercent`, saved with the existing settings on close. `OffsetMs` is deleted from `AppSettings` and not migrated, because its local timestamp shift need not match HSTP sign semantics. `System.Text.Json` ignores the stale key on load and the next save drops it. If a nonzero legacy value is present in the file, log once that it was discarded (read it via a raw JSON check, not a retained property). Invalid stored stroke values (out of range or min > max - gap) fall back to 0-100.

## Testability

Inject an `HttpMessageHandler` (internal constructor) into `HandyApiClient` so request bodies, paths, and response/error parsing can be tested without a device. Keep pure helpers testable: firmware version parsing, percent/relative conversion, stroke validation. The debounce/latest-wins coordinator should accept a delay and a write delegate so ordering can be tested deterministically. Keep the poll-result acceptance rule (edit versions, pending/in-flight/dragging) as a pure decision so conflict cases can be tested without timers. Keep temperature band classification with hysteresis as a pure function.

## Limitations and Risks

- External changes are detected within one poll interval (or on window activation), not instantly. Instant stroke notification via SSE is deferred to Connection Events.
- If an online player is still open and keeps writing, the reflect policy will follow it; HandySync does not lock values.
- The spec does not define HSTP offset sign or bounds. Sign direction is recorded from device verification; bounds remain unverified.
- Rate limits (`X-RateLimit-*`) are not handled beyond debounce and the poll interval; rapid dragging must not produce more than a few writes per second.
