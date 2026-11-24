# Common Architecture

Common is the shared .NET library used by HandySync and FunscriptEditor, not a controller host, device connection owner, or local output scheduler.

## Facilities

- `Funscript`: sorted action model, JSON persistence, and CSV export. Root extension data preserves unmodeled properties; actions use lowercase `actions`, `at`, and `pos`.
- `Funscript.ReplaceActionsAtomically`: validates and copies a complete sorted candidate before replacing actions. It preserves root extension data and returns `false` for a no-op; callers own notifications, dirty tracking, and history.
- `ActionEdit` / `ActionHistory`: minimal splice deltas between action lists and multilevel undo/redo stacks of them, with a saved-state checkpoint for dirty tracking. `Funscript.ApplyEdit` splices a delta and rejects one that does not match the current actions.
- `PatternGenerator`: pure triangle, asymmetric, and cosine movement generation returning read-only FunscriptAction candidates and preview metadata. Supports whole-cycle fitting, continuation from action snapshots, deterministic quantization, adaptive accuracy checks, and bounded output. It neither edits documents nor owns conflicts/history/UI.
- `PatternInsertionPlan`: immutable action snapshots, interior conflict/collision analysis, and pure Stop/Replace/Merge candidate resolution. Preserves existing boundaries, detects no-ops, and never commits changes itself. Its candidate can be committed to a Funscript with `ReplaceActionsAtomically`; UI/history and dialog preference ownership remain separate.
- `MpvSession`: named-pipe JSON IPC, playback observations, and seek/pause/frame commands. It observes mpv's `speed` property as a nullable positive finite value and exposes `SpeedChanged`; `SetSpeedAsync` sends finite-positive values without optimistically changing observed state or imposing consumer-specific bounds. It owns pipe/read cancellation, not process launch or application session policy.
- `AppLog`: shared file logging.

## Shared Segment-Speed Model

- [`SegmentSpeedCalculator`](../../src/ScriptSyncPilot.Common/SegmentSpeedCalculator.cs) measures one adjacent action pair in script space: signed elapsed milliseconds, absolute position distance in percentage points, and average percentage-points-per-second when the end timestamp is later than the start. Duplicate and decreasing timestamps retain their elapsed/distance values but have invalid timing and no speed; a positive-duration zero-distance pair is valid at `0 pp/s`.
- [`SegmentSpeedScaleCalculator`](../../src/ScriptSyncPilot.Common/SegmentSpeedScale.cs) fits a range from all valid adjacent segments, independent of viewport and zoom. It uses the nearest-rank P99 speed, rounds the upper bound up to the next 100 pp/s, and enforces a 100 pp/s minimum. With no valid segments it returns 0–100 pp/s.
- `SegmentSpeedColorRange` validates finite, non-negative ranges with minimum below maximum. It clamps out-of-range speeds to endpoint colors while retaining low/high clamp metadata; in-range color position uses the square root of normalized speed.
- [`SegmentSpeedPalette`](../../src/ScriptSyncPilot.Common/SegmentSpeedPalette.cs) provides the shared Jet-style rainbow as UI-neutral `RgbColor` values. Common owns measurement, fitting, mapping, clamp metadata, and palette RGB data; each application owns drawing and high-contrast adaptation.

The two apps persist matching speed-color fields in their own settings files. Settings are not shared or synchronized. These values describe script-space segment averages at normal (1x) playback, not physical device speed. HandySync further summarizes multiple segments in a waveform column by their maximum valid speed; see its [architecture notes](../HandySync/ARCHITECTURE.md#movement-speed-coloring).

## Contracts

Empty action lists are supported for editing; HandySync imposes its own playback validation. Loaded actions are sorted, and CSV reserves the first line for metadata.

Consumers retain emitting session identity and reject stale callbacks. Disposal/cancellation does not make already-queued application notifications current. UI dispatch belongs to each application, not the IPC library.

Device HTTP clients remain in HandySync. Add code here only for genuine cross-project contracts, not to recreate obsolete Core/ControllerHost abstractions. Tests/CI/privacy guidance live in the [shared backlog](../BACKLOG.md) and [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues).
