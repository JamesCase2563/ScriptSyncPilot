# Code review

## Medium: A stale playback failure can suspend a newer command

**Status:** Fixed and verified; regression test passed 1/1 on 2026-11-13.

**File:** `src/ScriptSyncPilot.HandySync/MainForm.cs:1502`

Playback event handlers run fire-and-forget. When a command fails, its `finally` block releases the device-command gate before `HandlePlaybackCommandFailure` runs. A queued command for the same mpv session and Handy client can proceed first, but the older failure can still pass the handler's session and client identity checks and set `_hsspReady = false`, stopping synchronization.

Serialize failure-state updates with command execution, or validate a playback-command generation/version before applying them.

**Resolution:** Playback callbacks now capture a monotonically increasing command generation before entering the device gate. Failure handling applies state changes only if its generation is still current, in addition to the existing session/client identity checks. A newer queued playback, resync, or sync callback therefore suppresses an older failure report. Regression coverage is in `DeviceControlPollingTests.StalePlaybackFailureDoesNotSuspendNewerQueuedCommand`.
