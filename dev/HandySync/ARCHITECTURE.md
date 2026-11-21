# HandySync Architecture

## Responsibilities

HandySync follows an external mpv player and controls the Handy using REST API v3/HSSP. mpv attachment and device connection are independent: waveform/playhead tracking works without a device, and a Handy can join an existing session.

- `MainForm` owns script selection, observations, readiness, UI state, and lifecycle orchestration.
- `HandyApiClient` owns device/hosting HTTP clients.
- `ScriptWaveformControl` renders actions and raises video seek requests.
- Common supplies script parsing/CSV, mpv IPC, and logging.

## Setup and Synchronization

Connection checks connectivity, selects HSSP mode, samples client/server clock offset, and installs the latest selected script. Scripts are hosted as CSV before setup. Empty scripts are rejected; failed replacements retain the previous valid script/path.

Version-aware installation captures client/script snapshots, serializes setup, and rechecks selection after upload. Synchronization stays suspended until the current version is installed. mpv position is tracked even without device readiness. Play/pause/seeks issue HSSP commands and periodic synctime corrects drift. `DeviceTimeMs` uses the mpv position only; HSTP offset is device-side and applies immediately during HSSP playback. Clock sampling currently happens during device connection, not periodically.

The main form exposes an offset slider and a two-thumb stroke-range slider. On connect, device-control support is gated by firmware, slider capability is read, and current offset/stroke values are adopted into the controls/settings without writing them back. User changes are debounced and sent through the device-command gate. Offset/stroke are polled every five seconds and refreshed on form activation; external changes update the controls unless a local edit or write owns that value. Motor temperature is read every third poll tick. Device-control writes are independent of mpv playback state.

Device playback requires an observed mpv speed of exactly 1x. Unknown or non-1x rates stop the device and show a warning, including when another application changes the shared player's speed. Seek, periodic sync, and offset commands are suppressed and queued commands recheck the rate after acquiring the device gate. Restoring confirmed 1x resumes from the current mpv position if playing; paused video leaves the device stopped. Stop failures are shown explicitly and cannot guarantee that physical motion has ceased. HandySync never writes the player's speed. Non-1x hardware rate compensation remains unsupported.

## Ordering and Lifecycle

- One device-command semaphore covers connection requests, setup, playback, clock sync, and final stops.
- Every Handy REST/hosting request and response read has an explicit budget (10 s for status/control, 5 s per server-time sample, 60 s for upload) enforced through linked cancellation and an independent task wait. A timeout surfaces as a failed operation with safe operation text, distinct from caller cancellation, and never triggers dependent setup/play steps. If transport or body reads ignore cancellation, the request wait still ends, but cancellation sources, HTTP clients, and the device-command gate remain retained until the underlying task settles. Late failures are observed and late success cannot replace the reported timeout/cancellation.
- Queued work rechecks client/session identity after acquiring it. Playback notifications retain their originating session, including queued waveform/duration updates. Seek, play/stop, and synctime failures are handled only if their captured mpv session and Handy client are still current; a late timeout from a replaced target neither clears readiness, stops sync, nor logs/reports against the new target.
- A session-transition semaphore serializes start/stop. Start remains disabled until teardown and readiness restoration finish.
- Close waits at most 2 s for an in-progress mpv attach/detach transition, then proceeds. Once shutdown starts (the lifetime token is canceled), an outstanding transition skips any remaining UI, status, script-reinstall and device work: a detach that is queued for the command gate is canceled, and the final Handy stop belongs to the shutdown teardown. The transition only releases its mpv session and the gate, so it cannot touch the form after `Close()`. Status updates and mpv-disconnect callbacks are ignored once the form is closing or disposed.
- Disconnect suspends command admission, cancels pending debounced writes and in-flight requests for the captured client, and waits at most 60 s for the command gate. The stop request uses the 10 s control cancellation budget, while the caller waits at most 2 s for its task. If stop is still pending, disconnect remains pending and a deferred observer retains the client and command gate until that task completes; only then does it dispose the client and re-enable reconnect. If the command gate does not drain, disconnect/close proceeds at the 60 s deadline while retaining the captured client; a background continuation waits for the gate, attempts stop, and disposes only after in-flight work completes. Stop failure or timeout is logged safely and never reported as a confirmed stop. Repeated disconnect requests share the in-progress teardown.
- Timed-out or canceled request work cannot return success even if its transport ignores cancellation and later completes; the operation checks its linked token after the hard wait. Disconnect stop teardown also waits for underlying transport completion, not merely the bounded request task, before disposing or releasing the gate. A transport that never settles leaves teardown/reconnect pending; forcibly disposing it or admitting overlapping commands is not supported. Closing completes after best-effort cleanup without a shutdown error dialog. Normal session stop keeps the device connection and installs any selection changed during stop before restoring readiness.

These are implemented foundations, not a complete recovery state machine or coalescing queue. Retries, recovery, duplicate suppression, and expanded diagnostics remain tracked in [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues).

## Settings and Connection Keys

HandySync owns its device-specific settings and credential protection; these do not belong in Common. Settings live in `%APPDATA%\ScriptSyncPilot.HandySync\settings.json`. `AppSettings` saves an ordered `ConnectionKeys` array of versioned entries containing stable IDs, labels, and Base64-encoded Windows DPAPI `CurrentUser` ciphertext. The form uses the first key; edits update that entry without duplicating it.

Internal settings operations add a labeled key and return its ID, retrieve/decrypt the first key (null when none), and delete by ID. Deleting the first promotes the next; deleting the last clears the runtime key and persists an empty collection on save. Surviving IDs, labels, and order are preserved. Failed protection/promotion leaves state unchanged; invalid/unknown deletion IDs are reported explicitly. Operations affect in-memory settings until `Save` is called. Key-management UI remains deferred.

The form explicitly retrieves the first saved key at initialization. Connection uses the editable textbox's trimmed value, allowing re-entry or intentional changes; it does not silently revert edits to the stored value. An internal settings/client-factory constructor enables form-level request-header tests without a device or real network; the public constructor retains normal settings loading and client creation.

Legacy plaintext `ConnectionKey` values are ignored, not migrated; users are prompted to re-enter their key. Other settings retain their semantics. All saved entries must decrypt successfully before settings become usable. Load errors are reported and abort startup without overwriting the file; save errors are shown and prevent closing while allowing retry.

Save protects/serializes before file I/O, writes a unique create-new staging file in the settings directory, flushes it to disk, and atomically replaces the existing file (or moves it into place on first save). There is no truncate-in-place fallback. Candidate protected entries are committed in memory only after successful replacement. Handled failures clean up owned staging files; cleanup failures are logged without sensitive details. A crash before replacement may leave a protected temporary file. Filesystem/hardware durability and manual power-loss recovery are not guaranteed by the automated tests.

DPAPI protection is tied to the Windows account/profile; copying the settings file to another account/machine is not supported migration. It does not protect against same-user malware/processes, administrators, or an unlocked session. Runtime keys remain in managed memory and the input control.

## Diagnostics and Temporary Script URLs

Temporary hosting URLs are bearer capabilities. Upload completion logs only a status message; the returned URL is passed to HSSP setup but not shown in either log surface or saved in settings. Upload/setup and shared playback/sync HTTP diagnostics retain operation names, numeric HTTP statuses, and numeric device error codes, not response bodies, HTTP reason phrases, device error names/descriptions, or raw transport exception details. Unsafe inner exceptions are not retained. Parsed device errors no longer populate `HandyApiException.Name`.

Transport/read cancellation stays a cancellation exception with safe operation text. Malformed upload or nonempty PUT responses fail explicitly rather than becoming success-shaped defaults; empty successful PUT responses remain accepted. Existing historical logs are not rewritten or rotated by this feature.

## Requirements to Preserve

Retain command ordering and stop-before-dispose during service extraction. Recovery follows current mpv position without resetting video. Device controls require firmware 4+ with `fw_status` other than update-required; stroke controls are disabled when capabilities report no slider. This control-specific gate does not reject the separate HSSP connection/setup. Firmware updates are not a ScriptSyncPilot feature; users are referred to official manufacturer instructions. Never log connection/authentication credentials.
