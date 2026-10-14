# Main-Form Device Controls Verification

**Status:** In progress. Device checks were performed on 2026-10-14; remaining results are listed below.

See [plan](PLAN.md) and [design](DESIGN.md).

## Device Contract Checks (before implementation is finalized)

Record firmware version, device model, and date for each result.

- [x] HSTP offset sign: observed 2026-10-14; negative offset delays device operation relative to video, and positive offset makes device operation run ahead of video.
- [ ] HSTP offset bounds or rejection behavior; the API contract specifies milliseconds, but magnitude has not been independently measured.
- [x] A change to HSTP offset during running HSSP playback takes effect immediately (observed 2026-10-14); no synctime or play was needed.
- [ ] HSTP offset and stroke persist on the device across HandySync disconnect/reconnect and device power cycle (record each).
- [ ] An online player's offset and stroke values are visible via `GET` after switching to HandySync.
- [ ] With HandySync connected, a change made in an online player becomes visible in HandySync via `GET`; record the observed delay. The standalone probe saw external stroke changes through GET within about 5-6 s on 2026-10-14; HandySync UI behavior remains to be tested.
- [x] `PUT /slider/stroke` with valid values returns the accepted values and mm range; 25-75% returned 30.165-80.495 mm, and 0-100% returned 5-105.66 mm (2026-10-14).
- [ ] `PUT /slider/stroke` with min >= max, out-of-range, or very small gaps: record rejection or normalization.
- [x] Stroke changes can be issued during operation; user confirmed the device responded to input from the probe and online player (2026-10-14).
- [ ] Record whether device errors arrive as HTTP 200 with an `error` object or as non-2xx.
- [x] Recorded device metadata on 2026-10-14: H01, firmware 4.3.0 (`fw_status` 0), one slider; physical range 0-110.66 mm, speed 32-400 mm/s, temperature trigger 80 °C and hysteresis 10 °C. `motor_temp` was 26.7 °C idle and about 29.3 °C while active.

## Automated Checks

- [ ] `HandyApiClient` sends correct path, method, and JSON body for get/set offset, get/set stroke, and info.
- [ ] Responses parse correctly, including stroke `min_absolute`/`max_absolute` and info `fw_status`/`fw_version`.
- [ ] A 200 response containing `error` raises `HandyApiException` with code/name; messages contain no connection or application key.
- [ ] Firmware gate: `4.x`/`fw_status` 0 or 1 supported; `3.x`, `fw_status` 2, and unparsable versions unsupported.
- [ ] Percent/relative conversion round-trips 0-100 and handles device-normalized values without drift.
- [ ] Range trackbar logic: a dragged or key-moved thumb is clamped at the other thumb minus the gap and never changes the other thumb; nearest-thumb hit-testing works, including coincident thumbs; setting values programmatically raises no change event; invalid persisted values fall back to 0-100.
- [ ] Write coordinator: rapid changes produce one write with the newest value; a change during an in-flight write produces exactly one follow-up write; cancellation after disconnect produces no write.
- [ ] `DeviceTimeMs` no longer includes any offset; no code path reads a local offset (`OnOffsetChangedAsync`, `_offsetChangeCts`, `AppSettings.OffsetMs` removed).
- [ ] A settings file containing a legacy `OffsetMs` loads without error, the value is not applied, and it is absent after the next save.
- [ ] Poll acceptance: a result is applied when nothing changed locally; discarded when the edit version changed, a write is pending or in flight, or the control is being dragged/key-adjusted. Applying a poll result raises no write.
- [ ] Poll skipping: a tick with the device-command gate busy issues no request; overlapping ticks do not run concurrently; no poll after disconnect starts or after close.
- [ ] Poll failures do not change control values or disconnect; repeated failures produce one log entry and the *device values unknown* state.
- [ ] Slider settings, capabilities, and slider state parse correctly; a missing or failed read leaves the controls usable and omits only the related details.
- [ ] Capabilities with `slider` 0 disable the stroke control and suppress stroke reads/writes.
- [ ] Temperature bands: normal below `trigger - 10`, warm up to `trigger`, hot at or above `trigger`; each band clears only after falling `temp_hysteresis` below its lower edge; default trigger used when slider settings are missing; one log entry per transition; no playback command is issued. Temperature is read every third poll tick only.

## Manual Interaction (running app with device)

- [ ] Connect after an online player changed offset and stroke: device values are logged and the agreed connect policy is applied before playback starts.
- [ ] During playback, dragging the offset slider changes sync perceptibly without stopping/restarting playback; the log shows a small number of writes.
- [ ] During playback, adjusting min/max stroke takes effect without interrupting playback; displayed values match the device's accepted values.
- [ ] On the range trackbar, dragging either thumb into the other stops at it without moving the other thumb; keyboard thumb switching and stepping work; focus cue is visible.
- [ ] Range trackbar is readable and usable at the form's normal size and at higher Windows display scaling.
- [ ] Offset (horizontal) and Stroke Range (vertical) look consistent; both work in either orientation; in vertical orientation max is at the top and Up/Page Up increase the value.
- [ ] Adjusting while paused applies values and does not start the device.
- [ ] Adjusting while disconnected keeps values; they are applied on connect.
- [ ] Simulated API failure (for example network disconnected) keeps selected values, shows *not applied*, does not disconnect, and a later change or reconnect applies them.
- [ ] With HandySync connected and playing, change offset and stroke in an online player: HandySync's controls update within one poll interval, show *changed by another app*, and log old/new values; playback is not interrupted.
- [ ] Switching from the browser back to HandySync refreshes values immediately.
- [ ] Dragging a control while a poll returns a different device value: the user's drag wins and the final user value is written.
- [ ] Polling does not noticeably delay play/pause/seek response or drift correction; request rate stays within the API rate limit (check `X-RateLimit-Remaining` in a debug log if available).
- [ ] Disconnect and close during a pending write complete promptly without errors or late writes.
- [ ] Values persist across app restart; an existing legacy `OffsetMs` is discarded with one log entry and does not shift playback.
- [ ] Stroke tooltip shows the physical stroke and speed limits from the device; motor temperature updates during playback with the expected color and text for its band, readable in the normal theme; status clears on disconnect. Record whether the provisional 10 °C warm band and 75 °C fallback felt right during trial, and any adjusted values.
- [ ] Unsupported-firmware state is shown if a pre-v4 or update-required device is available; otherwise record as not tested (and covered by automated gate tests).
- [ ] Existing behavior unchanged: script load/swap, play/pause/seek, periodic synctime, start/stop session, disconnect/reconnect.

## Required Gates

Build the solution and run the HandySync tests and existing suites. Record exact commands and results. Builds alone do not verify device behavior; Common model tests do not verify HandySync UI or device effects. Update permanent HandySync architecture/backlog only to what was implemented and verified.

- [x] `dotnet test ScriptSyncPilot.slnx --verbosity minimal` (2026-10-14): passed, 109 tests.
- [x] `dotnet build ScriptSyncPilot.slnx --verbosity minimal` (2026-10-14): passed.

## Results and Limitations

The standalone device probe and API contract checks were run on 2026-10-14. HandySync implementation and automated tests are in place. Manual testing of the HandySync UI with the device remains pending; remaining contract checks are listed above.
