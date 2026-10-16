# Main-Form Device Controls Verification

**Status:** Device contract and manual HandySync interaction checks complete. API-unspecified bounds/invalid-range behavior remain intentionally unprobed.

See [plan](PLAN.md) and [design](DESIGN.md).

## Device Contract Checks (before implementation is finalized)

Record firmware version, device model, and date for each result.

- [x] HSTP offset sign: observed 2026-10-14; negative offset delays device operation relative to video, and positive offset makes device operation run ahead of video.
- [x] HSTP offset units are milliseconds per the Handy REST v3 contract. The contract does not specify bounds; offset-limit behavior is treated as device-defined and will not be manually probed.
- [x] A change to HSTP offset during running HSSP playback takes effect immediately (observed 2026-10-14); no synctime or play was needed.
- [x] HSTP offset and stroke persist on the device across HandySync disconnect/reconnect and device power cycle (confirmed 2026-10-16).
- [x] The REST contract exposes current offset and stroke through `GET`; online-player offset and stroke changes were observed through GET and HandySync's live controls during manual testing.
- [x] With HandySync connected, a change made in an online player becomes visible in HandySync within one 5-second poll interval; manual interaction check passed. Separately, the standalone probe saw external stroke changes through GET within about 5-6 s on 2026-10-14.
- [x] `PUT /slider/stroke` with valid values returns the accepted values and mm range; 25-75% returned 30.165-80.495 mm, and 0-100% returned 5-105.66 mm (2026-10-14).
- [x] Invalid-range probing is omitted: the REST contract does not specify rejection/normalization for min >= max or out-of-range stroke values. The UI keeps min <= max, uses device-accepted values, and reports write errors as not applied.
- [x] Stroke changes can be issued during operation; user confirmed the device responded to input from the probe and online player (2026-10-14).
- [x] The Handy REST v3 contract allows HTTP 200 responses with an `error` object; this behavior is accepted from the spec and the client handling is covered by an automated test. No device fault was injected to observe the transport status.
- [x] Recorded device metadata on 2026-10-14: H01, firmware 4.3.0 (`fw_status` 0), one slider; physical range 0-110.66 mm, speed 32-400 mm/s, temperature trigger 80 °C and hysteresis 10 °C. `motor_temp` was 26.7 °C idle and about 29.3 °C while active.

## Automated Checks

- [x] `HandyApiClient` sends correct path, method, auth headers, and JSON body for info, get/set offset, get/set stroke, capabilities, slider settings/state, and mode.
- [x] Core responses parse correctly, including stroke `min_absolute`/`max_absolute` and info `fw_status`/`fw_version`.
- [x] A 200 response containing `error` raises `HandyApiException` with code/name; messages contain no connection key or device error message.
- [x] Firmware gate: `4.x`/`fw_status` 0 or 1 supported; `3.x`, `fw_status` 2, missing status, and unparsable versions unsupported.
- [x] Percent/relative conversion round-trips 0-100 and remains stable after device-normalized values.
- [x] Slider checks: signed single-thumb values, horizontal thumb rendering, range ordering, keyboard thumb switching/stepping, and coincident-thumb behavior.
- [x] Mouse hit-testing selects the nearest thumb; range dragging clamps at the other thumb; programmatic setting is silent; invalid persisted ranges fall back to 0-100.
- [x] Rapid stroke changes coalesce into one write containing the newest value.
- [x] A change during an in-flight stroke write produces exactly one latest-value follow-up; a pending write is cancelled when disconnect begins.
- [x] `DeviceTimeMs` no longer includes any offset; `OnOffsetChangedAsync`, `_offsetChangeCts`, and `AppSettings.OffsetMs` were removed from the runtime code.
- [x] A settings file containing legacy `OffsetMs` loads without error, the value is not applied, and the property is omitted on serialization.
- [x] An unopposed poll adopts device offset/stroke values; raced, pending, in-flight, or user-adjusted values are rejected by the poll acceptance rule; poll application sends no PUT.
- [x] Polling skips a busy gate, does not overlap ticks, and does not start after disconnect or close begins.
- [x] Poll failures preserve selected values and connection state; repeated failures log once and reach *device values unknown*.
- [x] Core slider settings, capabilities, and slider state properties parse correctly.
- [x] Failed optional capabilities/settings reads leave controls usable and omit the related details.
- [x] Capabilities with `slider` 0 disable stroke controls and suppress stroke reads and writes.
- [x] Temperature bands cover thresholds and hysteresis; the fallback trigger is used without settings; one log entry is emitted per transition; temperature is read every third poll tick; polling issues no playback command.
- [x] Video actions are separately labeled `Attach to mpv`, `Detach from mpv`, `Play Video`, and `Pause Video`; transport buttons are disabled until attached and track mpv's observed pause state.

## Manual Interaction (running app with device)

- [x] Connect after an online player changed offset and stroke: on 2026-10-15, HandySync adopted the device values without issuing offset/stroke PUTs, and playback setup completed.
- [x] During playback, dragging the offset slider changes sync perceptibly without stopping/restarting playback; the log shows a small number of writes.
- [x] During playback, adjusting min/max stroke takes effect without interrupting playback; displayed values match the device's accepted values.
- [x] On the range trackbar, dragging either thumb into the other stops at it without moving the other thumb; keyboard thumb switching and stepping work; focus cue is visible.
- [x] Range trackbar is readable and usable at the form's normal size and at higher Windows display scaling.
- [x] Offset (horizontal) and Stroke Range (vertical) look consistent; both work in either orientation; in vertical orientation max is at the top and Up/Page Up increase the value.
- [x] Adjusting while paused applies values and does not start the device.
- [x] Adjusting while disconnected keeps values; they are applied on connect.
- [x] Simulated API failure (for example network disconnected) keeps selected values, shows *not applied*, does not disconnect, and a later change or reconnect applies them.
- [x] With HandySync connected and playing, change offset and stroke in an online player: HandySync's controls update within one poll interval, show *changed by another app*, and log old/new values; playback is not interrupted.
- [x] Switching from the browser back to HandySync refreshes values immediately.
- [x] Dragging a control while a poll returns a different device value: the user's drag wins and the final user value is written.
- [x] Polling does not noticeably delay play/pause/seek response or drift correction; request rate stays within the API rate limit (check `X-RateLimit-Remaining` in a debug log if available).
- [x] Disconnect and close during a pending write complete promptly without errors or late writes.
- [x] Values persist across app restart; an existing legacy `OffsetMs` is discarded with one log entry and does not shift playback.
- [x] Stroke tooltip shows the physical stroke and speed limits from the device; motor temperature updates during playback with the expected color and text for its band, readable in the normal theme; status clears on disconnect. Record whether the provisional 10 °C warm band and 75 °C fallback felt right during trial, and any adjusted values.
- [x] No pre-v4/update-required device was available (current device is 4.3.0); unsupported firmware was not tested on hardware and the gate is covered by automated tests.
- [x] Confirmed the Video/Handy button grouping is clear at the form's normal size and display scaling (2026-10-16).
- [x] Existing behavior unchanged: script load/swap, play/pause/seek, periodic synctime, mpv attach/detach, Handy connect/disconnect.

## Required Gates

Build the solution and run the HandySync tests and existing suites. Record exact commands and results. Builds alone do not verify device behavior; Common model tests do not verify HandySync UI or device effects. Update permanent HandySync architecture/backlog only to what was implemented and verified.

- [x] `dotnet test ScriptSyncPilot.slnx --verbosity minimal -p:BaseOutputPath=artifacts/transport-controls/bin/ -p:UseAppHost=false` (2026-10-16): passed, 129 tests.
- [x] `dotnet build ScriptSyncPilot.slnx --verbosity minimal -p:BaseOutputPath=artifacts/transport-controls/bin/ -p:UseAppHost=false` (2026-10-16): passed.
- [x] Focused tests for slider logic, write coordination, polling, temperature cadence, HSSP reconnect ordering, non-invasive connect adoption, and video transport labels/state passed.

## Results and Limitations

The standalone device probe and API contract checks were run on 2026-10-14. Connect-time adoption was confirmed on 2026-10-15. Manual HandySync interaction checks, including offset/stroke persistence across reconnect and power cycle and the Video/Handy button grouping, were confirmed by 2026-10-16. Offset bounds and invalid stroke normalization remain unspecified by the API and were not probed.
