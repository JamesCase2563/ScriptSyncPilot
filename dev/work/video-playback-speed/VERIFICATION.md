# Video Playback Speed Verification

**Status:** Common speed observation/state/events, validated commands, and nine deterministic IPC tests implemented. All 96 solution tests pass and the solution builds. Initial attach/rate-display, speed controls and field focus, media-time behavior, frame stepping, keyboard focus/Space behavior, no-jump/clean-state behavior, external speed observation, and outside-range handling are manually confirmed; remaining playback and UI checks are pending.

See [plan](PLAN.md) and [design](DESIGN.md). UI bounds of 0.25-2.00x and 0.05x increments, no preferred-speed persistence, 1x editor-owned launch, and observation-only handling on attach/outside-range updates are agreed. mpv speed support is documented; these checks verify our integration, and observed speed refers to the configured rate rather than measured playback throughput.

## Automated IPC Checks

- [x] Reusable speed state, observation/events, validation, and commands live in Common without dependencies on WinForms or editor settings; the editor does not duplicate IPC speed logic.
- [x] Attachment sends observe_property for speed with an id distinct from time-pos, pause, and duration.
- [x] Valid speed observations update stored state and raise SpeedChanged with the observed value.
- [x] Initial state distinguishes unknown from an observed 1x rate.
- [x] Null, wrong-type, nonpositive, or unrepresentable speed data is ignored safely; a following valid event is still processed.
- [x] SetSpeedAsync writes the expected structured set_property speed command and numeric value.
- [x] Invalid command values (zero, negative, NaN, infinity) fail before any write.
- [x] Cancellation/disconnection follows existing session behavior without hanging the test or leaving fixture reader failures unobserved; broken-pipe writes report IOException.
- [x] Existing position, pause, duration, seek, and frame-step commands retain their behavior.

Use a controlled local named-pipe server with bounded cancellation and cleanup; no live mpv or physical device is needed for these tests. Reading a command verifies serialization, not player acceptance.

## Editor Contracts

- [x] Speed controls are disabled while disconnected/attaching; actual-rate display shows unknown rather than a falsely confirmed 1x.
- [x] Current-session speed observations update the display on the UI thread without feedback writes.
- [ ] Queued speed updates from old sessions are ignored after attach replacement/disconnect/close.
- [ ] User edits/reset target the captured active session; late completion/failure does not overwrite a replacement session's UI.
- [ ] Failed writes produce actionable/logged feedback, not an unhandled UI exception or a false confirmed rate.
- [x] Actual-rate display is observation-driven rather than optimistic write completion.
- [x] Outside-UI-range player observations remain visible and do not trigger clamped writebacks.
- [ ] Speed changes do not mutate funscript actions, metadata, selection, history, or dirty/saved state.
- [ ] Speed controls fit the existing transport layout without clipping normal Windows sizes/DPI.

## Manual Playback Checks

- [x] Connection button shows Attach when disconnected and Detach after successful attach; Detach clears speed/transport state. If mpv was launched by the editor, Detach leaves it running and reattaching to its generated pipe reconnects without killing it; normal editor shutdown still terminates owned mpv.
- [ ] During playback at zoomed-in scale, reaching the visible right edge recenters the viewport on the playhead without seeking or changing zoom; end bounds are respected, paused updates stay still, and active mouse gestures are not interrupted.
- [ ] Middle-button drag pans the timeline horizontally without moving the playhead or changing actions/history. Test both boundaries, dragging outside the control, and release; left-button editing/scrubbing remains unchanged.
- [x] With the timeline focused, Space toggles Play/Pause once per press; holding it does not repeatedly toggle. Space retains native behavior in buttons/editable fields, and modified Space does not invoke the timeline shortcut.
- [ ] Launch editor-owned mpv: it starts paused at 1x under the agreed launch policy.
- [x] Attach to an external player already at a non-1x rate: display that rate and do not change it automatically.
- [x] Select a slower rate, a faster rate, and 1x reset; actual mpv playback and the observed display agree.
- [x] At 0.5x, media time advances approximately half as fast as wall time; timeline/action timestamps still use media time.
- [x] Change speed while paused: playback remains paused and the playhead does not move because of the speed command.
- [x] Changing speed or resetting to 1x does not jump the playhead or mark the script unsaved.
- [x] Left/Right frame steps still operate on video frames and retain their pause behavior.
- [x] Shift/Ctrl relative seeks still move 1/10 media seconds at slowed and faster rates; arrow hold-repeat remains functional.
- [x] Absolute timeline seeking and pattern start/end values keep the same media-time meaning at all rates.
- [ ] Numeric position entry during slow playback inserts at the current media timestamp, not a speed-scaled timestamp.
- [x] Focus the speed numeric field: arrows/digits edit that field normally and do not author movement points.
- [x] Click Play/Pause, frame/seek buttons, Attach/Launch, or 1x Reset: focus returns to the timeline without an extra seek. After clicking Play, a position key inserts immediately without needing to click the timeline; arrow hold-repeat still works. Editable pipe/speed fields retain native focus behavior.
- [x] Change speed externally using mpv's controls; the editor reflects it without sending a loop of writes.
- [x] Attach to a player outside the editor's edit range; show actual speed without silently changing the player.
- [x] Disconnect, reconnect, or replace the session: disable/reset stale state and then show the new observed rate.
- [ ] Close while a speed command is pending; no late UI exception or stuck operation occurs.
- [ ] Restart the editor and attach again: no saved preferred speed is imposed under the agreed no-persistence policy.
- [ ] New/Open a script leaves video speed unchanged and preserves normal history-reset behavior.

## Shared Regressions and Required Gates

- [x] Run all Common tests, including persistence/history and new IPC checks.
- [x] Run all FunscriptEditor tests, including numeric entry/focus/repeat regressions.
- [x] Build the solution, including both desktop consumers and test projects.
- [x] Confirm HandySync remains unchanged: it gains no UI speed controls, automatic speed writes, or scheduling changes.
- [ ] Record manual checks actually performed and any unverified limitations.

If a running application locks build outputs, use an isolated artifacts path rather than terminating a user's unsaved session. Record exact commands, test counts, failures, and successful reruns. A build or fake-pipe test alone does not verify actual video rate or hardware synchronization.

## Results and Limitations

Planning documents created on 2026-10-10. The user confirmed control bounds/increments, no preferred-speed persistence, 1x editor-owned launch, and preserving external rates without clamped writebacks. Common support and its automated IPC tests are implemented; editor integration and manual playback checks remain pending. The mpv stable manual documents speed control over 0.01-100x and runtime property manipulation. HandySync speed compensation, live recording, and exhaustive timing/latency measurement are outside scope.

### Common Speed Observation (2026-10-10)

MpvSession now observes speed using id 4 alongside existing observations. Nullable Speed distinguishes unknown from an observed 1x rate; SpeedChanged reports finite positive configured-rate observations. Null, wrong-type, nonpositive, nonfinite, and unrepresentable speed data do not replace valid state. No speed writes or editor controls added in this step.

`dotnet build ScriptSyncPilot.slnx --no-restore` succeeded for Common, both consumers, and tests. `dotnet test ScriptSyncPilot.slnx --no-build --no-restore` passed all 85 existing tests, 0 failed, 0 skipped. These are regression checks, not dedicated speed-observation evidence; the IPC checklist remains unchecked until its tests are implemented.

### Common Speed Command (2026-10-10)

Added SetSpeedAsync(double speed, CancellationToken), rejecting nonfinite and nonpositive input before using the existing structured set_property speed write path. The shared API does not impose editor control bounds or change observed Speed on write completion. Existing disconnected-send and cancellation behavior is unchanged.

`dotnet build ScriptSyncPilot.slnx --no-restore` succeeded; `dotnet test ScriptSyncPilot.slnx --no-build --no-restore` passed all 85 existing tests, 0 failed, 0 skipped. Dedicated command serialization/input-validation pipe tests remain the next milestone; no manual playback test was performed.

### Deterministic IPC Tests (2026-10-10)

Added [MpvSessionTests](../../../tests/ScriptSyncPilot.Common.Tests/MpvSessionTests.cs) with nine MSTest cases and a local NamedPipeServerStream fixture. No external mpv process, video, or device is launched. Tests cover distinct observation ids, unknown/valid/outside-editor-range speed values, invalid observations followed by valid events, structured commands, invalid command input, cancelled writes/attachment/lifetime, disconnect notification and broken-pipe write failure, and existing property/seek/frame-step semantics.

Initial fixture runs failed because AutoFlush was enabled before connection and command reads were not concurrent with Windows pipe writes/flushes. The corrected fixture continuously drains commands into a channel, bounds waits to ten seconds, and awaits its reader during cleanup. Cancellation assertions accept OperationCanceledException subclasses; disconnect tests expect IOException from a broken-pipe write. Production code was unchanged during these repairs.

`dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --no-restore --filter FullyQualifiedName~MpvSessionTests`: 9 passed, 0 failed, 0 skipped. `dotnet test ScriptSyncPilot.slnx --no-build --no-restore`: 94 passed, 0 failed, 0 skipped (88 Common, 6 editor). `dotnet build ScriptSyncPilot.slnx --no-restore` succeeded for both consumers and test projects. These tests verify IPC behavior, not actual playback speed or future editor controls.

### Automated Regression and Build Gate (2026-10-11)

`dotnet test ScriptSyncPilot.slnx --artifacts-path artifacts/video-speed-verification-final` passed 96 tests, 0 failed, 0 skipped (88 Common, 8 editor). The same command built Common, FunscriptEditor, HandySync, and both test projects successfully into isolated outputs. HandySync code and behavior were not changed for this feature; its project build passed.

### Editor Speed Controls (2026-10-10)

Added a grouped transport input with 0.25-2.00x bounds, two decimal places, 0.05x increments, a 1x reset button, and a player-rate display initially reading unknown. Input and reset remain disabled until command/observation wiring is implemented; no speed writes, observation updates, or keyboard changes added in this step.

`dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --no-restore` succeeded. Running-UI layout and playback behavior checks remain pending.

### Editor Speed Write Wiring (2026-10-10)

Speed input changes and the 1x reset now call SetPlaybackSpeedAsync, capturing the active MpvSession and lifetime token before awaiting Common's SetSpeedAsync. I/O, cancellation, disposal, and validation failures are caught; logging is limited to the still-current session while the form is open. Write completion does not change observed-rate presentation or document state. Controls remain disabled until observation wiring is implemented.

The first isolated build with --no-restore failed because the new artifacts directory had no assets file. `dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --artifacts-path artifacts/video-speed-verification` restored and built successfully. Manual command/reset behavior remains pending.

### Editor Speed Observation Wiring (2026-10-10)

Subscribed to SpeedChanged and refreshed cached Speed after publishing an attached session. Queued UI callbacks check session identity and closing/disposed state. A synchronization flag prevents observation-driven numeric changes from writing back. The display shows the configured observed rate; only the numeric presentation is clamped before decimal conversion. Speed/reset enable after a valid observation; shutdown/replacement disables both, resets the input under the guard, and clears the display to unknown.

`dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --no-restore --artifacts-path artifacts/video-speed-verification` succeeded. Manual attach, external speed changes, reset, outside-range presentation, and replacement behavior remain unverified; editor contract checkboxes remain pending.

Manual verification (2026-10-11): The user confirmed all three initial checks passed: disconnected startup shows unknown with controls disabled; attaching to an external player at non-1x displays its rate; and attachment leaves the player's speed unchanged.

Manual verification (2026-10-11): The user confirmed the slower-speed, faster-speed, and 1x reset checks passed on the attached player, with the displayed rate matching playback.

Manual verification (2026-10-11): The user also confirmed changing/resetting speed causes no playhead jump and does not mark the script unsaved. Other action/history/metadata invariants remain covered by the separate editor-contract check.

Manual verification (2026-10-11): The user changed speed externally in mpv and confirmed the editor reflected the new rate without feedback updates.

Manual verification (2026-10-11): The user confirmed an externally configured 3x rate remains unchanged and is displayed accurately despite exceeding the editor input range; the explicit 1x reset also works.

Manual verification (2026-10-11): The user confirmed Detach disables controls, reattach restores them, and closing/restarting mpv disables them again. Attaching after restarting the player also works.

Manual verification (2026-10-11): The user confirmed changing speed while paused leaves playback paused and the playhead steady; at 0.5x, media-time progression, relative seeks, absolute seeks, and script timestamps behave as expected in media time.

Manual verification (2026-10-11): The user confirmed forward/back frame stepping at non-1x speed moves one frame and leaves playback paused.

Manual verification (2026-10-11): The user confirmed the speed numeric field accepts native arrow/digit editing without triggering point entry or playback shortcuts; the 1x reset control also works.

Manual verification (2026-10-11): The user confirmed that after clicking Play, a top-row position key inserts without an additional timeline click and does not seek; arrow hold-repeat is preserved. Space Play/Pause also works as expected.

### Native Speed-Field Keyboard Editing (2026-10-10)

MainForm's playback KeyDown handler now returns without consuming keys while the speed NumericUpDown or its child text field has focus. Native numeric/caret editing remains with WinForms; timeline scan-code routing and navigation repeat handling are unchanged.

The isolated editor build succeeded. `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --artifacts-path artifacts/video-speed-verification --filter FullyQualifiedName~NumericPositionEntryTests` passed all 6 existing tests. These verify numeric-entry/routing regressions, not the new speed-field focus guard or actual mpv arrow hold-repeat; those manual checks remain pending.

### Transport Button Focus (2026-10-10)

Transport button clicks now immediately return focus to the timeline, without invoking timeline mouse handling or seeking. This allows numeric point entry after Play/Pause or stepping without a playhead-moving timeline click. Launch/Attach and 1x Reset also return focus; editable inputs are unchanged. The isolated editor build passed; the manual focus regression above remains pending.

### Space Play/Pause (2026-10-10)

Unmodified Space on the focused timeline raises PlayPauseRequested, wired to the editor's existing playback toggle. Held repeats are consumed without another toggle; other controls and modified Space retain existing processing. The isolated editor build and focused Routing_FocusedTimeline test passed, verifying one request per press and no action insertion. Actual mpv playback and native input behavior remain manual checks.

### Middle-Button Timeline Panning (2026-10-10)

Added captured horizontal panning with existing viewport clamp/scrollbar helpers. It does not seek or commit edits; release, capture loss, and document loading end the gesture. The isolated editor build and focused MiddleDrag test passed, covering direction, both bounds, release/capture loss, and unchanged playhead/actions/history. Physical mouse interaction remains a manual check.

### Playback Edge Following (2026-10-10)

Active mpv position updates now request viewport following: when the playhead reaches the visible right edge, the timeline recenters on it within existing bounds. Zoom and video position are unchanged; active pan/point-drag/scrub gestures suppress following, and paused/manual updates retain their viewport. The isolated editor build and PlaybackFollow/MiddleDrag tests passed (2 tests); actual playback interaction remains pending manual verification.

### Attach/Detach Button State (2026-10-11)

The connection button now reads Attach while disconnected and Detach after successful connection. Detach closes the editor IPC session and clears playback/speed state. If the editor launched mpv, explicit detach leaves that process alive; reattaching to its generated pipe preserves the owned process, while normal shutdown/replacement still terminates it. `dotnet build src/ScriptSyncPilot.FunscriptEditor/ScriptSyncPilot.FunscriptEditor.csproj --no-restore --artifacts-path artifacts/video-speed-verification` succeeded; all 8 editor tests passed using the isolated artifacts path. The manual interaction above remains pending.