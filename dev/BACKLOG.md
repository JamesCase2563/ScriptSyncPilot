# Shared Backlog

## Table of Contents

- [Automated Tests and CI](#automated-tests-and-ci)
- [UI and Session Logic Boundaries](#ui-and-session-logic-boundaries)
- [GUI Usability and Visual Polish](#gui-usability-and-visual-polish)
- [Privacy and Operational Guidance](#privacy-and-operational-guidance)
- [Completed](#completed)

Project-specific features belong in the [HandySync backlog](HandySync/BACKLOG.md) or [editor backlog](FunscriptEditor/BACKLOG.md).

Active work is tracked in the linked GitHub issues. The [Completed](#completed) section records implemented baselines without implying that broader work is complete.

## Initial-release scope classification

Reviewed against the [approved support scope](archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
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

Tracked in GitHub issue [#21](https://github.com/JamesCase2563/ScriptSyncPilot/issues/21).

## UI and Session Logic Boundaries

Tracked in GitHub issue [#22](https://github.com/JamesCase2563/ScriptSyncPilot/issues/22).

## GUI Usability and Visual Polish

Tracked in GitHub issue [#23](https://github.com/JamesCase2563/ScriptSyncPilot/issues/23).

## Privacy and Operational Guidance

Tracked in GitHub issue [#24](https://github.com/JamesCase2563/ScriptSyncPilot/issues/24).

## Completed

- **Initial Common unit-test suite:** Funscript action edits, round-trips, lowercase JSON fields, metadata preservation, sorted loading, empty documents, invalid JSON, and CSV export in [the Common test project](../src/tests/ScriptSyncPilot.Common.Tests). Broader coverage and CI remain above.
- **Local credential/privacy baseline:** protected HandySync key persistence, sanitized hosted-script URL diagnostics, and settings/log/recovery guidance in [online security](../docs/online-security.md). Task closed by the maintainer on 2026-10-23; implementation, verification results, and limitations are recorded in the archived [credential-protection design](archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md).
