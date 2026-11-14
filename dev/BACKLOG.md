# Shared Backlog

## Table of Contents

- [Automated Tests and CI](#automated-tests-and-ci)
- [UI and Session Logic Boundaries](#ui-and-session-logic-boundaries)
- [GUI Usability and Visual Polish](#gui-usability-and-visual-polish)
- [Privacy and Operational Guidance](#privacy-and-operational-guidance)
- [Completed](#completed)

Project-specific features belong in the [HandySync backlog](HandySync/BACKLOG.md) or [editor backlog](FunscriptEditor/BACKLOG.md).

Active entries describe remaining work. Implemented baselines are summarized under [Completed](#completed); partial delivery does not mark the broader item complete.

## Initial-release scope classification

Reviewed against the [approved support scope](work/production-readiness/SCOPE-AND-LIMITATIONS.md).
These labels describe initial-release scope, not implementation completion or a change
to future feature priorities.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| Automated Tests and CI | In scope | Verify existing supported workflows, lifecycle and failure paths; use test doubles where practical |
| UI and Session Logic Boundaries | Deferred | Extract only what a concrete release-blocking defect or verification need requires; no architecture rewrite prerequisite |
| GUI Usability and Visual Polish | Deferred | Separate product-improvement effort; fixes to defects in existing supported workflows remain in scope |
| Privacy and Operational Guidance | In scope | Accurate data handling, support/build/troubleshooting guidance and disclosure of unconfirmed provider retention; do not invent provider guarantees |
| Completed baselines | In scope | Preserve implemented behavior and existing verification evidence |

## Automated Tests and CI

**Priority:** High. **Status:** Broader coverage and CI remain open; initial Common suite implemented (see [Completed](#completed)). Command-ordering verification completed 2026-11-08 ([plan](work/command-ordering/PLAN.md), [verification](work/command-ordering/VERIFICATION.md)); firmware/capability verification completed 2026-11-13 ([plan](archive/2026-11-firmware-capabilities/PLAN.md), [verification](archive/2026-11-firmware-capabilities/VERIFICATION.md)). The completed [error-reporting work](archive/2026-11-error-reporting/PLAN.md) is archived. These efforts do not by themselves establish clean-checkout CI coverage.

Establish focused tests and CI build/test gates for the existing projects, preserving the useful testing goals of the former controller plan without creating its nonexistent projects.

- mpv attachment failures, cancellation, observations, and disposal using a controlled local pipe server.
- Session replacement and stale queued notifications in both applications; HandySync release-gate ordering is tracked in [command-ordering](work/command-ordering/PLAN.md).
- Handy ordering, script selection during setup, draining requests, and stop-before-dispose with fake HTTP transport rather than hardware; existing timeout/shutdown evidence is archived in [its verification record](archive/2026-11-timeout-shutdown/VERIFICATION.md).
- Safe, truthful failure reporting for supported operations was verified in the [archived error-reporting work item](archive/2026-11-error-reporting/PLAN.md); this does not replace the broader test matrix or CI requirement.
- Timeline zoom/scroll limits, deletion during drag, and keyboard routing.

## UI and Session Logic Boundaries

**Priority:** Medium. **Status:** Proposed.

Extract session/device orchestration from forms where this reduces lifecycle complexity or improves testing. Keep UI updates on the UI thread and services' ownership/cancellation explicit. Common should contain genuinely shared facilities, not become a catch-all controller.

## GUI Usability and Visual Polish

**Priority:** To be decided. **Status:** Proposed; separate from production-readiness work.

Evaluate the FunscriptEditor and HandySync interfaces as a distinct product-improvement effort. Identify user workflows and usability friction before proposing visual redesign or framework changes. Consider consistency, accessibility, keyboard navigation, error recovery, onboarding, and high-DPI behavior across both applications. Define user-validated goals and testable acceptance criteria; prefer focused improvements over a wholesale rewrite without evidence.

## Privacy and Operational Guidance

**Priority:** Medium. **Status:** Partially implemented; remaining guidance gaps and provider retention clarification remain.

Review and fill remaining quickstart/troubleshooting gaps for launchers, mpv pipes, device firmware, connection failures, and log locations rather than duplicating existing guidance. Local credential protection, script-URL diagnostics, and related user documentation are complete; see the [archived credential-protection plan](archive/2026-10-protect-credentials-and-script-urls/PLAN.md). The upload/setup failure case is automatically verified but was not exercised manually; preserve that distinction.

Clarify provider-side script retention and URL expiry when Handy replies to the [sent inquiry](work/production-readiness/HANDY-DATA-RETENTION-EMAIL.md), then update disclosures with confirmed facts. Diagnostics must not expose connection or application keys. Do not introduce remote APIs merely to satisfy the superseded controller plan.

## Completed

- **Initial Common unit-test suite:** Funscript action edits, round-trips, lowercase JSON fields, metadata preservation, sorted loading, empty documents, invalid JSON, and CSV export in [the Common test project](../src/tests/ScriptSyncPilot.Common.Tests). Broader coverage and CI remain above.
- **Local credential/privacy baseline:** protected HandySync key persistence, sanitized hosted-script URL diagnostics, and settings/log/recovery guidance in [online security](../docs/online-security.md). Task closed by the maintainer on 2026-10-23; implementation, verification results, and limitations are recorded in the archived [credential-protection design](archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md).
