# Shared Backlog

Future and active work is tracked in [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues).
GitHub Issues are the source of truth for priorities, status, labels, epics, sub-issues, and implementation progress.

Project-specific features belong in the [HandySync backlog](HandySync/BACKLOG.md) or [editor backlog](FunscriptEditor/BACKLOG.md).
This file retains shared release boundaries and implemented baselines that are not duplicated in issue tracking.

## Initial-release scope classification

Reviewed against the [approved support scope](archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md).
These labels describe initial-release scope, not implementation completion or a change
to future feature priorities.

| Entry | Classification | Release boundary |
| --- | --- | --- |
| Automated Tests and CI | In scope | Verify existing supported workflows, lifecycle and failure paths using test doubles where practical |
| UI and Session Logic Boundaries | Deferred | Extract UI/session boundaries only for a concrete release-blocking defect or verification need; do not make an architecture rewrite a prerequisite |
| GUI Usability and Visual Polish | Deferred | Treat GUI usability and visual polish as a separate product-improvement effort; fix defects in existing supported workflows within the relevant release work |
| Privacy and Operational Guidance | In scope | Keep data-handling and troubleshooting guidance accurate, including supported launch paths, log locations, credential protection, and provider retention only when confirmed |
| Completed baselines | In scope | Preserve implemented behavior and existing verification evidence |

## Completed

- **Initial Common unit-test suite:** Funscript action edits, round-trips, lowercase JSON fields, metadata preservation, sorted loading, empty documents, invalid JSON, and CSV export in [the Common test project](../src/tests/ScriptSyncPilot.Common.Tests). Broader coverage and CI remain above.
- **Local credential/privacy baseline:** protected HandySync key persistence, sanitized hosted-script URL diagnostics, and settings/log/recovery guidance in [online security](../docs/online-security.md). Task closed by the maintainer on 2026-10-23; implementation, verification results, and limitations are recorded in the archived [credential-protection design](archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md).
