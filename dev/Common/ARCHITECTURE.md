# Common Architecture

Common is the shared .NET library used by HandySync and FunscriptEditor, not a controller host, device connection owner, or local output scheduler.

## Facilities

- `Funscript`: sorted action model, JSON persistence, and CSV export. Root extension data preserves unmodeled properties; actions use lowercase `actions`, `at`, and `pos`.
- `PatternGenerator`: pure triangle, asymmetric, and cosine movement generation returning read-only FunscriptAction candidates and preview metadata. Supports whole-cycle fitting, continuation from action snapshots, deterministic quantization, adaptive accuracy checks, and bounded output. It neither edits documents nor owns conflicts/history/UI.
- `PatternInsertionPlan`: immutable action snapshots, interior conflict/collision analysis, and pure Stop/Replace/Merge candidate resolution. Preserves existing boundaries, detects no-ops, and never commits changes to the source document. Application/history and dialog preference ownership remain separate.
- `MpvSession`: named-pipe JSON IPC, playback observations, and seek/pause/frame commands. It owns pipe/read cancellation, not process launch or application session policy.
- `AppLog`: shared file logging.

## Contracts

Empty action lists are supported for editing; HandySync imposes its own playback validation. Loaded actions are sorted, and CSV reserves the first line for metadata.

Consumers retain emitting session identity and reject stale callbacks. Disposal/cancellation does not make already-queued application notifications current. UI dispatch belongs to each application, not the IPC library.

Device HTTP clients remain in HandySync. Add code here only for genuine cross-project contracts, not to recreate obsolete Core/ControllerHost abstractions. Tests/CI/privacy guidance live in the [shared backlog](../BACKLOG.md).