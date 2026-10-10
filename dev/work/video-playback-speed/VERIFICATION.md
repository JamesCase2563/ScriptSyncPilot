# Video Playback Speed Verification

**Status:** Planned checks only; implementation and verification not started. No tests or manual checks are claimed as passed.

See [plan](PLAN.md) and [design](DESIGN.md). UI bounds of 0.25-2.00x and 0.05x increments, no preferred-speed persistence, 1x editor-owned launch, and observation-only handling on attach/outside-range updates are agreed. mpv speed support is documented; these checks verify our integration, and observed speed refers to the configured rate rather than measured playback throughput.

## Automated IPC Checks

- [ ] Reusable speed state, observation/events, validation, and commands live in Common without dependencies on WinForms or editor settings; the editor does not duplicate IPC speed logic.
- [ ] Attachment sends observe_property for speed with an id distinct from time-pos, pause, and duration.
- [ ] Valid speed observations update stored state and raise SpeedChanged with the observed value.
- [ ] Initial state distinguishes unknown from an observed 1x rate.
- [ ] Null, wrong-type, nonpositive, or unrepresentable speed data is ignored safely; a following valid event is still processed.
- [ ] SetSpeedAsync writes the expected structured set_property speed command and numeric value.
- [ ] Invalid command values (zero, negative, NaN, infinity) fail before any write.
- [ ] Cancellation/disconnection follows existing session behavior without hanging the test or leaving background failures unobserved.
- [ ] Existing position, pause, duration, seek, and frame-step commands retain their behavior.

Use a controlled local named-pipe server with bounded cancellation and cleanup; no live mpv or physical device is needed for these tests. Reading a command verifies serialization, not player acceptance.

## Editor Contracts

- [ ] Speed controls are disabled while disconnected/attaching; actual-rate display shows unknown rather than a falsely confirmed 1x.
- [ ] Current-session speed observations update the display on the UI thread without feedback writes.
- [ ] Queued speed updates from old sessions are ignored after attach replacement/disconnect/close.
- [ ] User edits/reset target the captured active session; late completion/failure does not overwrite a replacement session's UI.
- [ ] Failed writes produce actionable/logged feedback, not an unhandled UI exception or a false confirmed rate.
- [ ] Actual-rate display is observation-driven rather than optimistic write completion.
- [ ] Outside-UI-range player observations remain visible and do not trigger clamped writebacks.
- [ ] Speed changes do not mutate funscript actions, metadata, selection, history, or dirty/saved state.
- [ ] Speed controls fit the existing transport layout without clipping normal Windows sizes/DPI.

## Manual Playback Checks

- [ ] Launch editor-owned mpv: it starts paused at 1x under the agreed launch policy.
- [ ] Attach to an external player already at a non-1x rate: display that rate and do not change it automatically.
- [ ] Select a slower rate, a faster rate, and 1x reset; actual mpv playback and the observed display agree.
- [ ] At 0.5x, media time advances approximately half as fast as wall time; timeline/action timestamps still use media time.
- [ ] Change speed while paused: playback remains paused and the playhead does not move because of the speed command.
- [ ] Left/Right frame steps still operate on video frames and retain their pause behavior.
- [ ] Shift/Ctrl relative seeks still move 1/10 media seconds at slowed and faster rates; arrow hold-repeat remains functional.
- [ ] Absolute timeline seeking and pattern start/end values keep the same media-time meaning at all rates.
- [ ] Numeric position entry during slow playback inserts at the current media timestamp, not a speed-scaled timestamp.
- [ ] Focus the speed numeric field: arrows/digits edit that field normally and do not author movement points.
- [ ] Change speed externally using mpv's controls; the editor reflects it without sending a loop of writes.
- [ ] Attach to a player outside the editor's edit range; show actual speed without silently changing the player.
- [ ] Disconnect, reconnect, or replace the session: disable/reset stale state and then show the new observed rate.
- [ ] Close while a speed command is pending; no late UI exception or stuck operation occurs.
- [ ] Restart the editor and attach again: no saved preferred speed is imposed under the agreed no-persistence policy.
- [ ] New/Open a script leaves video speed unchanged and preserves normal history-reset behavior.

## Shared Regressions and Required Gates

- [ ] Run all Common tests, including persistence/history and new IPC checks.
- [ ] Run all FunscriptEditor tests, including numeric entry/focus/repeat regressions.
- [ ] Build the solution, including both desktop consumers and test projects.
- [ ] Confirm HandySync remains unchanged: it gains no UI speed controls, automatic speed writes, or scheduling changes.
- [ ] Record manual checks actually performed and any unverified limitations.

If a running application locks build outputs, use an isolated artifacts path rather than terminating a user's unsaved session. Record exact commands, test counts, failures, and successful reruns. A build or fake-pipe test alone does not verify actual video rate or hardware synchronization.

## Results and Limitations

Planning documents created on 2026-10-10. The user confirmed control bounds/increments, no preferred-speed persistence, 1x editor-owned launch, and preserving external rates without clamped writebacks. No implementation, automated tests, or manual playback checks performed for this work item. The mpv stable manual documents speed control over 0.01-100x and runtime property manipulation. HandySync speed compensation, live recording, and exhaustive timing/latency measurement are outside scope.