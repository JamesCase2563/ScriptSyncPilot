# Development Documentation

ScriptSyncPilot has two Windows desktop applications and one shared library. mpv owns video playback; the applications attach over its local IPC pipe.

| Project | Responsibility | Architecture | Future Work |
| --- | --- | --- | --- |
| ScriptSyncPilot.HandySync | Follow mpv and control the Handy through cloud HSSP | [Architecture](HandySync/ARCHITECTURE.md) | [Backlog](HandySync/BACKLOG.md) |
| ScriptSyncPilot.FunscriptEditor | Edit funscripts and optionally follow/control mpv | [Architecture](FunscriptEditor/ARCHITECTURE.md) | [Backlog](FunscriptEditor/BACKLOG.md) |
| ScriptSyncPilot.Common | Shared persistence, mpv IPC, and logging | [Architecture](Common/ARCHITECTURE.md) | [Shared backlog](BACKLOG.md) |

Start with the [system architecture](ARCHITECTURE.md). The [vision](VISION.md) describes possible directions, not an implemented specification or committed roadmap.

## Conventions

- Architecture describes what exists today and the contracts to preserve.
- Backlogs describe future work with priority and status. Implemented foundations are not unfinished tasks.
- When selecting a backlog item, create one feature/change folder under `dev/work/<name>/` for all work, regardless of affected projects. Do not use project-local work folders, `plans/`, or `initiatives/`. Keep milestones and tasks together in its `PLAN.md`.
- External API/file-format references belong in `docs/`, not in project plans.
- The obsolete controller documents have been removed. There is no ControllerHost or Core project, and Common is not a device controller.

Useful ideas from the earlier proposal survive in current architecture requirements and backlogs: command ordering, lifecycle safety, compatibility checks, diagnostics, privacy, tests, UI/session separation, and an optional local backend.

## Active Cross-Project Work

- [Production readiness](archive/2026-11-production-readiness/PLAN.md): coordinate ownership, legal/privacy review, product/protocol boundaries, reliability, and release readiness without duplicating component backlog details.

## Working Documents and Archives

This README is the canonical convention; conversational notes are historical context, not competing instructions.

All sub-project working documents live under `dev/work/<name>/`, whether they affect HandySync, FunscriptEditor, Common, or any combination. Identify the affected projects in the plan rather than through folder placement. Group documents by feature/change, not by release or PR. Permanent project architecture documents and backlogs remain in their project directories.

- `PLAN.md`: scope, affected projects, open decisions, milestones, task checklist, and status.
- `DESIGN.md`: implementation details when substantial enough to need a separate document.
- `VERIFICATION.md`: acceptance criteria, test results, and unresolved limitations.
- Add research/experiment notes only when useful; do not create empty placeholder documents.

On completion:

1. Update permanent architecture documentation to describe the implemented system.
2. Move remaining ideas into the owning backlog.
3. Record the outcome and PR reference in the plan.
4. Move the whole work folder to `dev/archive/YYYY-MM-<name>/` for all completed work, regardless of affected projects, and update links.

Archives are historical snapshots and are not maintained as current instructions. Permanent documentation must explain the current system without requiring archive consultation.

## Unit Tests

The solution includes [ScriptSyncPilot.Common.Tests](../src/tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj), a .NET 10 MSTest project using VSTest. Tests are discoverable in Visual Studio Test Explorer and VS Code's .NET testing integration.

Run from the repository root:

```powershell
dotnet test src/tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj
```

The initial suite covers Funscript action edits, sorted loading, empty documents, invalid JSON, lowercase serialization, preserved root properties, and CSV export. Each test uses an isolated temporary directory and cleans it up. Add tests for pure Common logic here; editor-specific generation/history logic should have its own test project if it lives in the editor rather than Common. No video player or physical device is required for this suite.