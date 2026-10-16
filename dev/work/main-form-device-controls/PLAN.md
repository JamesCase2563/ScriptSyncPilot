# Main-Form Device Controls Plan

**Status:** Implementation complete; device verification and closeout remain.
**Selected:** 2026-10-11.
**Affected projects:** HandySync (API client, main form, settings). Common and FunscriptEditor are outside scope.

Source: [HandySync backlog](../../HandySync/BACKLOG.md#main-form-device-controls). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Let the user adjust the device-side sync offset and slider stroke range from the main form while a video and script are playing. Online players write these values to the device and leave them changed, so HandySync must show the device's actual values, apply the user's HandySync values reliably, and allow quick correction during playback without stopping the session.

## Scope

- Remove the old local offset controller: the `OffsetMs` shift of the mpv timestamp, its slider wiring, its debounced synctime resync, and the `OffsetMs` setting. The device-side HSTP offset (`GET/PUT /hstp/offset`) replaces it. Client/server clock-offset estimation stays unchanged and separate.
- Add minimum and maximum stroke controls backed by `GET/PUT /slider/stroke` (relative 0.0-1.0). Show the values the device accepted, including the absolute millimetre range from the response.
- Keep all controls on the main form; no settings dialog.
- Read device values on connection and adopt/persist them; do not write saved HandySync values during connect. User adjustments write to the device.
- Debounce rapid changes, keep only the newest pending value per control, and serialize writes through the existing `_deviceCommandGate`.
- Add a minimal firmware gate for these controls: `GET /info`, firmware major version 4 or newer and `fw_status` not update-required. Show an unsupported state instead of issuing writes. No API v2 fallback.
- Treat a device `error` object in an HTTP 200 response as a failed request for the new endpoints.
- Keep selected UI values through write failures, disconnects, and reconnects.
- Observe external changes: online players alter offset and stroke at unpredictable times, so poll the device values while connected (and immediately when the HandySync window is activated) and update the controls to reflect the device, without overriding an in-progress user edit.
- Show read-only device information next to the controls: physical slider settings (`GET /settings/slider`, read on connect) in a stroke tooltip; capabilities (`GET /capabilities`, read on connect) to disable stroke controls when the device has no slider; and motor temperature (`GET /slider/state`, polled) with a warning at the device's own `temp_high_trigger`.
- Prepare a device status area that can later show SSE warnings (`temp_high`, `slider_blocked`, `battery_changed`); the SSE subscription itself stays in Connection Events.

Out of scope: SSE event subscription (belongs to [Connection Events](../../HandySync/BACKLOG.md#connection-events); it may later replace stroke polling, but the v3 spec lists no offset-change event, so offset polling remains needed; blocked-slider and battery-level warnings have no REST equivalent and wait for it), retry/backoff policy and recovery state machine, extended offset range or reset-to-zero (belongs to [Offset Feedback](../../HandySync/BACKLOG.md#offset-feedback)), global hotkeys while mpv has focus, HAMP stroke, full capabilities validation for setup, and the local Buttplug backend.

## Open Decisions

- [x] **Connect policy.** On connect, read and log the device's current values, adopt them into the controls/settings, and do not write values back. User changes made in HandySync write to the device.
- [x] **Old local offset controller.** Remove it outright rather than keep it as a deprecated or hidden option; there is one offset, on the device. The legacy `OffsetMs` value is not migrated (its sign need not match HSTP); the property is deleted from `AppSettings`, so the stale key is ignored on load and dropped on the next save. Log once on first start if a nonzero legacy value was found.
- [x] **Offset display convention.** Display the raw HSTP value so it matches what online players show, with a tooltip stating that positive values make device operation run ahead of video and negative values delay it (verified 2026-10-14).
- [x] **Offset range and step.** Keep the existing -1000..+1000 ms slider with 10 ms small steps; extending it is Offset Feedback work.
- [x] **Stroke control type.** One custom range trackbar with two thumbs (min and max) in whole percent (0-100), plus a label showing percent and accepted millimetres. A thumb stops at the other thumb; it cannot pass it or push it. WinForms has no built-in range trackbar, so this is a small owner-drawn HandySync control.
- [x] **Slider look and orientation (GUI guidance, not a requirement).** Offset and Stroke Range share one owner-drawn slider style and both support horizontal/vertical orientation. Default: Offset horizontal, Stroke Range vertical with max at the top. See [design](DESIGN.md#layout-and-appearance-gui-guidance).
- [x] **Stroke apply timing.** Stroke writes may be issued during HSSP playback without stopping/restarting playback; online players can alter settings during operation.
- [x] **External changes: observe by polling.** Poll `GET /hstp/offset` and `GET /slider/stroke` while a supported device is connected, plus once on window activation. SSE is deferred: it covers stroke only, passes the API key as a URL query parameter, and needs its own reconnect lifecycle.
- [x] **External change policy.** Reflect the device value in the controls, mark *changed by another app*, and log old/new values; do not reapply automatically. The connect policy also adopts the device's current values without writing.
- [x] **Poll interval.** Poll offset and stroke every 5 s (two GETs, about 24 requests/min, alongside the existing 3 s synctime), skipping when the device-command gate is busy. Poll motor temperature every third tick (15 s), adding about 4 requests/min. Adjust the interval later if it causes problems.
- [x] **Temperature warning.** Show and log only; no automatic stop. Color-code the temperature display (normal, warm, hot) relative to the device's `temp_high_trigger`, with hysteresis on clearing; log transitions only. See [design](DESIGN.md#device-status-read-only).

## Milestones

### 1. Design and Contract Verification

- [x] Record API contract from the v3 OpenAPI spec in DESIGN.md.
- [x] Resolve the open decisions above.
- [ ] Verify device-specific behavior not guaranteed by the REST contract: persistence across HandySync reconnect/device power cycle and HandySync UI response to live external changes. Offset sign and immediate effect during running HSSP playback are confirmed; valid stroke writes and external stroke visibility via GET were observed on 2026-10-14. Treat API-defined units/error shapes as authoritative; offset bounds and invalid stroke normalization are unspecified and will not be manually probed.
- [x] Record verified results in VERIFICATION.md and adjust DESIGN.md.

### 2. Implementation

- [x] Extend `HandyApiClient`: device info, get/set HSTP offset, get/set slider stroke, slider settings, capabilities, slider state; parse device `error` objects for these calls.
- [x] Add firmware check during connect and an unsupported-firmware UI state for the controls.
- [x] Remove the old local offset controller: offset term in `DeviceTimeMs`, `OnOffsetChangedAsync` synctime resync, `_offsetChangeCts`, and `AppSettings.OffsetMs`. Rewire the offset slider to the device-side write path.
- [x] Add a shared owner-drawn slider (one or two thumbs, horizontal/vertical orientation; mouse drag, keyboard, focus cues). Use it for Offset (one thumb, horizontal) and Stroke Range (two thumbs, vertical) with value/state labels.
- [x] Implement debounced, latest-wins, gate-serialized writes with per-control applied/pending/failed/unsupported state.
- [x] Adopt the device's current values after mode/clock setup and before playback can start; do not issue connect-time offset/stroke PUTs.
- [x] Persist chosen values in `AppSettings`.
- [x] Cancel pending writes on disconnect/close without delaying teardown; never write after disconnect begins.
- [x] Add device-value polling and window-activation refresh; reflect external changes without overriding pending or in-flight user edits; stop polling on disconnect/close.
- [x] Add the device status area: stroke tooltip from slider settings, no-slider handling from capabilities, temperature display and warning from polled slider state.
- [x] Add focused automated tests (see VERIFICATION.md) in the existing Windows-targeted HandySync test suite following repository conventions.

### 3. Verification and Completion

- [x] Run automated checks and build the solution; record exact commands and results.
- [ ] Complete manual device checks during live playback, including values left by an online player.
- [ ] Update [HandySync architecture](../../HandySync/ARCHITECTURE.md) and the backlog (remove this item; adjust Firmware and Capabilities and Offset Feedback to what remains).
- [ ] Record limitations and the PR reference.
- [ ] Archive under `dev/archive/2026-10-main-form-device-controls/` (or the completion month) and update links.

## Outcome

Implementation complete; solution tests and build pass. Manual HandySync UI/device checks, remaining contract verification, architecture/backlog updates, and archival are still pending.
