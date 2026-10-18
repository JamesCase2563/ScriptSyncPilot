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
- Queued work rechecks client/session identity after acquiring it. Playback notifications retain their originating session, including queued waveform/duration updates.
- A session-transition semaphore serializes start/stop. Start remains disabled until teardown and readiness restoration finish.
- Disconnect suspends commands, drains outstanding work, awaits the final stop attempt, logs failures, then disposes the captured client.
- Closing awaits teardown. Normal session stop keeps the device connection and installs any selection changed during stop before restoring readiness.

These are implemented foundations, not a complete recovery state machine or coalescing queue. Timeouts/retries, recovery, duplicate suppression, and diagnostics remain [backlog work](BACKLOG.md).

## Requirements to Preserve

Retain command ordering and stop-before-dispose during service extraction. Recovery follows current mpv position without resetting video. Device controls require firmware 4+ with `fw_status` other than update-required; stroke controls are disabled when capabilities report no slider. This control-specific gate does not reject the separate HSSP connection/setup. Firmware updates are not a ScriptSyncPilot feature; users are referred to official manufacturer instructions. Never log connection/authentication credentials.