# Production Readiness Verification

**Status:** **NO-GO (2026-11-14).** The .NET 10 full suite passes locally and in CI, but release blockers remain; see the gate evaluation and dated decision below.

This record tracks automated and manual release evidence for the supported initial scope. A passing automated run does not replace the manual environment trial or the remaining release gates. The dated NO-GO decision below was recorded at the repository owner's request based on the evidence and open risks listed here.

## Automated build and test gate

| Field | Result |
| --- | --- |
| Release candidate | `production-readiness` at `72a4e5ed2e41cce09bc2657f51f36452ff7b9934` |
| Local checkout | Clean source at `0d21dc889a8cb8d91b690fb3403cf0c0f0af251d`; the subsequent CI commit changes the workflow only |
| Local environment | Windows 11 x64, OS build `26300`; .NET SDK `10.0.401`; Microsoft.NETCore.App and Microsoft.WindowsDesktop.App `10.0.12` |
| CI environment | GitHub Actions `windows-2025-vs2026` image `20261103.298.1`; Windows Server 2025 x64, OS build `10.0.26100`; .NET SDK `10.0.404`, runtime `10.0.15` |
| Restore and Release build | Passed locally and in CI |
| Full solution test run | Passed locally and in CI: **341/341** (Common.Tests 90/90; FunscriptEditor.Tests 251/251) on `net10.0` and `net10.0-windows` |
| CI result | [Run 43565771417](https://github.com/JamesCase2563/ScriptSyncPilot/actions/runs/43565771417) passed on the exact release-candidate commit `72a4e5e`. |
| Prior hang | Resolved by avoiding synchronization-context capture in the non-UI timeout tests; the complete suite now passes locally and in CI. |

The local full-suite run used:

```powershell
dotnet test src/ScriptSyncPilot.slnx --configuration Release --verbosity minimal
```

CI run 43565771417 used these steps from a clean checkout:

```powershell
dotnet restore src/ScriptSyncPilot.slnx
dotnet build src/ScriptSyncPilot.slnx --configuration Release --no-restore --verbosity minimal
dotnet test src/ScriptSyncPilot.slnx --configuration Release --no-build --no-restore --blame-hang-timeout 2m --verbosity minimal
```

The focused checks that passed:

```powershell
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~NonCooperativeTransport_HasHardBudget_AndDefersDisposal"
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~NonCooperativeTransport_HasHardBudget_AndDefersDisposal"
```

The test suite contains coverage in these areas:

- **Lifecycle and command ordering:** device polling, disconnect/shutdown teardown, pending writes, stale playback callbacks and session replacement (`DeviceControlPollingTests`, `HandyApiTimeoutTests`, `PlaybackSpeedCommandTests`).
- **Protocol handling:** API endpoint construction, response parsing, firmware/capability checks, script transfer and HSSP behavior (`HandyApiDeviceControlTests`, `HandyScriptTransferCharacterizationTests`, `HandySettingsCharacterizationTests`).
- **Persistence:** actual settings save/load, protected key round-trips, migration and preservation of existing values (`ConnectionKeyCollectionTests`, `ProtectedSettingsTests`, `SettingsFailurePreservationTests`).
- **Failure paths:** request timeouts, malformed/error responses, unavailable services, failed reads/writes, cancellation, secret-safe errors and failed persistence (`HandyApiTimeoutTests`, `HandySyncErrorReportingTests`, `DeviceControlPollingTests`, `SettingsFailurePreservationTests`).

Coverage by test presence is not evidence that the complete suite passes. The complete .NET 10 suite passed locally and in the clean-checkout CI run linked above. The maintainer reports a successful manual trial; ENV-004 now records its build revision, runtime, mpv, device, and logged workflows, while the exact SDK patch and full workflow checklist remain incomplete.

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
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiTimeoutTests"
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~DeviceControlPollingTests|FullyQualifiedName~HandySyncErrorReportingTests"
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~ScriptDiagnosticsTests"
dotnet test src/tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MpvSessionTests"
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SettingsFailurePreservationTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ConnectionKeyCollectionTests"
dotnet test src/tests/ScriptSyncPilot.Common.Tests/ScriptSyncPilot.Common.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FunscriptTests"
dotnet test src/tests/ScriptSyncPilot.FunscriptEditor.Tests/ScriptSyncPilot.FunscriptEditor.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~UserFilePreservationTests"
```

Across these focused groups, **209/209 selected test cases passed**. Local source changes used for this follow-up: `Funscript.Save` now stages and replaces the destination atomically; a partial-write preservation test was added; the editor's discard confirmation is injectable so cancellation can be verified without a modal dialog; a test reflection lookup now selects the intended overload; and a stale diagnostics assertion now matches the current "Device controls unavailable" status wording.

The earlier full-solution run stopped at `HandyApiTimeoutTests.NonCooperativeTransport_HasHardBudget_AndDefersDisposal` after 102 editor tests. The timeout tests were updated to avoid capturing the synchronization context left by prior STA/WinForms tests. The subsequent full .NET 10 Release run passed locally and in remote CI; manual production workflows and other release gates remain pending.

## Remaining release gates

- Record the exact SDK patch and workflow checklist for ENV-004 when available. ENV-005 now records successful execution of both documented Release `dotnet publish` commands with `win-x64 --self-contained false` using SDK `11.0.100`; launching those Release outputs and validating them on a clean machine remain unverified.
- Publish a public support route and private security-reporting route before release. The repository is private, GitHub private vulnerability reporting is unavailable for it, and no public maintainer contact is configured. Privacy/data-flow information is documented in `docs/online-security.md`; Handy CSV retention and generated-URL lifetime remain unknown after no response to the email, with no further follow-up planned.
- Recheck the published support scope against the tested environment record and perform the remaining manual release workflows.
- After the blockers close, repository owner to review the updated evidence and record a new dated decision if warranted.

## Release procedure review (2026-11-14)

Reviewed [the source-release guide](../../docs/source-release.md) against the current `src/` project layout and .NET 10 target. It documents source acquisition by tag/archive, restore/build/test commands, framework-dependent `win-x64` publishing, versioned installation directories, update and known-good rollback steps, annotated SemVer tagging, release-note contents, and withdrawal/superseding-release handling. The guide clearly says no release/tag exists yet and prohibits tagging until the gates pass.

**Review result:** The documented procedure is complete for the source-only release model. No release is authorized by this review. ENV-004 logs confirm successful Debug local publishes and a successful maintainer-reported trial. ENV-005 records successful execution of both documented Release `win-x64 --self-contained false` publish commands using SDK `11.0.100`; launching the Release outputs and runtime behavior on a clean machine are not covered. The updated HandySync log identifies an HSTP offset-poll timeout at 10:23:34 and two later HSSP stop failures (device error 1001) at 10:25:12 and 10:25:15. The maintainer suspects the manual emergency stop button may explain these responses, but the log cannot confirm this; retain it as an unverified hypothesis. Release notes must include verified public support and private security-reporting routes before a public release.

## Overall gate evaluation

| Release gate | Status | Evidence and remaining work |
| --- | --- | --- |
| Build, automated tests, and source-release process | **PARTIAL** | The .NET 10 Release restore/build and complete 341/341 suite passed locally and in [CI run 43565771417](https://github.com/JamesCase2563/ScriptSyncPilot/actions/runs/43565771417). Both documented Release framework-dependent publishes passed with SDK `11.0.100` (see [ENV-005](TESTED-ENVIRONMENTS.md#env-005--release-framework-dependent-publish-commands)); launching those outputs and clean-machine runtime validation remain unverified. No version tag or release exists. See [automated build and test gate](#automated-build-and-test-gate) and [release procedure review](#release-procedure-review-2026-11-14). |
| Supported scope and tested environments | **PARTIAL** | User-facing requirements and boundaries are in [SCOPE-AND-LIMITATIONS.md](SCOPE-AND-LIMITATIONS.md). [ENV-003](TESTED-ENVIRONMENTS.md#env-003--net-10-release-build-and-automated-tests) records local and CI .NET 10 automated runs. ENV-004 now records a Debug .NET 10 trial on one H01 firmware combination with mpv IPC, upload, and HSSP setup evidence; the exact SDK patch and complete workflow checklist remain missing. ENV-001 retains the earlier two-device, broader manual workflow evidence on .NET 9 with local changes. Do not claim broader .NET 10 hardware compatibility than ENV-004 supports. |
| Reliability and user-file preservation | **PARTIAL** | The four completed reliability sub-projects are linked from the [production plan](PLAN.md#initial-release-reliability-priorities); broader controlled failure tests passed 209/209 in [this verification record](#broader-failure-path-evidence-2026-11-13), and the complete .NET 10 suite passed locally and in CI. This establishes focused simulated coverage for service/protocol failures, local mpv IPC loss, failed saves, and cancelled edits. No live Handy/network interruption was induced, and a physical device stop cannot be guaranteed after communication loss. |
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
| Basis | The .NET 10 runtime and full-suite test gates are satisfied locally and by CI on `72a4e5e`; the maintainer reports a successful manual .NET 10 Debug trial (ENV-004), and both documented Release publish commands pass on SDK `11.0.100` (ENV-005). NO-GO remains because external support/private security routes are not available while the repository is private, the ENV-004 SDK patch/checklist remain incomplete, and the trial log's two HSSP stop failures (device error 1001) have a plausible but unconfirmed emergency-stop-button explanation. |
| Revisit when | The repository is made public and both reporting routes are enabled and verified, the ENV-004 SDK/workflow details and stop diagnostics are reviewed, remaining support-scope evidence is reviewed, and the owner records a new decision. |
