# Product Vision

**Status:** Future direction, not an implementation specification or committed roadmap.

ScriptSyncPilot supports creating, editing, and playing time-synchronized device scripts alongside video. The current approach uses mpv for video, FunscriptEditor for editing, HandySync for device synchronization, and Common for shared facilities.

## Goals

- Reliable synchronization through pause, seek, and device recovery.
- Practical editing that preserves script compatibility and metadata.
- Explicit device/session ownership and predictable shutdown.
- Privacy-conscious diagnostics.

## Possible Extensions

### Initial-release classification

Reviewed against the [approved support scope](archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
The current Windows editor/mpv and HandySync REST API v3/HSSP workflows, file
preservation, lifecycle verification and privacy-conscious diagnostics are **in scope**.
Reliable automatic device recovery remains a future goal, not an initial-release
guarantee.

- **In scope (implemented baseline):** undo/redo and reusable pattern insertion; preserve standard funscript interchange and metadata.
- **Deferred:** new markers/snapping features, multiple tracks and a richer project format.
- **Excluded from the initial release:** additional/local output backends, including Buttplug/Intiface, and their scheduler/latency/back-pressure requirements. Revisit only through a separate scope decision.
- **Experimental:** author-guided video motion detection in the editor backlog is exploration, not a committed feature.
- **Excluded from current scope:** firmware updates, ControllerHost, an embedded player, WPF rewrite, headless hosting, remote REST/WebSocket control APIs, and generic sensor integrations. They are not prerequisites for this release.

The component backlogs carry entry-level classifications; future ideas below do not
override those release boundaries.

### Future ideas

- Undo/redo, markers, snapping, reusable patterns, and eventually multiple tracks if a concrete workflow justifies them.
- Additional output backends, beginning with evaluating local Buttplug/Intiface as an alternative to cloud HSSP.
- A richer project format with tracks, device bindings, markers, video references, offsets, and metadata. Define versioning/JSON Schema only if adopted; preserve standard funscript interchange.
- A shared scheduler if local device output requires it, including latency compensation and back-pressure.

These ideas do not require ControllerHost, an embedded player, a WPF rewrite, headless hosting, or REST/WebSocket control APIs. Such changes need an explicit use case and design decision. Generic BLE/TCP sensor integrations are not part of the current direction.

Actionable work: [shared backlog](BACKLOG.md), [HandySync backlog](HandySync/BACKLOG.md), [editor backlog](FunscriptEditor/BACKLOG.md).