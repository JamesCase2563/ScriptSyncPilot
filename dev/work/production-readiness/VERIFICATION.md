# Production Readiness Verification

**Status:** **NO-GO (2026-11-14).** Release verification is incomplete and release blockers remain; see the gate evaluation and dated decision below.

This record tracks automated and manual release evidence for the supported initial scope. A passing local run does not replace a CI run or the remaining release gates. The dated NO-GO decision below was recorded at the repository owner's request based on the evidence and open risks listed here.

## Automated build and test gate

| Field | Result |
| --- | --- |
| Release candidate | `production-readiness` at `25ddbdb850f87b98efb4117111bade56381ac186` |
| Checkout | Separate clean detached worktree at `F:\Users\email\repos\ScriptSyncPilot-release-check`; no files from the primary worktree were copied into it |
| Environment | Windows 11 x64, OS build `26300`; .NET SDK `10.0.401`; .NET 9 targets and Windows Desktop Runtime `9.0.11` |
| Restore | Passed for all five solution projects |
| Release build | Passed; 0 warnings, 0 errors |
| Full solution test run | **Not passed.** The 89 Common tests passed. The editor test host passed 102 tests, then stopped making progress at `HandyApiTimeoutTests.NonCooperativeTransport_HasHardBudget_AndDefersDisposal`. VSTest hang diagnostics captured a dump and aborted the host after two minutes. |
| Focused reproduction | The five cases in `NonCooperativeTransport_HasHardBudget_AndDefersDisposal` passed alone, and passed after `HandyApiDeviceControlTests` in a focused run (14 tests total). The hang is therefore order/context dependent and remains unresolved. |
| CI result | Pending. A Windows workflow has been added at `.github/workflows/windows-release-verification.yml`; it runs on pushes and pull requests, restores/builds/tests the full solution, and enables hang diagnostics. It has not run remotely because this change has not been pushed. |

Commands run from the clean worktree:

```powershell
dotnet restore ScriptSyncPilot.slnx
dotnet build ScriptSyncPilot.slnx --configuration Release --no-restore --verbosity minimal
dotnet test ScriptSyncPilot.slnx --configuration Release --no-build --no-restore --verbosity minimal
```

The focused checks that passed:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~NonCooperativeTransport_HasHardBudget_AndDefersDisposal"
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~NonCooperativeTransport_HasHardBudget_AndDefersDisposal"
```

The test suite contains coverage in these areas:

- **Lifecycle and command ordering:** device polling, disconnect/shutdown teardown, pending writes, stale playback callbacks and session replacement (`DeviceControlPollingTests`, `HandyApiTimeoutTests`, `PlaybackSpeedCommandTests`).
- **Protocol handling:** API endpoint construction, response parsing, firmware/capability checks, script transfer and HSSP behavior (`HandyApiDeviceControlTests`, `HandyScriptTransferCharacterizationTests`, `HandySettingsCharacterizationTests`).
- **Persistence:** actual settings save/load, protected key round-trips, migration and preservation of existing values (`ConnectionKeyCollectionTests`, `ProtectedSettingsTests`, `SettingsFailurePreservationTests`).
- **Failure paths:** request timeouts, malformed/error responses, unavailable services, failed reads/writes, cancellation, secret-safe errors and failed persistence (`HandyApiTimeoutTests`, `HandySyncErrorReportingTests`, `DeviceControlPollingTests`, `SettingsFailurePreservationTests`).

Coverage by test presence is not evidence that the complete suite passes. The full clean-checkout test gate remains open until the order-dependent hang is understood and the complete suite passes locally and in CI.

## Broader failure-path evidence (2026-11-13)

The following follow-up was run in an isolated Release worktree at base commit `1e5c9af9620e024f8147020baaee170661bcea36`, with the local changes listed below applied. Environment: Windows 11 x64, OS build `26300`, .NET SDK `10.0.401`, Release configuration. The solution built with 0 warnings and 0 errors. These focused runs verify the remaining parent-plan scenarios with controlled failure handlers and local IPC peers; they do not use a live Handy service or deliberately interrupt a physical device.

| Scenario | Automated evidence and result | Boundary |
| --- | --- | --- |
| Service outage and timeout | `HandyApiTimeoutTests`: **33/33 passed**. `DeviceControlPollingTests` + `HandySyncErrorReportingTests`: **63/63 passed**. Covers stalled/unavailable HTTP operations, 503 responses, failed reads/writes, bounded disconnect/stop attempts, and actionable unknown-state reporting. | HTTP services are represented by test handlers; no live service outage was induced. A failed/timeout stop is not reported as confirmed. |
| Protocol failure | `HandyApiDeviceControlTests` + `HandyScriptTransferCharacterizationTests` + `ScriptDiagnosticsTests`: **41/41 passed**. Covers malformed/error response bodies, device error objects, upload/setup failures and secret-safe diagnostics. | Synthetic protocol responses; no claim of physical-device behavior under malformed vendor responses. |
| Communication loss | `MpvSessionTests`: **10/10 passed**, including `ServerDisconnect_NotifiesOnceAndSpeedWriteReportsTransportFailure`. The 63-test HandySync group also covers failed mpv commands and automatic detach after a local named-pipe disconnect. | Local IPC disconnect is simulated. A live Handy/network link was not deliberately dropped; software cannot guarantee a physical device stop after communication is lost. |
| Settings preservation | `SettingsFailurePreservationTests` + `ProtectedSettingsTests` + `ConnectionKeyCollectionTests`: **46/46 passed**. Failed staged writes, denied replacement, corrupt/unreadable settings, and protection failures leave the existing settings bytes unchanged or refuse to replace them. |
| Funscript save failure and cancelled edits | `FunscriptTests`: **15/15 passed**, including injected partial staged-write failure preserving existing file bytes and cleaning the temp file. `UserFilePreservationTests`: **1/1 passed**; cancelling the discard prompt before New Script and before close preserves the on-disk file and keeps the current edit dirty. |

The focused test commands, run after the Release build in the isolated worktree, were:

```powershell
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiTimeoutTests"
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~DeviceControlPollingTests|FullyQualifiedName~HandySyncErrorReportingTests"
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~ScriptDiagnosticsTests"
dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MpvSessionTests"
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SettingsFailurePreservationTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ConnectionKeyCollectionTests"
dotnet test tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FunscriptTests"
dotnet test tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~UserFilePreservationTests"
```

Across these focused groups, **209/209 selected test cases passed**. Local source changes used for this follow-up: `Funscript.Save` now stages and replaces the destination atomically; a partial-write preservation test was added; the editor's discard confirmation is injectable so cancellation can be verified without a modal dialog; a test reflection lookup now selects the intended overload; and a stale diagnostics assertion now matches the current "Device controls unavailable" status wording.

The full solution was also rerun with hang diagnostics from this worktree. All **90 Common tests passed**; the editor test host passed **102 tests**, then again stopped at `HandyApiTimeoutTests.NonCooperativeTransport_HasHardBudget_AndDefersDisposal` until VSTest's two-minute inactivity timeout aborted it. That test class passes **33/33** when run by itself. Therefore the four requested scenario areas have passing focused evidence, but the complete-suite/CI release gate remains open until this order-dependent hang is resolved and CI passes.

## Remaining release gates

- Resolve the full-suite hang and rerun the complete Release suite from a clean checkout.
- Push the workflow and confirm a successful GitHub Actions run for the intended release revision.
- Review and complete the [source-release guide](../../docs/source-release.md), including obtaining, building, installing, updating, rollback, version tags, release notes, and faulty-release handling.
- Update both application targets from .NET 9, which reached end of support on 2026-11-11, to a supported runtime; verify the updated target and publish commands before a stable release.
- Publish a public support route and private security-reporting route before release. The repository is private, GitHub private vulnerability reporting is unavailable for it, and no public maintainer contact is configured. Privacy/data-flow information is documented in `docs/online-security.md`; Handy CSV retention and generated-URL lifetime remain unknown after no response to the email, with no further follow-up planned.
- Recheck the published support scope against the tested environment record and perform the remaining manual release workflows.
- After the blockers close, repository owner to review the updated evidence and record a new dated decision if warranted.

## Overall gate evaluation

| Release gate | Status | Evidence and remaining work |
| --- | --- | --- |
| Build, automated tests, and source-release process | **BLOCKED** | A clean Release build passed at candidate `25ddbdb850f87b98efb4117111bade56381ac186`, but the full solution test host hangs at `NonCooperativeTransport_HasHardBudget_AndDefersDisposal`; the focused failure-path groups passed 209/209. The Windows CI workflow is present but has not run remotely because this revision has not been pushed. The [source-release guide](../../docs/source-release.md) documents acquisition/build/update/rollback and tag/withdrawal procedures, but no version tag or release exists. The projects target .NET 9, whose support ended 2026-11-11. Resolve the full-suite hang, obtain passing CI, and retarget/reverify on a supported runtime before release. See [automated build and test gate](#automated-build-and-test-gate) and [broader failure-path evidence](#broader-failure-path-evidence-2026-11-13). |
| Supported scope and tested environments | **PARTIAL** | User-facing requirements and boundaries are in [SCOPE-AND-LIMITATIONS.md](SCOPE-AND-LIMITATIONS.md). [ENV-001](TESTED-ENVIRONMENTS.md#env-001) records an earlier Debug build with local changes. [ENV-002](TESTED-ENVIRONMENTS.md#env-002--firmware-capabilities-production-retest-incomplete-metadata) records a positive owner report but lacks the tested SHA, environment, and workflow list. Therefore current release-candidate compatibility is not established; complete a versioned environment record before making those claims. |
| Reliability and user-file preservation | **PARTIAL** | The four completed reliability sub-projects are linked from the [production plan](PLAN.md#initial-release-reliability-priorities); broader controlled failure tests passed 209/209 in [this verification record](#broader-failure-path-evidence-2026-11-13). This establishes focused simulated coverage for service/protocol failures, local mpv IPC loss, failed saves, and cancelled edits. The full-suite hang and lack of remote CI remain blockers; no live Handy/network interruption was induced, and a physical device stop cannot be guaranteed after communication loss. |
| Privacy and user support/security reporting | **BLOCKED** | The reviewed [privacy and security notice](../../docs/online-security.md) describes project data flows, local settings/logs, and the unresolved Handy retention/URL lifetime; README and [scope](SCOPE-AND-LIMITATIONS.md) warn users not to send personal data, connection keys, or private script URLs. However the repository is private, GitHub Issues are not externally accessible, and no external private security-reporting channel is published. Establish and verify both public support and private security routes before release. |
| Release decision | **NO-GO** | The repository owner records the dated decision below. Do not publish a stable release or mark the project production-ready while any blocker above remains open. |

## Remaining limitations and accepted risks

- The supported hardware claim remains limited to the two H01 devices and firmware versions in ENV-001; ENV-002 does not add a verifiable device/software combination.
- Handy's uploaded CSV retention period and generated URL lifetime are unknown. The provider did not answer the 2026-10-22 inquiry, and no further follow-up is planned. User-facing privacy information makes no expiry promise.
- Service/protocol and local IPC failures have simulated evidence, but no live Handy/network interruption or physical synchronization-loss test was performed. The application cannot guarantee that the device stops after communication is lost.
- Automatic reconnect, device-takeover recovery, session restoration, and expanded retry/backoff are deferred from the initial scope.
- The legal/privacy applicability assessment for GDPR and Norwegian/EEA obligations remains undetermined. No claim of compliance or non-applicability is made; see [PLAN.md open risks](PLAN.md#open-risks-and-follow-up).
- Distribution remains source-only; no published version tag, installer, or binary package exists.

## Dated go/no-go decision

| Field | Decision |
| --- | --- |
| Decision date | 2026-11-14 |
| Decision authority | Frank Yong, repository owner |
| Decision | **NO-GO for a stable source release and production-ready designation.** |
| Basis | Full-suite test hang; remote CI not yet run; .NET 9 target is out of support; external support and private security reporting routes are not available; final production-test metadata is incomplete. |
| Revisit when | All blockers in the gate table are closed, the intended clean release revision passes full CI on a supported runtime, the public support and private security routes are verified, and the owner reviews the updated evidence and environment record. |
