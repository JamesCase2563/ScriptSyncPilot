# Production Readiness Verification

**Status:** Active; release verification is not complete.

This record tracks automated and manual release evidence for the supported initial scope. A passing local run does not replace a CI run or the remaining release gates. Do not record a go/no-go decision until the repository owner reviews the evidence and open risks.

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

## Remaining release gates

- Resolve the full-suite hang and rerun the complete Release suite from a clean checkout.
- Push the workflow and confirm a successful GitHub Actions run for the intended release revision.
- Review and complete the [source-release guide](../../docs/source-release.md), including obtaining, building, installing, updating, rollback, version tags, release notes, and faulty-release handling.
- Update both application targets from .NET 9, which reached end of support on 2026-11-11, to a supported runtime; verify the updated target and publish commands before a stable release.
- Finish support/reporting and privacy information review; Handy's CSV retention and generated-URL lifetime remain unknown after no response to the email, with no further follow-up planned.
- Recheck the published support scope against the tested environment record and perform the remaining manual release workflows.
- Repository owner to review all remaining risks and record a dated go/no-go decision.
