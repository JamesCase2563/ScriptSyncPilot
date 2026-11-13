# Code review

## Medium: A stale playback failure can suspend a newer command

**File:** `src/ScriptSyncPilot.HandySync/MainForm.cs:1502`

Playback event handlers run fire-and-forget. When a command fails, its `finally` block releases the device-command gate before `HandlePlaybackCommandFailure` runs. A queued command for the same mpv session and Handy client can proceed first, but the older failure can still pass the handler's session and client identity checks and set `_hsspReady = false`, stopping synchronization.

Serialize failure-state updates with command execution, or validate a playback-command generation/version before applying them.
