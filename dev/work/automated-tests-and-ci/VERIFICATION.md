# Automated Tests and CI Verification

**Status:** In progress. M1.1 coverage mapping is complete; the identified gaps and CI follow-up remain open.
**Issue:** [#21 — Automated Tests and CI](https://github.com/JamesCase2563/ScriptSyncPilot/issues/21).
See the [plan](PLAN.md).

## Scope

Issue #21 calls for focused tests across mpv attachment/lifecycle behavior, session replacement and stale notifications, Handy setup and teardown ordering, and timeline limits/editing/keyboard routing, together with a clean-checkout build/test gate. This record distinguishes existing/reused evidence from scenarios that still need confirmation.

## Current repository and CI baseline

- `src/ScriptSyncPilot.slnx` contains the Common, FunscriptEditor, and HandySync applications/libraries and two test projects: `ScriptSyncPilot.Common.Tests` and `ScriptSyncPilot.FunscriptEditor.Tests`.
- The Common suite covers Funscript behavior, action history, pattern generation/insertion, and `MpvSession` behavior.
- The FunscriptEditor suite includes WinForms tests and shared Handy HTTP/lifecycle tests; it references both HandySync and FunscriptEditor.
- `.github/workflows/windows-release-verification.yml` checks out the repository, sets up .NET 10, restores the solution, builds Release, and runs all solution tests with a hang timeout. Pull requests to `main` and `production-readiness` are triggers when `src/**` changes; pushes to `production-readiness` and manual dispatch are also triggers.
- Existing release verification records a clean-checkout GitHub Actions run on the production-readiness candidate: restore/build/test passed, with 341/341 tests at that revision (90 Common and 251 FunscriptEditor). This is historical evidence for that commit, not a current run for every later revision.
- Later local full-suite evidence is recorded in work-specific verification notes, including 345 passing tests after the fine-horizontal-navigation changes. It does not itself prove a clean-checkout CI run for this work item.

## Issue scenario matrix

| Issue scenario | Existing/reused test evidence | Boundary and remaining gap |
| --- | --- | --- |
| mpv attach failures | `HandySyncErrorReportingTests.FailedMpvAttachLeavesActionableStatusAfterCleanup` exercises a form attach failure and checks the final status after cleanup. `MpvSessionTests.Attach_PreCancelledTokenDoesNotConnect` checks pre-cancelled attach at the Common layer. | No controlled-pipe test currently cancels an attach after connection has started; form test is a failure-path check, not a complete attach/dispose lifecycle matrix. |
| mpv observations and pipe behavior | `MpvSessionTests.Attach_ObservesSpeedWithDistinctIdWithoutChangingPlayerSpeed`, `SpeedObservations_ReportConfiguredRateIncludingOutsideEditorBounds`, `InvalidSpeedObservations_PreserveStateAndReaderContinues`, `ExistingPropertiesAndPlaybackCommands_RetainMediaTimeSemantics`, and `ServerDisconnect_NotifiesOnceAndSpeedWriteReportsTransportFailure`. `PipeEndpoint` is a local named-pipe server. | Covers observation commands, property changes, malformed values, disconnect notification, and a failed command after disconnect. Does not assert all app-level attach/detach event ordering or disposal exactly once. |
| mpv disposal/cancellation | `MpvSessionTests.LifetimeCancellation_EndsObservationWithoutHanging`, `Attach_PreCancelledTokenDoesNotConnect`; `PipeEndpoint.Dispose` disposes the session/server and joins the reader. | Helper cleanup is exercised, but no test asserts disposal behavior while attach/read/write is in flight, or that callbacks cannot arrive after disposal. |
| Session replacement and stale queued notifications | Archived [command-ordering verification](../../archive/2026-11-command-ordering/VERIFICATION.md): `DeviceControlPollingTests.QueuedPlaybackCallbackIsSuppressedAfterMpvSessionReplacement` covers queued position-resync, play/pause, and sync producers; `LateSuccessfulPlaybackCommandDoesNotReportForReplacementSession` covers late successful play/resync; `FirstSyncAfterMpvSessionReplacementUsesReplacementPosition` covers current-position selection. `PlaybackSpeedCommandTests.ApplySpeedUpdateIfCurrentSession_DropsQueuedUpdateAfterReplacement` checks a queued speed observation. | Tests are in the shared FunscriptEditor test project and exercise HandySync form behavior with direct target replacement, not full attach/detach. Common pipe tests verify transport disconnect, not app-level stale notifications. No equivalent FunscriptEditor app attach/session-replacement coverage was located. Archived evidence also calls out waveform IPC seek replacement as untested. |
| Handy ordering and script selection during setup | Archived [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md) reuses `PendingOffsetAndStrokeWritesAreCancelledWhenDisconnectBegins`, `DisconnectRejectsNewWritesWhileStopIsInProgress`, and `DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop`. Archived [command-ordering verification](../../archive/2026-11-command-ordering/VERIFICATION.md) covers selected queued and in-flight replacement cases. | `ScriptDiagnosticsTests.ActualSetupAndReplacement_KeepVisibleAndPersistentLogsSecretFree` covers setup/replacement outcome and diagnostics, not changing script selection while setup is blocked. No explicit script-selection race test was found. |
| Request draining and stop-before-dispose | `DeviceControlPollingTests.DisconnectCancelsInflightPlayAndSkipsQueuedSeekBeforeStop`, `DisconnectDrainHasHardDeadlineAndRetainsClientUntilLateWorkCompletes`, and `DisconnectStopThatIgnoresCancellationRemainsPendingUntilDeferredTeardownReleasesGate`; archived timeout/shutdown evidence documents cooperative/non-cooperative transport boundaries and deferred disposal. | Strong coverage for device command gate and Handy client teardown. It does not mean every HTTP producer/order combination is tested, and non-cooperative transport disposal remains deferred until safe completion. |
| Timeline zoom/scroll limits and deletion during drag | `NumericPositionEntryTests.MiddleDrag_PansAndClampsWithoutSeekingOrEditing`, `ShiftLeftDrag_PansAndClampsWithoutSeekingOrEditing`, and `PlaybackFollow_RecentersOutsideEitherEdgeWithoutSeekingOrChangingZoom` cover pan/follow viewport limits and zoom preservation. | These are pan/follow bounds, not the complete zoom-slider/scrollbar limit matrix. No deletion-while-dragging regression test was found. |
| Keyboard routing | `NumericPositionEntryTests.Decoder_MapsAllElevenPhysicalPositionsAndConsumesRepeats`, `Decoder_LeavesModifiersFocusAndOtherMessagesUntouched`, `Decoder_DoesNotConsumeNavigationRepeatsOrNumpad`, and `Routing_FocusedTimelineConsumesPhysicalKeyThroughWinFormsPreprocessing` cover numeric-key decoding and focused timeline preprocessing. | This is targeted timeline numeric/navigation key routing; not a general keyboard matrix for all controls or app-level session replacement. |
| Clean-checkout CI build/test gate | `.github/workflows/windows-release-verification.yml`; archived production-readiness verification records clean-checkout run 43565771417, 341/341 tests on revision `72a4e5e`. | Historical run proves that revision passed. Workflow PR path filter only includes `src/**`, so workflow-only or test-infrastructure changes outside `src/**` do not trigger the PR workflow unless a `src/**` path also changes. Need a current-revision CI run and explicit decision/fix for trigger coverage. |

M1.1 conclusion: reuse the archived Handy lifecycle/ordering and historical CI evidence as scoped above. The highest-value gaps to carry into M1.2/M2 are the complete mpv in-flight cancellation/disposal path, script-selection changes during setup, app-level stale notifications in both apps, timeline deletion during drag and zoom/scroll bounds, and CI trigger coverage for workflow/test-infrastructure changes.

## Reusable verification records

- [Timeout and shutdown](../../archive/2026-11-timeout-shutdown/VERIFICATION.md): Handy HTTP budgets/cancellation, command draining, stop attempts, cleanup, deferred disposal, and close behavior. Tests use controlled transports; physical stop under network loss is not guaranteed.
- [Command ordering](../../archive/2026-11-command-ordering/VERIFICATION.md): queued and late Handy/mpv target replacement scenarios, with explicit limits for direct target replacement and IPC waveform seek.
- [Firmware and capabilities](../../archive/2026-11-firmware-capabilities/VERIFICATION.md): simulated response boundaries and dependent-operation suppression.
- [Production readiness](../../archive/2026-11-production-readiness/VERIFICATION.md): prior full-suite clean-checkout CI evidence and environment metadata.
- Current editor tests include [`NumericPositionEntryTests`](../../../src/tests/ScriptSyncPilot.FunscriptEditor.Tests/NumericPositionEntryTests.cs); current shared tests include [`MpvSessionTests`](../../../src/tests/ScriptSyncPilot.Common.Tests/MpvSessionTests.cs).

## Runs

No test command or CI workflow was run while creating this planning/verification baseline. Do not treat the historical runs above as verification of future changes.

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| — | — | Focused tests for remaining issue scenarios | Not run for this work item. |
| — | — | Full solution Release build/test | Not run for this work item. |
| Historical; see linked record | Windows GitHub Actions, clean checkout, production-readiness candidate | Restore, Release build, full solution test | Passed: 341/341 on the recorded revision; this result predates later commits. |

## Limitations

- The existing workflow's pull-request path filter is limited to `src/**`; changes outside that tree do not trigger the PR gate. Confirm whether this is intentional for workflow/test infrastructure changes and update only if required by the desired gate.
- A passing prior CI run is not evidence that the current branch or a future PR passes.
- Reused tests do not prove behavior beyond their asserted conditions. The named test and archive boundaries above are part of the M1.1 audit; follow-up work should add direct attach/detach transitions, both-app stale notification cases, script-selection races, and exact timeline edge cases where absent.
- No real Handy hardware or external mpv service is required for the acceptance tests; simulated results must not be described as hardware certification.
