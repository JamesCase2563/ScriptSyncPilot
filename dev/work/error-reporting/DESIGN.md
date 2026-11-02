# Error Reporting Design

**Status:** Approved by the maintainer on 2026-11-02; investigation and verification not started.
**Affected projects:** HandySync primarily; Common or FunscriptEditor only if a shared reporting defect is shown to affect the release gate.
**Production-readiness priority:** 2, error reporting.

## Goal

Verify that failures in the supported HandySync workflows are presented as failures, give users a useful next step where one is known, and keep credentials, hosted-script URLs, response bodies, and unsafe transport exception text out of user-visible diagnostics. Fix only defects found against this goal.

This is a verification work item, not a logging redesign. Preserve useful existing diagnostics and the current UI flow unless evidence shows it misleads users or hides an actionable failure.

## Existing baseline and inputs

- The credential and hosted-script URL protection work is complete; see the archived [design](../../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md).
- Request-budget and shutdown failures have typed timeout handling and safe stop-failure reporting; see [timeout-shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md).
- The production-readiness release gate requires visible/actionable failures, no false success, and secret-free diagnostics. See the [coordinating plan](../production-readiness/PLAN.md).
- Relevant implementation includes HandySync `MainForm` and `HandyApiClient`; inspect current code and existing tests before proposing a code change.

## M1.1 Characterization: implementation paths and existing coverage

Reviewed `src/ScriptSyncPilot.HandySync/MainForm.cs`, `HandyApiClient.cs`, and the focused tests in `tests/ScriptSyncPilot.FunscriptEditor.Tests/`. The API layer generally normalizes transport, status, response-shape, and device-error failures into `HandyApiException`; timeout expiry uses `HandyApiTimeoutException`. The form then chooses between a top-level error/status plus disconnect, an operation-specific log/status, or a recoverable optional-read/poll state.

| Area | Implementation paths | Existing automated coverage | Coverage boundary found in M1.1 |
| --- | --- | --- | --- |
| API error normalization and secret safety | [`HandyApiClient`](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs): `SendAsync`, `ReadBodyAsync`, `ReadResultAsync`, `EnsureSuccessAsync`, `ThrowIfDeviceError`, `DeviceError`, and upload response parsing | [`ScriptDiagnosticsTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptDiagnosticsTests.cs): transport exception sanitization, status/device error data, malformed upload/setup response, actual upload/setup replacement logs. [`HandyApiDeviceControlTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/HandyApiDeviceControlTests.cs): device-error envelope parsing and untrusted detail suppression. [`HandyScriptTransferCharacterizationTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/HandyScriptTransferCharacterizationTests.cs): upload and setup HTTP/device failures. | Good client-layer coverage for safe exceptions. This does not establish the form's visible message and state for every endpoint/failure. |
| Connect and initial device setup | [`MainForm.ConnectHandyAsync`](../../../src/ScriptSyncPilot.HandySync/MainForm.cs) catches connection/setup exceptions, logs `Handy error: …`, sets `Handy error`, then disconnects. `CheckDeviceFirmwareAsync` and `ReadAndAdoptDeviceControlsAsync` handle info/capability/settings/offset/stroke reads; non-timeout optional failures are logged and some controls remain usable, while timeout escapes to connect handling. `SetupScriptOnDeviceAsync` sequences upload and HSSP setup. | [`HandyApiTimeoutTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/HandyApiTimeoutTests.cs): non-cooperative connect, firmware-read timeout abort, upload timeout suppresses setup/play and readiness. [`ScriptDiagnosticsTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptDiagnosticsTests.cs): firmware/info parsing/logging and safe diagnostic data. [`FirstKeyConnectionTests`](../../../tests/ScriptSyncPilot.FunscriptEditor.Tests/FirstKeyConnectionTests.cs): connection-key selection and connect invocation. | Existing tests cover timeout flow and firmware logging, but M1.1 did not find a focused assertion for the final visible status/log after an ordinary non-timeout `ConnectHandyAsync` failure, nor a complete matrix of initial optional-read failures. |
| Script upload and setup/replacement | `SetupScriptOnDeviceAsync` and `ReSetupScriptAsync`; API failure construction in `HandyApiClient.UploadScriptCsvAsync` and `HsspSetupAsync` | `HandyScriptTransferCharacterizationTests` covers failed HTTP upload, absent/malformed upload URL, setup HTTP/device errors and cancellation. `ScriptDiagnosticsTests.ActualSetupAndReplacement_KeepVisibleAndPersistentLogsSecretFree` covers actual UI/persistent logs for upload/setup failures during script replacement. | Replacement-path UI logging is covered; initial-connect UI presentation is covered mainly by timeout tests, not the full set of ordinary error classes. |
| mpv attach and video transport | `AttachMpvAsync`, `SeekMpvAsync`, and `SetVideoPausedAsync` | [`MpvSessionTests`](../../../tests/ScriptSyncPilot.Common.Tests/MpvSessionTests.cs) covers pipe disconnect notification and a speed-write transport failure at the Common session layer. | M1.1 found no HandySync form-level tests for attach/seek/pause failure text or status. These are required integration paths because mpv is an external HandySync dependency. |
| Playback and synchronization | `OnMpvPositionChangedAsync`, `OnMpvPauseChangedAsync`, `OnSyncTickAsync`, and `HandlePlaybackCommandFailure` in `MainForm` | `HandyApiTimeoutTests` covers play/sync timeout state, stopped periodic sync, and stale timeout suppression after mpv/client replacement. `PlaybackSpeedCommandTests` covers Handy behavior for rate suspension/resumption and queued seek rechecks. `DeviceControlPollingTests` covers a queued play canceled on disconnect and stale seek suppression. | Timeout, lifecycle, and rate-gate behavior is covered. M1.1 did not find equivalent form-level assertions for ordinary HTTP/device errors during play, seek, or `synctime`, including whether users receive a sufficiently useful visible outcome. |
| Device control writes and polling | `WriteOffsetAfterDebounceAsync`, `WriteStrokeAfterDebounceAsync`, `PollDeviceValuesAsync`, and `UpdateTemperatureAsync` | `DeviceControlPollingTests` covers poll failure state after repeated errors, independent endpoint failure/recovery, recovery on successful poll, preservation of locally edited values, and not polling after disconnect/close. `HandyApiDeviceControlTests` covers API response parsing. | Poll failure behavior has focused UI-state tests. Existing named tests do not appear to drive an actual failed offset/stroke write through its catch/log path; `PollDoesNotReplaceAUserValueAfterItsWriteFailed` seeds the failure flags. Temperature-read failure presentation and write-timeout/failure presentation need explicit review. |
| Disconnect, stop, and application close | `DisconnectHandyCoreAsync`, deferred teardown helpers, `LogStopFailure`, `DetachMpvCoreAsync`, and the form closing handler | `DeviceControlPollingTests` covers stop failure/timeout, safe logging, cleanup, close without a shutdown dialog, non-cooperative stop, deferred disposal, repeated disconnect, and gate/deadline behavior. `HandyApiTimeoutTests` covers hard request timeout/cancellation behavior. | Strong lifecycle and stop-failure coverage. Review whether all ordinary detach/stop paths have consistent visibility; application close intentionally logs and continues rather than showing a shutdown error dialog. |

This is a path/test map, not a conclusion that each message is adequate. Record exact current text, UI surface, resulting state, next step, and sensitive-data assessment in the grouped [error-message inventory](ERROR-MESSAGE-INVENTORY.md). M1.1 is complete; the message-by-message characterization remains for the next M1 task.

## Proposed approach

1. Inventory failure paths for connect/setup, capability and device reads, upload, play/pause/seek/synctime, offset/stroke controls, disconnect/stop, and application close in the separate [error-message inventory](ERROR-MESSAGE-INVENTORY.md).
2. For each path, record the observable outcome: UI status/dialog/log, whether dependent work proceeds, whether readiness is cleared when state is unknown, and whether the text contains sensitive or arbitrary exception data.
3. Classify messages as actionable, safely descriptive but not actionable, misleading/false-success, hidden, or sensitive. Do not infer vendor-side causes from generic HTTP/network failures.
4. Add or adjust only the minimum safe message/state behavior needed. Keep timeout classification distinct from caller cancellation and avoid logging raw response bodies or arbitrary transport exception strings.
5. Verify representative failures with fake transports and UI/lifecycle tests; reuse archived credential/URL evidence where it directly applies.

## Constraints

- Never display or log connection keys, hosted-script URLs, private media paths where not needed, raw response bodies, or unreviewed exception text.
- A failed operation must not be described as successful. When the remote state cannot be known, say so plainly and prevent dependent commands from assuming success.
- Do not add retries, recovery state machines, structured logging infrastructure, telemetry, or new support channels here.
- Do not change normal successful behavior solely to make logs more verbose.

## Open questions for investigation

- Which UI messages, status labels, and logs are user-facing in the supported flows, and which are only developer diagnostics?
- Are any exception messages passed through from APIs that can contain credentials, URLs, response content, or local paths?
- Do any failures leave a stale ready/connected state or allow follow-on commands?
- Which current messages have a reliable next step, and where would inventing one exceed available evidence?

## Completion criteria

- Failure-path inventory covers the supported operation groups above.
- Relevant failure states are visible, do not imply success, and are safe to expose.
- Automated tests cover any changed behavior; test commands and outcomes are recorded in [VERIFICATION.md](VERIFICATION.md).
- Findings and any remaining limitations are linked from the production-readiness plan. No broad logging overhaul is required.
