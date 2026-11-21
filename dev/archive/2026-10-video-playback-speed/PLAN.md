# Video Playback Speed Plan

**Status:** Implementation and verification complete; [PR #6](https://github.com/JamesCase2563/ScriptSyncPilot/pull/6) under review.
**Selected:** 2026-10-10.
**Affected projects:** Common (mpv speed observation/command and IPC tests), FunscriptEditor (transport controls and session-safe UI), HandySync (device synchronization safeguard for unknown/non-1x shared-player rates). HandySync receives no speed controls or player speed writes.

Source: [editor backlog](../../FunscriptEditor/BACKLOG.md). Details: [design](DESIGN.md) and [verification](VERIFICATION.md).

## Goal

Change video playback speed from the editor to make review and point authoring easier. Show the player-observed rate and provide an explicit reset to normal speed without changing funscript timestamps or movement values.

## First-Version Scope

- Add speed observation and a validated speed command to Common's existing MpvSession.
- Keep all reusable speed state, observation/events, validation, and IPC commands in Common so a future player can use the same API; only editor controls and presentation belong in FunscriptEditor.
- Add a compact numeric speed control and 1x reset alongside editor playback controls; disable writes while disconnected or attaching.
- Reflect external mpv speed changes without sending feedback writes.
- Preserve the speed already configured in an externally attached player; do not set speed merely because attachment completed.
- Keep pause, seeking, frame stepping, timeline coordinates, and numeric position entry in media time.
- Keep speed outside document history and dirty state.
- Reuse current session identity/cancellation and UI-thread marshaling conventions.
- Suspend HandySync device playback with a warning at unknown/non-1x player rates; resume only at confirmed 1x, with queued-command rate checks.

Live pattern recording, frequency controls, speed keyboard shortcuts, new settings dialogs, HandySync non-1x rate compensation, reconnect redesign, and a general mpv request/response framework are outside scope.

## Agreed Decisions

- [x] Agreed numeric range: 0.25-2.00x, two decimal places, 0.05x steps. These are editor control limits; mpv documents a wider 0.01-100x range.
- [x] Do not save a preferred speed or automatically reapply it on attach/reconnect. Observe the current player rate. Editor-launched mpv starts explicitly at 1x.
- [x] Display an outside-UI-range observed rate separately without clamping or writing it back; keep the edit control within its own bounds.

## Milestones

### 1. Design

- [x] Record media-time, ownership, observation, and no-document-mutation contracts.
- [x] Specify automated IPC and manual interaction checks.
- [x] Confirm control range, increments, and session/persistence policy.

### 2. Common IPC Support

- [x] Observe mpv's speed property and expose validated speed state/change events.
- [x] Send set_property speed commands with finite positive input validation.
- [x] Add deterministic named-pipe tests for observation, commands, invalid data/input, and cancellation/disconnection behavior using existing MSTest conventions.

### 3. Editor Integration

- [x] Add compact speed editing, actual-rate display, and 1x reset.
- [x] Wire writes through the current session and lifetime token; log failures without unhandled UI exceptions.
- [x] Refresh observed values with session identity checks and suppress feedback writes.
- [x] Disable unavailable controls and clear stale observed state on shutdown/replacement. Enable speed controls only after a valid current-session observation.
- [x] Keep native numeric-field editing and existing timeline/keyboard behavior unchanged. Main-form playback shortcuts bypass the focused speed input; manual interaction verification passed.

### 4. Verification and Completion

- [x] Run automated checks and build both Common consumers; 103 solution tests pass and all projects build (2026-10-12).
- [x] Complete and record manual checks in VERIFICATION.md; late session-completion races use deterministic tests instead of timing-sensitive manual checks.
- [x] Update permanent Common/editor architecture and the editor backlog.
- [x] Record limitations and return deferred work to the owning backlog.
- [x] Record [PR #6](https://github.com/JamesCase2563/ScriptSyncPilot/pull/6).
- [x] After review, archive under dev/archive/YYYY-MM-video-playback-speed/ and update links.

## Outcome

Common's MpvSession observes speed with id 4, exposes nullable Speed and SpeedChanged, and provides SetSpeedAsync with finite-positive validation without imposing editor bounds. Commands do not optimistically change observed state. Nine deterministic named-pipe tests pass without launching mpv. On 2026-10-12, all 103 solution tests passed, and Common, both consumers, and both test projects built. Manual playback/layout checks passed; session-race and write-failure behavior has deterministic editor test coverage. HandySync suspends device synchronization for unknown/non-1x rates rather than compensating its hardware output; no exhaustive throughput/latency or hardware-output measurement was performed. PR: [#6](https://github.com/JamesCase2563/ScriptSyncPilot/pull/6).