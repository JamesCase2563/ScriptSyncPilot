# HandySync Architecture

## Responsibilities

HandySync follows an external mpv player and controls the Handy using REST API v3/HSSP. mpv attachment and device connection are independent: waveform/playhead tracking works without a device, and a Handy can join an existing session.

- `MainForm` owns script selection, observations, readiness, UI state, and lifecycle orchestration.
- `HandyApiClient` owns device/hosting HTTP clients.
- `ScriptWaveformControl` renders actions and raises video seek requests.
- Common supplies script parsing/CSV, mpv IPC, and logging.

## Setup and Synchronization

Connection checks connectivity, selects HSSP mode, samples client/server clock offset, and installs the latest selected script. Scripts are hosted as CSV before setup. Empty scripts are rejected; failed replacements retain the previous valid script/path.

Version-aware installation captures client/script snapshots, serializes setup, and rechecks selection after upload. Synchronization stays suspended until the current version is installed. mpv position is tracked even without device readiness. Play/pause/seeks issue HSSP commands, periodic synctime corrects drift, and the offset slider debounces adjustments. Clock sampling currently happens during device connection, not periodically.

## Ordering and Lifecycle

- One device-command semaphore covers connection requests, setup, playback, clock sync, and final stops.
- Queued work rechecks client/session identity after acquiring it. Playback notifications retain their originating session, including queued waveform/duration updates.
- A session-transition semaphore serializes start/stop. Start remains disabled until teardown and readiness restoration finish.
- Disconnect suspends commands, drains outstanding work, awaits the final stop attempt, logs failures, then disposes the captured client.
- Closing awaits teardown. Normal session stop keeps the device connection and installs any selection changed during stop before restoring readiness.

These are implemented foundations, not a complete recovery state machine or coalescing queue. Timeouts/retries, recovery, duplicate suppression, and diagnostics remain [backlog work](BACKLOG.md).

## Requirements to Preserve

Retain command ordering and stop-before-dispose during service extraction. Recovery follows current mpv position without resetting video. Add firmware/capability validation before setup when implemented. Never log connection/authentication credentials.