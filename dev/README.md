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
- Create a plan only when selecting a backlog item for development. Keep milestones and tasks together in that plan under the owning project's `plans/` directory.
- External API/file-format references belong in `docs/`, not in project plans.
- The obsolete controller documents have been removed. There is no ControllerHost or Core project, and Common is not a device controller.

Useful ideas from the earlier proposal survive in current architecture requirements and backlogs: command ordering, lifecycle safety, compatibility checks, diagnostics, privacy, tests, UI/session separation, and an optional local backend.