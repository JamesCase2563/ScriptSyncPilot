# ControllerHost Roadmap

Phase 0 — Planning (current)
- Create plan and tasks (this folder)
- Agree on interfaces and MVP scope

Phase 1 — Skeleton & Core API (1 week)
- Create projects and add solution entries
- Define `IControllerHost` in `ScriptSyncPilot.Core`
- Add basic unit tests

Phase 2 — Adapter integration (2 weeks)
- Implement `ButtplugAdapter` inside ControllerHost
- Provide adapter registration and lifecycle management

Phase 3 — GUI & Headless (2 weeks)
- Build minimal WPF GUI: device list, connect controls, logs
- Add headless CLI/service mode and config

Phase 4 — Testing & polish (1 week)
- Integration tests, telemetry and logs, docs and release notes
