# ScriptSyncPilot — ControllerHost Plan

Purpose
- Define the scope, milestones, and tasks to implement `ScriptSyncPilot.ControllerHost` (the host for the Controller). The host provides a Windows GUI for interactive device management and a headless/CLI mode for automated/service scenarios.

Scope (MVP)
- Host `Controller` from `ScriptSyncPilot.Core` and expose a local API for scheduling device commands.
- Provide a Windows GUI to view devices, start/stop Buttplug server connections, and show firmware/update warnings.
- Provide a headless mode that can run as a background service or CLI process.
- Implement a simple Buttplug adapter integration (connect, scan, list devices, send vibrate/linear commands) with safe logging only (no auto-updates).

Milestones
1. Project skeleton and CI: create projects and solution, add basic build/test targets.
2. Core interface: define `IControllerHost` and the controller-proxy API.
3. Buttplug integration: add a working `ButtplugAdapter` implementation behind the ControllerHost.
4. GUI: basic WPF app showing device list, connection controls, and logs.
5. Headless: CLI flags for service mode, config file support.

Deliverables
- `src/ScriptSyncPilot.ControllerHost` project (GUI + headless entry points)
- `docs/plans/controller/TASKS.md` — actionable tasks and statuses
- Integration tests: connect/disconnect mock Buttplug server and verify command routing

Constraints & Notes
- The ControllerHost is the only component allowed to open device transports. Adapters must register with the host and send commands through the host's `IController` interface.
- Keep UI and core logic separated: GUI is a consumer of a service layer.
