# ControllerHost — Task List

Status legend: `not-started`, `in-progress`, `completed`

1. Project skeleton and solution entries — completed
   - create `src/ScriptSyncPilot.ControllerHost` project — not-started
   - add to `ScriptSyncPilot.sln` — not-started

2. Core API and interfaces
   - define `IControllerHost` and `IController` APIs in `ScriptSyncPilot.Core` — not-started
   - add unit tests for API contract — not-started

3. Buttplug integration
   - implement `ButtplugAdapter` wiring inside ControllerHost — not-started
   - add connection and scanning UI hooks — not-started

4. GUI (Windows WPF)
   - basic device list & connect/disconnect controls — not-started
   - firmware/update warnings UI — not-started
   - log view & action buttons (scan, start server) — not-started

5. Headless / CLI
   - `--service` mode and config file support — not-started
   - graceful shutdown and restart behavior — not-started

6. Tests & CI
   - mock Buttplug server tests — not-started
   - basic integration test to verify command routing — not-started

7. Docs
   - update `docs/SPEC.md` with ControllerHost API references — not-started
   - add quickstart and troubleshooting — not-started
