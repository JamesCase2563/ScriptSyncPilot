# Production Readiness Plan

**Status:** Active; adopted 2026-10-31. The previous plan is preserved in the [archive](../../archive/2026-10-production-readiness/PLAN.md).
**Goal:** Decide whether ScriptSyncPilot is ready for its documented initial support scope, and record the evidence and remaining limits.

This is a source-only, free MIT project maintained by an individual in Norway. This plan coordinates the release decision; implementation details remain in the component backlogs. It is not legal advice or a compliance determination. Keep distribution source-only unless the operating model is reassessed. Dated decisions and reviewed findings are kept in the append-only [decision log](DECISIONS.md); gate evidence and the final go/no-go record belong in `VERIFICATION.md`.

## Product baseline

HandySync targets Handy REST API v3 and HSSP, uploads funscript action data through Handy Hosting API v2, and requires an mpv session and internet access to Handy services. The vendor API requires firmware v4 or newer; control availability also depends on firmware status and device capabilities. The [support scope and limitations](SCOPE-AND-LIMITATIONS.md) and [tested environments and compatibility matrix](TESTED-ENVIRONMENTS.md) define the actual supported and tested baseline. Compatibility claims must not exceed that evidence.

Firmware installation, other device vendors and protocols, legacy API v2, local/offline output, and non-1x device playback are outside the initial-release scope. Automatic reconnect, session restoration, and expanded retry/backoff are also deferred. See the [HandySync backlog](../../HandySync/BACKLOG.md) for implementation work and the [product vision](../../VISION.md) for future scope.

## Recorded decisions

See [DECISIONS.md](DECISIONS.md) for dated decisions, reviewed findings, attributions, and their limits. The approved support scope and tested environments remain in [SCOPE-AND-LIMITATIONS.md](SCOPE-AND-LIMITATIONS.md) and [TESTED-ENVIRONMENTS.md](TESTED-ENVIRONMENTS.md), respectively.

## Release gates

Record evidence and unresolved items in `VERIFICATION.md` when verification begins. A gate is complete when its evidence is linked there; defects found during verification go to the relevant component backlog. The agreed reliability verification order is listed below and in `VERIFICATION.md` when that file is created.

| Gate | Minimum evidence for the initial release | Release blocker? |
| --- | --- | --- |
| Build and source release | CI builds and runs tests covering lifecycle, protocol handling, persistence, and failure paths from a clean checkout. Source acquisition, build/install, update, and return-to-known-good instructions are published. Version tags, release notes, and a process to withdraw or supersede a faulty source release are documented. | Yes |
| Supported scope | User-facing requirements match the approved support scope and tested matrix. Known limitations and external requirements, including mpv and internet access, are easy to find. | Yes |
| Reliability and data preservation | Verification covers timeouts and shutdown, visible/actionable failures, secret-free diagnostics, stale command ordering after disconnect/session replacement, firmware/capability gates, and preservation of existing files on failed saves or cancelled edits. Simulated failures/test doubles are sufficient where practical; additional physical devices are not required. | Yes |
| Privacy and support | Publish a support/reporting channel and ask users not to include personal data, connection keys, or private hosted-script URLs. Recheck actual data flows and publish only accurate, reviewed privacy information. No claim of legal compliance or non-applicability without an adequate basis. | Yes, for accurate user-facing notices and a usable reporting route; paid legal review is not a prerequisite under the current budget decision. |
| Release decision | List remaining known risks and limitations, confirm no known release-blocking defects, and record a dated go/no-go decision by the repository owner. | Yes |

### Initial-release reliability priorities

This is the agreed verification order, not evidence that verification is complete. Preserve existing behavior and change code only where verification identifies a defect in the supported baseline. Simulated failures and test doubles are sufficient where practical; additional physical devices are not required.

| Priority | Area / failure scenario | Minimum expectation |
| --- | --- | --- |
| 1 | Timeouts and shutdown: slow or unavailable services | Requests do not wait indefinitely; disconnect/shutdown finishes predictably and attempts to stop the device. Communication loss prevents guaranteeing a physical stop. Automated evidence recorded 2026-11-01 in the [timeout-shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md). |
| 2 | [Error reporting work](../../archive/2026-11-error-reporting/PLAN.md): failed requests or operations | Complete 2026-11-03; full suite passed 312/312. See [error-reporting verification](../../archive/2026-11-error-reporting/VERIFICATION.md). Failures are visible and actionable, do not falsely report success, and do not expose keys or private script URLs. |
| 3 | [Command ordering work](../command-ordering/PLAN.md): disconnect or session replacement during queued/in-flight work | Complete 2026-11-08; full solution suite passed 326/326. See [command-ordering verification](../command-ordering/VERIFICATION.md). Focused tests verify queued HSSP suppression and stale completion handling; waveform IPC seek replacement remains an explicit test limitation. Preserve serialization; no command-coalescing machinery is required. |
| 4 | [Firmware and capabilities work](../firmware-capabilities/PLAN.md): unsupported controls or missing/failed reads | Existing controls respect implemented firmware/capability gates; missing or failed reads are reported clearly. No broader hardware compatibility claim or new model support is required. |
| Deferred | Automatic recovery and expanded retries | Automatic reconnect, device-takeover restoration, session restoration and expanded retry/backoff are not initial-release requirements. The user must be able to disconnect and start a fresh connection. |
| Deferred | Additional diagnostics | No structured logging overhaul, drift metrics or detailed network statistics are required; preserve useful, secret-free diagnostics. |

File preservation is covered by the Reliability and data preservation gate: failed saves, invalid settings, and cancelled edits must preserve existing data.

## Open risks and follow-up

- **Handy upload retention:** Make one documented attempt to ask Handy for the uploaded CSV expiry/deletion period and which privacy-policy retention statements apply to this direct upload/HSSP flow. The API describes temporary URLs but does not give an exact retention period. If Handy does not clarify, record that as unresolved and do not promise a retention period. This does not need to block unrelated engineering verification.
- **Privacy/legal applicability:** GDPR and Norwegian/EEA applicability has not been determined. The earlier plan called for a full assessment of roles, lawful basis, privacy information, and related processes. This plan deliberately narrows the initial release task to verifying project-controlled data flows and support workflows, publishing only statements supported by evidence, and making no claim of compliance or non-applicability without an adequate basis. This downgrade reflects the current budget decision to decline paid legal review; it does not establish that no obligations apply. Reassess the scope if the operating model changes or new evidence warrants it.
- **Failure-path coverage:** Existing normal-path device evidence does not establish behavior during service outages, protocol failures, or loss of communication. The release gates above remain open until those cases are verified.

## Ownership and source of truth

The repository owner is accountable for issue and security-report triage, source releases, support expectations, and communicating breaking changes. Component implementation tasks remain in the [shared backlog](../../BACKLOG.md), [HandySync backlog](../../HandySync/BACKLOG.md), and [editor backlog](../../FunscriptEditor/BACKLOG.md). Preserve the contracts in the [system architecture](../../ARCHITECTURE.md). This plan replaces the coordination structure of the previous plan while preserving its dated decision history in [DECISIONS.md](DECISIONS.md), its superseded text in the [archive](../../archive/2026-10-production-readiness/PLAN.md), all approved support decisions, and the component backlogs.

## Go/no-go record

When the gates are reviewed, add to `VERIFICATION.md`:

- Decision and date.
- Evidence links for each release gate.
- Any unresolved risks and published limitations.
- Repository owner responsible for the decision.

Do not describe the project as production-ready before the release gates are met for the documented support promise.
