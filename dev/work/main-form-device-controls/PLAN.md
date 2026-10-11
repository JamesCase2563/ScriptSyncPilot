# Main-Form Device Controls Plan

**Status:** Planning; open decisions below must be resolved before implementation.
**Selected:** 2026-10-11.
**Affected projects:** HandySync (API client, main form, settings). Common and FunscriptEditor are outside scope.

Source: [HandySync backlog](../../HandySync/BACKLOG.md#main-form-device-controls). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Let the user adjust the device-side sync offset and slider stroke range from the main form while a video and script are playing. Online players write these values to the device and leave them changed, so HandySync must show the device's actual values, apply the user's HandySync values reliably, and allow quick correction during playback without stopping the session.

## Scope

- Remove the old local offset controller: the `OffsetMs` shift of the mpv timestamp, its slider wiring, its debounced synctime resync, and the `OffsetMs` setting. The device-side HSTP offset (`GET/PUT /hstp/offset`) replaces it. Client/server clock-offset estimation stays unchanged and separate.
- Add minimum and maximum stroke controls backed by `GET/PUT /slider/stroke` (relative 0.0-1.0). Show the values the device accepted, including the absolute millimetre range from the response.
- Keep all controls on the main form; no settings dialog.
- Read device values on connection; apply/persist the user's chosen values according to the agreed connect policy.
- Debounce rapid changes, keep only the newest pending value per control, and serialize writes through the existing `_deviceCommandGate`.
- Add a minimal firmware gate for these controls: `GET /info`, firmware major version 4 or newer and `fw_status` not update-required. Show an unsupported state instead of issuing writes. No API v2 fallback.
- Treat a device `error` object in an HTTP 200 response as a failed request for the new endpoints.
- Keep selected UI values through write failures, disconnects, and reconnects.
- Observe external changes: online players alter offset and stroke at unpredictable times, so poll the device values while connected (and immediately when the HandySync window is activated) and update the controls to reflect the device, without overriding an in-progress user edit.

Out of scope: SSE event subscription (belongs to [Connection Events](../../HandySync/BACKLOG.md#connection-events); it may later replace stroke polling, but the v3 spec lists no offset-change event, so offset polling remains needed), retry/backoff policy and recovery state machine, extended offset range or reset-to-zero (belongs to [Offset Feedback](../../HandySync/BACKLOG.md#offset-feedback)), global hotkeys while mpv has focus, HAMP stroke, full capabilities validation for setup, and the local Buttplug backend.

## Open Decisions

- [ ] **Connect policy.** Recommended: on connect, read and log the device's current values, then reapply the saved HandySync values so that values left behind by online players do not affect HandySync. Alternative: adopt the device's values into the controls and write only on user change.
- [x] **Old local offset controller.** Remove it outright rather than keep it as a deprecated or hidden option; there is one offset, on the device. The legacy `OffsetMs` value is not migrated (its sign need not match HSTP); the property is deleted from `AppSettings`, so the stale key is ignored on load and dropped on the next save. Log once on first start if a nonzero legacy value was found.
- [ ] **Offset display convention.** Recommended: display the raw HSTP value so it matches what online players show, with a tooltip describing the verified direction (for example, "positive = device moves later").
- [ ] **Offset range and step.** Recommended: keep the existing -1000..+1000 ms slider with 10 ms small steps; extending it is Offset Feedback work.
- [x] **Stroke control type.** One custom range trackbar with two thumbs (min and max) in whole percent (0-100), plus a label showing percent and accepted millimetres. A thumb stops at the other thumb; it cannot pass it or push it. WinForms has no built-in range trackbar, so this is a small owner-drawn HandySync control.
- [x] **Slider look and orientation (GUI guidance, not a requirement).** Offset and Stroke Range share one owner-drawn slider style and both support horizontal/vertical orientation. Default: Offset horizontal, Stroke Range vertical with max at the top. See [design](DESIGN.md#layout-and-appearance-gui-guidance).
- [ ] **Stroke apply timing.** Confirm that stroke writes may be issued during HSSP playback without stopping/restarting playback (expected; verify on device).
- [x] **External changes: observe by polling.** Poll `GET /hstp/offset` and `GET /slider/stroke` while a supported device is connected, plus once on window activation. SSE is deferred: it covers stroke only, passes the API key as a URL query parameter, and needs its own reconnect lifecycle.
- [ ] **External change policy.** Recommended: reflect the device value in the controls, mark *changed by another app*, and log old/new values; do not reapply automatically. Alternative: reassert the HandySync value automatically (risks fighting an online player that is still open). Align the connect policy with this choice.
- [ ] **Poll interval.** Recommended: 5 s (two GETs, about 24 requests/min, alongside the existing 3 s synctime), skipped when the device-command gate is busy.

## Milestones

### 1. Design and Contract Verification

- [x] Record API contract from the v3 OpenAPI spec in DESIGN.md.
- [ ] Resolve the open decisions above.
- [ ] Verify on the device: HSTP offset sign and units, whether a running HSSP playback picks up an offset change immediately, offset persistence across device reconnect, how quickly an online player's changes become visible via GET, stroke normalization/rejection behavior, and 200-with-`error` responses.
- [ ] Record verified results in VERIFICATION.md and adjust DESIGN.md.

### 2. Implementation

- [ ] Extend `HandyApiClient`: device info, get/set HSTP offset, get/set slider stroke; parse device `error` objects for these calls.
- [ ] Add firmware check during connect and an unsupported-firmware UI state for the controls.
- [ ] Remove the old local offset controller: offset term in `DeviceTimeMs`, `OnOffsetChangedAsync` synctime resync, `_offsetChangeCts`, and `AppSettings.OffsetMs`. Rewire the offset slider to the device-side write path.
- [ ] Add a shared owner-drawn slider (one or two thumbs, horizontal/vertical orientation; mouse drag, keyboard, focus cues). Use it for Offset (one thumb, horizontal) and Stroke Range (two thumbs, vertical) with value/state labels.
- [ ] Implement debounced, latest-wins, gate-serialized writes with per-control applied/pending/failed/unsupported state.
- [ ] Apply the agreed connect policy after mode/clock setup and before playback can start.
- [ ] Persist chosen values in `AppSettings`.
- [ ] Cancel pending writes on disconnect/close without delaying teardown; never write after disconnect begins.
- [ ] Add device-value polling and window-activation refresh; reflect external changes without overriding pending or in-flight user edits; stop polling on disconnect/close.
- [ ] Add focused automated tests (see VERIFICATION.md) in a Windows-targeted HandySync MSTest project following existing conventions.

### 3. Verification and Completion

- [ ] Run automated checks and build the solution; record exact commands and results.
- [ ] Complete manual device checks during live playback, including values left by an online player.
- [ ] Update [HandySync architecture](../../HandySync/ARCHITECTURE.md) and the backlog (remove this item; adjust Firmware and Capabilities and Offset Feedback to what remains).
- [ ] Record limitations and the PR reference.
- [ ] Archive under `dev/archive/2026-10-main-form-device-controls/` (or the completion month) and update links.

## Outcome

Not started.
