# Automated Tests and CI Verification

**Status:** In progress. Existing test and CI evidence is available; the issue's remaining scenario matrix has not yet been reconciled and closed.
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

| Scenario from issue #21 | Existing/reused evidence | Status / remaining verification |
| --- | --- | --- |
| mpv attachment failures, cancellation, observations, and disposal with a controlled local pipe server | `ScriptSyncPilot.Common.Tests/MpvSessionTests.cs` covers session behavior; inspect exact cases against the requested attach/cancel/observe/dispose matrix. | Partial; map case-by-case and add uncovered integration/lifecycle cases. |
| Session replacement and stale queued notifications in both applications | Archived command-ordering plan and verification cover Handy command suppression and mpv session replacement for specified callbacks. | Partial; FunscriptEditor and queued notification coverage in both apps still need an explicit inventory and gap closure. |
| Handy ordering, script selection during setup, draining requests, and stop-before-dispose with fake HTTP transport | Archived timeout/shutdown verification covers draining, cancellation, deferred teardown, stop, and disposal. Archived command-ordering verification covers selected queued/in-flight replacement cases. | Partial; verify script-selection change during setup and confirm no duplicate/uncovered ordering paths remain. |
| Timeline zoom/scroll limits, deletion during drag, and keyboard routing | `NumericPositionEntryTests.cs` and related editor tests cover navigation, clamping, edits, and key behavior, including recent navigation work. | Partial; explicitly map zoom/scroll limits, deletion while dragging, and keyboard-routing cases; add uncovered tests. |
| Clean-checkout CI build/test gate | Workflow exists; archived production-readiness verification records a successful clean-checkout run (341/341) on a prior revision. | Existing gate is operational on recorded revision; obtain/record passing CI for the current intended revision and recheck trigger coverage. |

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
- Reused tests do not prove behavior beyond their asserted conditions. Record direct attach/detach transitions, both applications, script-selection races, and exact timeline edge cases individually as the matrix is audited.
- No real Handy hardware or external mpv service is required for the acceptance tests; simulated results must not be described as hardware certification.
