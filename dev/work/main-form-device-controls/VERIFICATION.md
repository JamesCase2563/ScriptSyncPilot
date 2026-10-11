# Main-Form Device Controls Verification

**Status:** Not started. Results below must distinguish automated coverage from user-performed device checks.

See [plan](PLAN.md) and [design](DESIGN.md).

## Device Contract Checks (before implementation is finalized)

Record firmware version, device model, and date for each result.

- [ ] HSTP offset sign: with a known script, a positive offset makes the device move earlier / later relative to video (record which).
- [ ] HSTP offset units are milliseconds; record any observed bounds or rejection.
- [ ] A change to HSTP offset during running HSSP playback takes effect immediately / requires synctime or play (record which).
- [ ] HSTP offset and stroke persist on the device across HandySync disconnect/reconnect and device power cycle (record each).
- [ ] An online player's offset and stroke values are visible via `GET` after switching to HandySync.
- [ ] With HandySync connected, a change made in an online player becomes visible via `GET`; record the observed delay.
- [ ] `PUT /slider/stroke` with valid values returns the accepted values and mm range.
- [ ] `PUT /slider/stroke` with min >= max, out-of-range, or very small gaps: record rejection or normalization.
- [ ] Stroke changes during HSSP playback take effect without stopping playback.
- [ ] Record whether device errors arrive as HTTP 200 with an `error` object or as non-2xx.

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
- [ ] Unsupported-firmware state is shown if a pre-v4 or update-required device is available; otherwise record as not tested (and covered by automated gate tests).
- [ ] Existing behavior unchanged: script load/swap, play/pause/seek, periodic synctime, start/stop session, disconnect/reconnect.

## Required Gates

Build the solution and run the HandySync tests and existing suites. Record exact commands and results. Builds alone do not verify device behavior; Common model tests do not verify HandySync UI or device effects. Update permanent HandySync architecture/backlog only to what was implemented and verified.

## Results and Limitations

Planning documents created on 2026-10-11 from the backlog item and the v3 OpenAPI spec. No implementation or device checks yet.
