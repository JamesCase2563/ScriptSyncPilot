# Production Readiness

> Archived on 2026-10-31 and superseded by the active [production-readiness plan](../2026-11-production-readiness/PLAN.md). Dated decisions are retained in [DECISIONS.md](../2026-11-production-readiness/DECISIONS.md).

**Status:** In progress; core product and operating decisions are recorded, with verification and implementation work remaining.
**Affected projects:** Repository/operations, FunscriptEditor, HandySync, Common, documentation.  
**Goal:** Establish the maintainer, legal, product-scope, security, reliability, and release foundations needed before describing ScriptSyncPilot as production-ready.

This is a coordinating work item, not a commitment to implement every possible feature. It records release decisions and sequences work that remains in the component backlogs. Paid legal review is outside the current budget and is not a project release prerequisite; legal and regulatory applicability remains unresolved where not established. This plan is not legal advice or a compliance determination.

## Outcomes

- Maintainer responsibilities for repository access, releases, and support are documented.
- Licensing, attribution, user-facing notices, privacy/data handling, and security contact/reporting have been reviewed and published as appropriate.
- Supported use cases, protocols, devices/firmware expectations, and explicit exclusions are documented.
- Existing reliability and privacy gaps are prioritized, implemented, and verified against measurable release criteria.
- A release checklist and support/maintenance expectations exist.

## Confirmed Operating Facts

- Maintained/published from Norway by an individual; intended for worldwide use.
- Free, MIT-licensed, source-only distribution; no commercial activity and no service provided. Keep releases source-only unless the scope is reassessed.
- **Preliminary CRA position:** Based on the European Commission's [CRA summary](https://digital-strategy.ec.europa.eu/en/policies/cra-summary) and [open-source guidance](https://digital-strategy.ec.europa.eu/en/policies/cra-open-source), the current free, non-monetised FOSS distribution appears not to be made available on the market in the course of commercial activity and therefore appears outside the CRA. The Commission describes open-source software stewards as legal persons, which does not match the stated individual maintainer. This is a reasoned preliminary position, not legal advice or a formal determination; reassess if monetisation, commercial distribution/support, a steward arrangement, or other relevant facts change.
- The maintainer reports not receiving or controlling personal data. Verify this against telemetry, diagnostics, support workflows, and actual data flows before publishing privacy statements.
- Firmware updates are outside ScriptSyncPilot's scope; provide compatibility information and refer users to official manufacturer instructions.

## Open Decisions

- **Legal applicability:** Whether GDPR, Norwegian/EEA rules, or other obligations apply to the verified operating model, and what actions/notices are required. Record the limits of the available assessment; do not claim compliance or non-applicability without an adequate basis. The maintainer declined paid legal review on 2026-10-24 due to cost; this does not establish that no obligations apply.

## Scope

### Current Product and Protocol Baseline

The following describes the current implementation and documented vendor requirements; it is not a certification or guarantee of compatibility with every model:

- HandySync targets Handy REST API v3 and uses HSSP for synchronized script playback. Scripts are uploaded as CSV through Handy Hosting API v2 and referenced by the returned URL.
- Handy's API v3 specification requires firmware v4 or newer. Offset/stroke controls additionally require firmware v4+ with `fw_status` other than update-required and the relevant slider capability.
- HandySync requires an mpv session and internet access to Handy services. Automatic reconnection/recovery is not fully implemented; see the [HandySync backlog](../../HandySync/BACKLOG.md).
- Firmware update/install flows, other device vendors, alternate Handy protocols, legacy API v2, local/offline device output, and non-1x device playback are outside the current product scope.
- Tested Handy model/firmware combinations and workflow results are recorded in the [tested environments and compatibility matrix](../2026-11-production-readiness/TESTED-ENVIRONMENTS.md). Coverage is limited to those records; do not make broader compatibility claims.

### In scope

- Repository access, maintainer responsibilities, and release operations.
- License and third-party notice selection; user-facing disclaimer and privacy documentation, as applicable.
- A data inventory covering connection keys, settings, logs, media/script paths, network requests, retention, and user control.
- Legal/regulatory applicability assessment and a process for updating compliance documentation.
- Protocol/device/firmware compatibility policy and explicit product boundaries.
- Reliability, diagnostics, testing, privacy/security, packaging, and release-readiness work tied to the agreed support promise.

### Not in scope unless separately approved

- Firmware update/install flows or distribution of vendor firmware across all ScriptSyncPilot applications. Apps may report compatibility requirements; documentation may link to the manufacturer's official instructions, but users perform updates through official vendor channels.
- Adding protocols, devices, backends, remote APIs, or platforms solely to broaden feature count.
- Treating a disclaimer as a substitute for safety, privacy, or legal obligations.
- Promising regulatory compliance or device compatibility that has not been independently verified.

## Milestones

### 1. Establish legal, licensing, and privacy foundations

- [x] Record the intended distribution facts: Norway to worldwide; free source distribution under MIT.
- [x] Record CRA-related operating facts supplied by the maintainer: source-only distribution, no commercial activity, individual publisher, and no service provided. Keep distribution source-only; do not add binary releases without reassessing the scope.
- [x] Record that the maintainer states they do not receive or control personal data.
- [x] Inspect project manifests for declared dependencies: the application projects use in-repository project references and have no explicit `PackageReference` entries.
- [x] Complete the current source/dependency/asset attribution inventory; no application runtime NuGet packages or third-party binary assets are bundled. Test-only package graph and its license/notice details are recorded in [third-party components inventory](../../../docs/third-party-components.md). Recheck when dependencies, assets, or distribution model change.
- [x] Select MIT as the repository license.
- [x] Confirmed copyright holder/year with the maintainer and added the MIT license as `Copyright (c) 2026 James Case`.
- [x] Contributions are accepted under the MIT License, each contributor retains copyright in their contribution, and no separate CLA is required; documented the contribution process in [CONTRIBUTING.md](../../../CONTRIBUTING.md).
- [x] Reviewed third-party notice requirements against the [third-party components inventory](../../../docs/third-party-components.md); no third-party runtime code/assets are bundled in the current source-only distribution. Test-only package notices are documented in the inventory.
- [x] Document the implemented HandySync flow: the Handy REST requests include the connection key and application identifier in headers; the app uploads a CSV containing only the funscript action timestamps/positions to Handy's Hosting API, receives a temporary download URL, then sends that URL and playback/synchronization timing to HSSP. Video and app settings remain local. See [HandyApiClient](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs) and [Funscript.ToCsv](../../../src/ScriptSyncPilot.Common/Funscript.cs).
- [x] Verify the implemented request/data flow against the source and current official API/privacy documents. The [Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml) describes uploads as temporary URLs but does not specify their exact expiry/deletion period. The [Handy privacy policy](https://www.thehandy.com/privacy-policy/) describes connection key/IP, device state, third-party provider identification, and script download URL processing for online third-party synchronization; it says session data is deleted within one hour after logoff and the device retains the script until overwritten. That policy does not clearly establish retention for the separately uploaded CSV or the exact applicability to this direct upload/HSSP flow.
- [ ] Ask Handy to clarify the uploaded CSV's expiry/deletion period and which privacy-policy retention statements apply to ScriptSyncPilot's direct upload/HSSP flow; do not infer a retention guarantee from the session-data statement.
- [x] Inspect the application projects and manifests for telemetry/crash-reporting code or SDKs: none were found in the current source or explicit package references. This does not establish whether Handy independently collects data or determine what a user might submit through an external support channel.
- [ ] Document the support/reporting channel and instruct users not to include personal data, connection keys, or private hosted-script URLs in reports; reassess if a project-controlled collection channel is introduced.
- [x] Inspect credential handling and logs: HandySync persists the connection key in its local settings JSON, and logs the temporary hosted-script URL. The connection key was not found in the inspected explicit log messages.
- [x] Implement credential and temporary script-URL protection: protected local key storage, URL omission/redaction from diagnostics, safe exception reporting, and local settings/log guidance. Task completed and archived by maintainer confirmation on 2026-10-23; see the [design](../../archive/2026-10-protect-credentials-and-script-urls/DESIGN.md) and [verification evidence and limitations](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md). Provider-side retention clarification and support-report guidance remain separate items above.
- [x] Compare the stated source-only, non-commercial FOSS model with the European Commission's [CRA summary](https://digital-strategy.ec.europa.eu/en/policies/cra-summary) and [open-source guidance](https://digital-strategy.ec.europa.eu/en/policies/cra-open-source): preliminary position is that the current model appears outside CRA scope, and the maintainer does not meet the stated legal-person condition for an open-source software steward. Reassess if monetisation, commercial distribution/support, a steward arrangement, or other relevant facts change; this is not a formal legal opinion.
- [ ] Assess GDPR and Norwegian/EEA data-protection obligations based on the maintainer's role, any personal data the project itself receives, the app's third-party data flows, and the worldwide audience. Determine whether privacy information, a lawful-basis/role analysis, contact point, or other process is required.
- **Deferred (budget; maintainer decision 2026-10-24):** Paid legal review of additional obligations, including copyright/dependency notices, product liability or safety expectations for software that controls a physical device, security vulnerability handling, and consumer-facing claims. Not a release prerequisite or a completed legal assessment; applicability remains unresolved, and disclaimers do not replace applicable duties.
- [ ] Publish only reviewed, accurate disclaimers, privacy information, security reporting instructions, and support contact details.

External service references for the data-flow/privacy review: [Handyverse](https://handyverse.com/#/), [The Handy privacy policy](https://www.thehandy.com/privacy-policy/), [Handy API v3 docs](https://www.handyfeeling.com/api/handy-rest/v3/docs/), and the [Handy Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml). The privacy policy and API docs provide useful facts but do not by themselves establish the project's legal role or the upload endpoint's exact data-retention period.

### 2. Freeze initial product and protocol boundaries

- [x] Define supported applications, operating systems, device models, firmware versions, API/protocol modes, and required external software. See the [approved support scope](../2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md) and [tested environments and compatibility matrix](../2026-11-production-readiness/TESTED-ENVIRONMENTS.md); coverage is limited to the recorded combinations.
- [x] Approve the [initial support scope and known limitations](../2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md). Maintainer approved on 2026-10-24, including limited automatic recovery for the first release; tested version/model coverage is recorded in ENV-001, while release verification remains outstanding.
- [x] Keep firmware updating out of scope across the entire application: apps may report compatibility, and documentation refers users to official manufacturer instructions; ScriptSyncPilot does not download, install, distribute, or initiate firmware updates.
- [x] Inventory and verify the Handy models/firmware combinations actually tested; maintain the compatibility and protocol revalidation matrix. [ENV-001](../2026-11-production-readiness/TESTED-ENVIRONMENTS.md#env-001) records two H01 devices (model number 0, variant 0), firmware `4.3.0+dcee7e58` and `4.2.2+44874492`, and successful workflows confirmed by the maintainer on 2026-10-24; connection/setup is additionally corroborated by logs.
- [x] Document the support boundary for unsupported firmware, protocol errors, outages, device takeover, synchronization loss, and unavailable internet/service in [known limitations](../2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md#known-limitations). Unsupported devices/firmware are untested and not guaranteed to work; acquiring unsupported hardware is not required. Normal-path tests do not validate failure/recovery scenarios, and communication loss prevents guaranteeing a physical stop. Implementation and simulated failure-path verification remain under milestone 3.
- [x] Maintain a [protocol compatibility matrix and version-change/revalidation checklist](../2026-11-production-readiness/TESTED-ENVIRONMENTS.md#compatibility-and-protocol-matrix). Future clean-release and failure-path verification remain separate gates.
- [x] Review product vision and backlog proposals against these boundaries; label items in-scope, deferred, experimental, or explicitly excluded. Initial-release classifications recorded in the [vision](../../VISION.md#initial-release-classification), [shared backlog](../../BACKLOG.md#initial-release-scope-classification), [HandySync backlog](../../HandySync/BACKLOG.md#initial-release-scope-classification) and [editor backlog](../../FunscriptEditor/BACKLOG.md#initial-release-scope-classification). Existing feature priorities remain future-work priorities, not release commitments.

### 3. Stabilize software against the support promise

- [x] Assign accountability to the GitHub repository owner for issue and security-report triage, publishing releases, setting any older-version support policy, and communicating breaking changes.
- [x] Prioritize the existing recovery, timeout/retry, command coalescing, diagnostics, and capability-validation work against agreed failure scenarios. Maintainer approved the [initial-release reliability priorities](#initial-release-reliability-priorities) on 2026-10-24; verification and any necessary defect fixes remain separate tasks.
- [ ] Establish CI build/test gates and required coverage for lifecycle, protocol handling, persistence, and failure paths.
- [ ] Verify reliable error reporting: failures are visible and actionable, do not falsely report success, and do not disclose credentials or private script URLs. Reuse the archived credential-protection evidence above and identify remaining gaps.
- [ ] Verify file preservation: failed saves, invalid settings, and cancelled edits preserve existing data. Reuse settings-preservation evidence and verify editor file operations separately.
- [ ] Verify stop/shutdown behavior: device-stop attempts, request ordering, cancellation, and resource cleanup are correct and shutdown finishes predictably. Document that an unreachable device/network prevents guaranteeing a physical stop.
- [ ] Verify remaining privacy/security controls and recovery behavior against the agreed support scope, using test doubles where physical hardware is unnecessary.
- [ ] Document how users obtain source, build/install it, update to a later source revision, and return to a prior known-good revision; include troubleshooting guidance for external tools and device connectivity.
- [ ] Define version tags and release notes for source releases, plus how to withdraw or supersede a faulty release. Do not plan binary artifact signing/distribution unless the source-only scope changes.
- [ ] Set measurable release acceptance criteria: required CI build and tests pass from a clean checkout; documented supported environment/protocol requirements match the tested matrix; no known release-blocking defects remain; privacy/security and troubleshooting documentation is reviewed; and limitations are published.

#### Initial-release reliability priorities

This is the agreed verification order, not a commitment to new features or evidence
that verification is complete. Preserve existing behavior; change code only where
verification identifies a defect in the supported baseline. Use simulated failures
and test doubles where practical; additional physical devices are not required.

| Priority | Area / failure scenario | Minimum expectation |
| --- | --- | --- |
| 1 | Timeouts and shutdown: slow or unavailable services | Requests do not wait indefinitely; disconnect/shutdown finishes predictably and attempts to stop the device. Communication loss prevents guaranteeing a physical stop. |
| 2 | Error reporting: failed requests or operations | Failures are visible and actionable, do not falsely report success, and do not expose keys or private script URLs. |
| 3 | Command ordering: disconnect or session replacement during queued/in-flight work | No stale play/seek commands are issued after disconnect or session replacement; preserve existing serialization and verify safe handling of in-flight requests. New command-coalescing machinery is not required. |
| 4 | Firmware and capabilities: unsupported controls or missing/failed reads | Existing controls respect implemented firmware/capability gates; missing or failed reads are reported clearly. No broader hardware compatibility claim or new model support is required. |
| Deferred | Automatic recovery and expanded retries | Automatic reconnect, device-takeover restoration, session restoration and expanded retry/backoff are not initial-release requirements. The user must be able to disconnect and start a fresh connection. |
| Deferred | Additional diagnostics | No structured logging overhaul, drift metrics or detailed network statistics are required; preserve useful, secret-free diagnostics. |

### 4. Confirm release readiness

- [ ] Run the release checklist, resolve blockers, document known limitations, and decide whether the product is ready for the stated support promise.

## Backlog Overlap and Ownership

Keep implementation details in the existing component backlogs. This plan owns cross-cutting decisions, sequencing, and release gates; it does not duplicate backlog specifications.

| Production-readiness area | Existing backlog/document | Relationship |
| --- | --- | --- |
| CI, automated tests, privacy/operational guidance | [Shared backlog](../../BACKLOG.md) | Direct overlap. Prioritize these items under milestone 4 and add release acceptance criteria; do not copy their test lists here. |
| Session/UI separation | [Shared backlog](../../BACKLOG.md) | Related engineering quality work, but only extract boundaries where reliability or testability warrants it. |
| Device recovery, timeouts/retries, coalescing, events, diagnostics | [HandySync backlog](../../HandySync/BACKLOG.md) | Direct overlap with reliability and support promises. Select and sequence specific entries after protocol support and recovery expectations are defined. |
| Firmware and capabilities | [HandySync backlog](../../HandySync/BACKLOG.md) | Existing partial capability/firmware checks overlap with compatibility policy. Firmware updates are explicitly out of scope across the application; user documentation points to official vendor instructions. |
| Playback speed synchronization and local output | [HandySync backlog](../../HandySync/BACKLOG.md) | Non-1x device playback and local output are excluded from initial-release scope; existing non-1x stop/suppress behavior remains in scope. |
| Product boundaries and possible extensions | [Product vision](../../VISION.md) | Initial-release classifications reconciled with approved scope in milestone 2; future extensions are not release commitments. |
| Current component responsibilities and contracts | [System architecture](../../ARCHITECTURE.md) | Preserve these ownership and lifecycle contracts while stabilizing or changing features. |

## Release Readiness Gate

Do not call the software production-ready until the team can answer and verify:

- Who owns and maintains the software, and where are support/security reports handled?
- What license applies, what third-party notices are required, and what user-facing legal/privacy information has been reviewed?
- What information leaves the user's machine, where credentials are stored, and what appears in logs?
- Which operating systems, devices, firmware, protocols, and external dependencies are supported, and which exact combinations have been verified?
- How does the software behave on network/service loss, invalid credentials, unsupported firmware, protocol changes, and shutdown?
- Can CI build and test the release, are known limitations documented, and can a bad release be withdrawn or superseded?

Record evidence, unresolved risks, and a go/no-go decision in a `VERIFICATION.md` when implementation begins.

## Status Notes

- No legal/compliance review has been performed by this work item.
- No firmware updater or protocol support expansion is authorized by this plan.
- The existing backlog items remain the implementation source of truth until explicitly selected and tracked here.
