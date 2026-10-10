# Video Playback Speed Plan

**Status:** Agreed design and scope; implementation not started. Control bounds, persistence, and external-rate handling are confirmed.
**Selected:** 2026-10-10.
**Affected projects:** Common (mpv speed observation/command and IPC tests), FunscriptEditor (transport controls and session-safe UI). HandySync receives no speed controls or synchronization changes.

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

Live pattern recording, frequency controls, speed keyboard shortcuts, new settings dialogs, HandySync device scheduling, reconnect redesign, and a general mpv request/response framework are outside scope.

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

- [ ] Observe mpv's speed property and expose validated speed state/change events.
- [ ] Send set_property speed commands with finite positive input validation.
- [ ] Add deterministic named-pipe tests for observation, commands, invalid data/input, and cancellation/disconnection behavior using existing MSTest conventions.

### 3. Editor Integration

- [ ] Add compact speed editing, actual-rate display, and 1x reset.
- [ ] Wire writes through the current session and lifetime token; log failures without unhandled UI exceptions.
- [ ] Refresh observed values with session identity checks and suppress feedback writes.
- [ ] Disable unavailable controls and clear stale observed state on shutdown/replacement.
- [ ] Keep native numeric-field editing and existing timeline/keyboard behavior unchanged.

### 4. Verification and Completion

- [ ] Run automated checks and build both Common consumers.
- [ ] Complete and record manual checks in VERIFICATION.md.
- [ ] Update permanent Common/editor architecture and the editor backlog.
- [ ] Record limitations and PR reference; return deferred work to the owning backlog.
- [ ] After review, archive under dev/archive/YYYY-MM-video-playback-speed/ and update links.

## Outcome

Planning documents created. No implementation or verification results yet. PR reference: not recorded.