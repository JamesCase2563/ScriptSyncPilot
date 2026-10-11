# Video Playback Speed Design

**Status:** Implemented and verified (2026-10-11); PR pending.

See [plan](PLAN.md) and [verification](VERIFICATION.md).

## Playback Rate and Media Time

Playback rate changes how quickly mpv advances through video, not the meaning of video timestamps. At 0.5x, one second of media normally takes two seconds of wall time; seeking by one second still moves one media second.

Keep timeline playhead, duration, action timestamps, pattern intervals, and numeric position entry based on mpv time-pos. Never multiply or divide these coordinates by speed. Speed is not movement cycle frequency and does not modify actions, metadata, history, or saved-state checkpoints.

Frame-step and frame-back-step retain mpv's existing frame-based behavior and pause semantics. A speed change must not implicitly play, pause, seek, or reset the playhead. Audio pitch/time-stretch behavior remains owned by the player's configuration; this feature does not change it.

## Controls

Add a compact speed NumericUpDown, actual observed rate display, and explicit 1x reset in the main editor's transport area. Reuse existing WinForms styling and controls; no settings dialog, new toolbar icon library, or keyboard-binding scheme is needed.

Agreed edit bounds are 0.25-2.00x with two decimal places and 0.05x increments. Keep these configurable in code initially rather than introducing preferences. Reset explicitly requests 1.00x. These are UI safeguards within mpv's documented 0.01-100x range.

Playback-speed support is documented in the [mpv manual](https://mpv.io/manual/stable/#playback-control), with runtime control through the speed property and JSON IPC set_property/observe_property commands. Observed speed means the player's configured rate, not measured decoding/rendering throughput; playback can fall behind under load. The planned tests verify our IPC/UI integration and media-time behavior, not the existence of mpv's speed feature.

Show an unavailable/unknown actual rate until an observation arrives, rather than claiming an assumed default was read from mpv. The edit control may show 1.00 while unavailable but must be disabled. Enable speed editing/reset only for the current attached session. Numeric fields keep native arrow/digit behavior; focused speed editing must not insert funscript points. Do not extend numeric authoring shortcuts to these controls.

## Observation and Command Path

MpvSession currently observes time-pos, pause, and duration through JSON IPC. Extend that same abstraction with observe_property for speed using a distinct observation id, a SpeedChanged event, and an observed speed value that can distinguish unknown from 1x.

Accept finite positive numeric observations; ignore null, wrong-type, zero/negative, and nonfinite/unrepresentable data safely without changing valid state or terminating the reader. Follow existing parser conventions and keep defensive parsing scoped to speed.

Add SetSpeedAsync(double speed, CancellationToken) sending the structured JSON command [set_property, speed, value]. Validate finite positive inputs before writing; UI bounds belong to the editor, not the shared session API. Reuse the current send/cancellation mechanism. Do not introduce a separate pipe reader or general request/response infrastructure for this feature.

The existing SendAsync completes a pipe write, not an mpv acknowledgment. Display the actual rate from observation, not from successful write completion. Do not describe a successful write as player acceptance. Log transport/cancellation failures consistently with other playback controls; do not leave an unhandled async UI exception. Absence of an observation must not be treated as confirmation.

## Session-Safe UI Updates

Subscribe during attachment alongside position/pause/duration. Marshal speed events to the UI thread and check current session identity inside the queued callback, as the existing transport callbacks do. Ignore callbacks from disconnected/replaced sessions or after close.

Use a local synchronization flag while updating the numeric control from observations so a property event does not emit another set_property command. If the observed rate is within control bounds, show it in the edit control and actual-rate display. For outside-bound rates, show the observed configured value in the rate display, clamp only the edit control's presentation, and suppress writes during that update. Never silently force an externally attached player into the editor's range.

User input sends to a captured current session with that session's lifetime cancellation token. On completion/failure, do not update controls belonging to a replacement session. Keep interactions minimal; serialized/debounced writes may be added locally only if the existing transport path needs them to avoid overlapping speed writes. A broad command-coalescing redesign is outside scope.

Shutdown clears the observed-rate display and disables speed controls. Attach/reconnect waits for the new player's observation; it does not inherit stale speed from the prior session.

## Launch and Persistence

No speed preference is persisted in EditorSettings. Attaching to an existing player reads its current speed without writing it. Launching editor-owned mpv explicitly requests --speed=1, retaining the existing paused launch behavior. Replacing videos in an external player follows whatever speed mpv reports, not a locally inferred reset.

The 1x command is always an explicit user action. Changing script documents does not change video rate. This agreed policy avoids unexpectedly overriding a user's external player configuration.

## Shared Consumer Boundary

Reusable playback-speed behavior belongs in Common's MpvSession: speed observation/state/events, command serialization, and finite-positive validation. Keep it usable by a future player consumer without depending on WinForms or EditorSettings. The editor owns only its controls, chosen UI bounds, UI-thread/session presentation, and explicit user actions; do not duplicate IPC speed logic in MainForm. Shared behavior does not include editor-specific preferences or control layout.

HandySync also uses MpvSession. Adding observation/event support must not change its current play/pause/seek synchronization or issue speed writes from that application. Playback-rate-aware hardware output is a separate design problem and is not solved or promised here. If HandySync is attached to the same player, do not claim its device scheduling automatically compensates for slowed video; record this limitation and avoid adding implicit cross-application control.

## Test Strategy and Limitations

Use deterministic NamedPipeServerStream tests for speed JSON commands and property observations, without requiring live mpv or hardware. Follow existing MSTest/VSTest packages and reuse helpers if one exists; keep any new pipe helper bounded to these tests. Extend the editor test project for local observation/UI logic where practical without building a broad UI automation framework.

Actual playback speed, external-player changes, focus behavior, frame stepping, and the wall-time/media-time relationship were verified in a running mpv manual trial; late session-completion races are covered by deterministic tests instead of timing-sensitive manual checks. Results are recorded in [verification](VERIFICATION.md). Control bounds, increments, persistence, and outside-range presentation are agreed and verified. No universal player/firmware behavior, hardware-output compensation, or exact timing/latency accuracy is promised.