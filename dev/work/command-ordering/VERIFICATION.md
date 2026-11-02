# Command Ordering Verification

**Status:** Not started. Existing related evidence is referenced below; no new checks are claimed.

## Scope and result

Production-readiness priority 3: suppress stale commands after disconnect or session replacement while preserving serialization and safe in-flight handling. See [design](DESIGN.md), [plan](PLAN.md), [production-readiness plan](../production-readiness/PLAN.md), and [HandySync architecture](../../HandySync/ARCHITECTURE.md).

## Existing evidence to reuse

- [Timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md): disconnect rejects writes, cancels pending debounced writes and in-flight play, suppresses queued seek, prevents late stale success, and handles deferred drain/stop teardown.
- This evidence does not by itself establish every mpv session replacement race; characterize coverage before treating priority 3 as complete.

## Scenario matrix

| Transition/scenario | Result | Evidence/notes |
| --- | --- | --- |
| Disconnect with queued or in-flight command | Prior evidence | Link exact tests/commands after review. |
| Handy client replacement | Not assessed | |
| mpv session replacement with queued/in-flight command | Not assessed | |
| Late completion after target replacement | Partial prior evidence | Confirm whether tests cover both target identities. |
| Command serialization and cleanup | Prior evidence | Confirm applicable tests and current implementation. |

## Runs

| Date | Revision/environment | Command or check | Result |
| --- | --- | --- | --- |
| — | — | No verification run for this work item yet | — |

## Findings and closure

- Confirmed defects: none recorded yet; characterization has not started.
- Changes and regression tests: none recorded.
- Limitations: pending characterization.
- Release-gate conclusion: open; this work item is not verified complete.
