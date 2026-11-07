# Error Reporting Verification

**Status:** Error-reporting verification complete 2026-11-03; full automated suite passed (312/312). The parent production-readiness release gates remain open.

## Scope and result

Production-readiness priority 2: failures must be visible and safely described, must not be represented as success, and must not expose keys or private hosted-script URLs. See [design](DESIGN.md), [plan](PLAN.md), and the [production-readiness release gate](../../work/production-readiness/PLAN.md).

## Evidence matrix

| Area | Scenario/evidence | Result | Notes |
| --- | --- | --- | --- |
| Connect/setup | Missing prerequisites, ordinary connect failure, firmware failure, upload/setup failure | Passed; some optional-read presentation remains partial | Validation and connect status regressions in `HandySyncErrorReportingTests`; setup secrecy and dependent-work tests in `ScriptDiagnosticsTests` and `HandyApiTimeoutTests`. |
| Playback/sync | Timeout, caller/lifecycle cancellation, ordinary failure state | Passed in full suite | Regression cases in `HandyApiTimeoutTests`; see 312-test run below. |
| Device controls | Failed offset/stroke write and temperature read | Passed in full suite | Regression cases in `DeviceControlPollingTests`; see 312-test run below. |
| Disconnect/close | Stop failure, timeout, cancellation, cleanup outcome | Partial prior evidence | See [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); identify remaining error-reporting checks. |
| Secret safety | Keys, hosted URLs, response bodies, unsafe exception text | Reviewed; automated checks passed | API errors expose normalized operation/status/code only; upload URLs are passed to setup and not logged; response bodies and device error names/messages are omitted. Remote firmware/model diagnostic fields are JSON-escaped and capped at 80 characters. Current visible and persistent-log secret checks are in `ScriptDiagnosticsTests`; archived credential evidence is [here](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md). |

## Runs

Record date, source revision, environment, exact command, discovered/passed counts, and result for every test or manual check. Do not record credentials or private URLs.

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| 2026-11-03 | Windows, .NET SDK 10.0.401; branch `work/error-reporting` | `dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~NonCooperativeTransport_CancellationHasHardWait_AndIsNotTimeout|FullyQualifiedName~CallerCanceledPlaybackCommand_ReportsCanceledAndUnknownDeviceState|FullyQualifiedName~LifecycleCanceledPlaybackCommand_IsSilentAndLeavesTeardownStateAlone|FullyQualifiedName~OrdinaryPlaybackFailure_ReportsUnknownDeviceStateAndStopsSync|FullyQualifiedName~TemperatureReadFailureClearsPreviousMeasurementAndRestoresNeutralColors|FullyQualifiedName~FailedOffsetAndStrokeWritesAreMarkedNotConfirmed" --verbosity minimal -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\.verification\bin\` | Build succeeded; test host discovered the assembly but returned no test results. Stopped after repeated waits; no pass/fail count claimed. |
| 2026-11-03 | Windows, .NET SDK 10.0.401; branch `work/error-reporting` | Retried the focused filter with `--logger "console;verbosity=detailed"`; also tried existing `CallerCancellation_RemainsCancellation_AndIsNotReportedAsTimeout` and `--list-tests` | The focused build succeeded and each invocation reached test assembly discovery, but neither test execution nor listing returned results. Stopped the runs after repeated waits; test counts remain unknown. |
| 2026-11-03 | User-provided test output for this worktree | `Tests\bin\Debug\net9.0-windows\ScriptSyncPilot.FunscriptEditor.Tests.dll` | Pre-fix run: 304 total, 296 passed, 8 failed. All eight reported failures were traced to cancellation assertion type, missing WinForms handles in new assertions, an incorrectly invoked temperature failure path, or a missing form handle for write logs. |
| 2026-11-03 | Windows, .NET SDK 10.0.401; current worktree after fixes | Focused rerun of the seven reported/new cases with normal `bin` output | Build succeeded; the local test host again stopped after discovery without results. Post-fix pass/fail count is unknown. |
| 2026-11-03 | Windows, .NET SDK 10.0.401; elevated execution outside sandbox from `F:\Users\email\repos\ScriptSyncPilot` | `dotnet test` | Passed: 304 total, 0 failed, 304 succeeded, 0 skipped (88 Common tests and 216 FunscriptEditor tests); FunscriptEditor test duration 12s. This post-fix full-suite run supersedes the earlier stalled sandboxed runs. |
| 2026-11-03 | Windows, .NET SDK 10.0.401; elevated execution outside sandbox from `F:\Users\email\repos\ScriptSyncPilot` | `dotnet test` after M3 status fixes and final form-level regressions | Passed: 311 total, 0 failed, 311 succeeded, 0 skipped (88 Common tests and 223 FunscriptEditor tests); FunscriptEditor test duration 12s. |
| 2026-11-03 | Windows, .NET SDK 10.0.401; elevated execution outside sandbox from `F:\Users\email\repos\ScriptSyncPilot` | `dotnet test` after remote metadata sanitization regression | Passed: 312 total, 0 failed, 312 succeeded, 0 skipped (88 Common tests and 224 FunscriptEditor tests); FunscriptEditor test duration 12s. |

## Findings and closure

- Message-safety review: API errors include only normalized operation names, numeric HTTP/device codes, and fixed transport/parse text. Response bodies, device error names/messages, connection keys, and hosted-script URLs are not logged. The upload URL is only passed into the setup request. Settings errors use fixed `SettingsException` messages; local script and mpv attach failures use static messages rather than raw paths/pipe exceptions. Hardware model and firmware values are JSON-escaped and capped at 80 characters; synthetic control-character, oversized-metadata, and private-session sentinels are checked in `ScriptDiagnosticsTests`.
- Confirmed defects corrected: caller/lifecycle cancellation was typed separately from timeout; unknown Handy state remains explicit; temperature, offset, and stroke failure state is truthful; mpv attach and Handy connect error statuses now survive cleanup; remote firmware/model text is escaped and bounded before logging.
- Changes and regression tests: full automated suite passed with 312/312 tests.
- Unresolved limits: exact presentation for every recoverable optional-read failure is not covered by form-level tests. These paths were reviewed in code; existing fixed/normalized reporting remains visible, and no known failure-reporting defect remains. Broader clean-checkout/CI coverage and other release gates remain tracked by the parent production-readiness plan.
- Release-gate conclusion: this error-reporting work item is complete. The overall production-readiness decision remains open.
