# Automated Tests and CI Plan

**Status:** Planned; existing suites and CI provide a baseline, but issue #21 remains open for broader targeted coverage and clean-checkout CI verification.
**Issue:** [#21 — Automated Tests and CI](https://github.com/JamesCase2563/ScriptSyncPilot/issues/21).
**Goal:** Establish focused, repeatable regression coverage and a reliable build/test gate for the projects that exist in this repository.

## Scope

- Reconcile issue #21 with the current solution, test projects, CI workflow, and archived verification records.
- Reuse existing tests for timeout/cancellation, command ordering, firmware/capability gates, error reporting, and shared Funscript behavior; do not duplicate completed scenarios.
- Add deterministic tests for remaining gaps identified by the issue:
  - mpv attachment failures, cancellation, observations, and disposal using a controlled local pipe server;
  - session replacement and stale queued notifications in HandySync and FunscriptEditor;
  - Handy ordering around script selection during setup, draining requests, and stop-before-dispose using fake HTTP transport;
  - timeline zoom/scroll limits, deletion during drag, and keyboard routing.
- Confirm clean-checkout CI restores, builds, and tests the existing solution on Windows.

Out of scope: creating the former controller plan's nonexistent projects, hardware-dependent tests, broad architecture changes, and duplicating completed work from archived plans.

## Existing baseline

- Solution: `src/ScriptSyncPilot.slnx`; current projects are Common, FunscriptEditor, HandySync, Common.Tests, and FunscriptEditor.Tests.
- Shared tests cover Funscript/action history, pattern generation/insertion, and mpv session behavior.
- FunscriptEditor.Tests references both app projects and contains the WinForms, Handy HTTP, timeout, settings, and lifecycle coverage.
- `.github/workflows/windows-release-verification.yml` restores, builds Release, and tests the solution on `windows-latest` with .NET 10. It runs for pull requests targeting `main` and `production-readiness` when `src/**` changes, and for pushes to `production-readiness`.
- Reusable completed evidence is archived in `dev/archive/2026-11-timeout-shutdown`, `dev/archive/2026-11-command-ordering`, and `dev/archive/2026-11-firmware-capabilities`, plus the completed error-reporting and production-readiness records.

## Milestones

### 1. Reconcile existing coverage

- [x] Map each issue scenario to current tests and archived evidence; record test boundaries and gaps in [VERIFICATION.md](VERIFICATION.md) (M1.1).
- [x] Confirm the CI trigger/build/test path covers the supported PR targets from a clean checkout; identify any trigger or workflow gaps without assuming that a workflow file alone proves a passing run (M1.2; the path-filter gap is recorded in [VERIFICATION.md](VERIFICATION.md)).
- [x] Keep scenarios already verified in archived work as reused evidence, with their limits stated (M1.3; see the scenario matrix and archived evidence links in [VERIFICATION.md](VERIFICATION.md)).

### 2. Fill focused test gaps

- [ ] Update the PR CI path filters to include workflow and root build/test configuration changes, not only `src/**`.
- [ ] Add/complete controlled local pipe-server coverage for mpv attach failure, cancellation, observable state changes, and disposal.
- [ ] Add/complete deterministic session-replacement tests for both applications, including suppression of stale queued notifications.
- [ ] Add/complete fake-transport tests for Handy setup ordering when script selection changes, draining in-flight requests, and stop-before-dispose.
- [ ] Add/complete timeline tests for zoom/scroll bounds, deletion during drag, and keyboard input routing.
- [ ] Fix only defects exposed by these tests, preserving current supported behavior and the existing project boundaries.

### 3. Verify and close

- [ ] Run the focused tests for changed scenarios and record exact commands/results in [VERIFICATION.md](VERIFICATION.md).
- [ ] Run the full solution Release build and test suite locally; record counts, environment, and limitations.
- [ ] Obtain a passing clean-checkout GitHub Actions run for the intended PR/source revision; record the workflow run and commit.
- [ ] Record any remaining uncovered or hardware-dependent behavior and link final evidence from issue #21.

## Acceptance criteria

- Every scenario in issue #21 is mapped to focused automated evidence or explicitly recorded as a remaining gap with rationale.
- Tests use controlled pipe/HTTP doubles where practical and do not require physical hardware or external services.
- Session replacement, stale queued notifications, setup ordering, request draining, and stop-before-dispose have deterministic assertions.
- Timeline limits, deletion-during-drag, and keyboard routing have regression coverage.
- A clean-checkout CI run restores dependencies, builds the current solution, and passes its test suite on the supported Windows/.NET environment.
- Evidence distinguishes synthetic/unit coverage from CI and hardware/manual observations; completed archived evidence is reused without overstating its scope.
- No project absent from the current solution is introduced as a prerequisite.
