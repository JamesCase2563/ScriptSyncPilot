# Error Reporting Design

**Status:** Approved by the maintainer on 2026-11-02; investigation and verification not started.
**Affected projects:** HandySync primarily; Common or FunscriptEditor only if a shared reporting defect is shown to affect the release gate.
**Production-readiness priority:** 2, error reporting.

## Goal

Verify that failures in the supported HandySync workflows are presented as failures, give users a useful next step where one is known, and keep credentials, hosted-script URLs, response bodies, and unsafe transport exception text out of user-visible diagnostics. Fix only defects found against this goal.

This is a verification work item, not a logging redesign. Preserve useful existing diagnostics and the current UI flow unless evidence shows it misleads users or hides an actionable failure.

## Existing baseline and inputs

- The credential and hosted-script URL protection work is complete; see the archived [design](../../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md).
- Request-budget and shutdown failures have typed timeout handling and safe stop-failure reporting; see [timeout-shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md).
- The production-readiness release gate requires visible/actionable failures, no false success, and secret-free diagnostics. See the [coordinating plan](../production-readiness/PLAN.md).
- Relevant implementation includes HandySync `MainForm` and `HandyApiClient`; inspect current code and existing tests before proposing a code change.

## Proposed approach

1. Inventory failure paths for connect/setup, capability and device reads, upload, play/pause/seek/synctime, offset/stroke controls, disconnect/stop, and application close.
2. For each path, record the observable outcome: UI status/dialog/log, whether dependent work proceeds, whether readiness is cleared when state is unknown, and whether the text contains sensitive or arbitrary exception data.
3. Classify messages as actionable, safely descriptive but not actionable, misleading/false-success, hidden, or sensitive. Do not infer vendor-side causes from generic HTTP/network failures.
4. Add or adjust only the minimum safe message/state behavior needed. Keep timeout classification distinct from caller cancellation and avoid logging raw response bodies or arbitrary transport exception strings.
5. Verify representative failures with fake transports and UI/lifecycle tests; reuse archived credential/URL evidence where it directly applies.

## Constraints

- Never display or log connection keys, hosted-script URLs, private media paths where not needed, raw response bodies, or unreviewed exception text.
- A failed operation must not be described as successful. When the remote state cannot be known, say so plainly and prevent dependent commands from assuming success.
- Do not add retries, recovery state machines, structured logging infrastructure, telemetry, or new support channels here.
- Do not change normal successful behavior solely to make logs more verbose.

## Open questions for investigation

- Which UI messages, status labels, and logs are user-facing in the supported flows, and which are only developer diagnostics?
- Are any exception messages passed through from APIs that can contain credentials, URLs, response content, or local paths?
- Do any failures leave a stale ready/connected state or allow follow-on commands?
- Which current messages have a reliable next step, and where would inventing one exceed available evidence?

## Completion criteria

- Failure-path inventory covers the supported operation groups above.
- Relevant failure states are visible, do not imply success, and are safe to expose.
- Automated tests cover any changed behavior; test commands and outcomes are recorded in [VERIFICATION.md](VERIFICATION.md).
- Findings and any remaining limitations are linked from the production-readiness plan. No broad logging overhaul is required.
