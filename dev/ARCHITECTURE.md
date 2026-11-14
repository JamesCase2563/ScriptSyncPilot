# Current System Architecture

## Components

- **FunscriptEditor:** WinForms editor for timestamp/position actions. It can launch owned mpv or attach to an external player.
- **HandySync:** WinForms application observing mpv and controlling a Handy using REST API v3, script hosting, and HSSP.
- **Common:** Shared `Funscript`, `MpvSession`, and `AppLog` facilities. It does not own device connections or schedule hardware output.
- **mpv:** External source of playback position, pause state, and media duration. VR projection belongs to the external player, such as VR-Flat-Player.

```text
FunscriptEditor ----> Common <---- HandySync ----> Handy REST / hosting ----> Handy
                        |
                        v
                    mpv IPC
```

The applications share files/library code, not a controller service. The [solution](../src/ScriptSyncPilot.slnx) lists the actual projects.

## Ownership and Contracts

- HandySync owns device commands. The editor edits scripts and controls video, not the Handy.
- mpv position is the synchronization source of truth; device connection/recovery must not restart the video.
- Observations retain their emitting session identity. Obsolete sessions must not change replacement-session state.
- Device teardown drains commands, awaits the final stop attempt, and disposes the client afterward.
- Funscript persistence preserves unknown root properties and writes lowercase `actions`. Empty documents are allowed for editing; HandySync requires actions before setup.
- Firmware lifecycle is outside the scope of all ScriptSyncPilot applications: apps may report compatibility requirements, while documentation points users to the manufacturer's official instructions. ScriptSyncPilot does not download, install, or initiate firmware updates.

## Design Direction

Both applications currently orchestrate workflows in `MainForm`. Preserve the earlier proposal's useful UI/session separation as a direction: extract narrowly scoped services when warranted by complexity or testability, without introducing a mandatory controller host.

Keep IPC local by default. Never log device connection keys or authentication credentials. Recovery diagnostics should expose timing and state, not secrets. Any future remote-control surface needs explicit consent and access control.

Details: [HandySync](HandySync/ARCHITECTURE.md), [FunscriptEditor](FunscriptEditor/ARCHITECTURE.md), [Common](Common/ARCHITECTURE.md).