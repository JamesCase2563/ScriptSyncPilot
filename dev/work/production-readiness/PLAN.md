# Production Readiness

**Status:** Proposed; discovery and decisions not started.  
**Affected projects:** Repository/operations, FunscriptEditor, HandySync, Common, documentation.  
**Goal:** Establish the ownership, legal, product-scope, security, reliability, and release foundations needed before describing ScriptSyncPilot as production-ready.

This is a coordinating work item, not a commitment to implement every possible feature. It records release decisions and sequences work that remains in the component backlogs. Legal and regulatory applicability must be confirmed with qualified counsel; this plan is not legal advice or a compliance determination.

## Outcomes

- The repository has a confirmed long-term owner and maintainers, with access and release responsibilities documented.
- Licensing, attribution, user-facing notices, privacy/data handling, and security contact/reporting have been reviewed and published as appropriate.
- Supported use cases, protocols, devices/firmware expectations, and explicit exclusions are documented.
- Existing reliability and privacy gaps are prioritized, implemented, and verified against measurable release criteria.
- A professional GUI review is based on agreed user needs and follows, rather than substitutes for, product and protocol decisions.
- A release checklist and support/maintenance expectations exist.

## Open Decisions

- **Legal scope:** The project is maintained/published from Norway by an individual, is free and MIT-licensed, distributes source only, has no commercial activity, provides no service, and is intended for worldwide use. The maintainer states they do not receive or control personal data. Assess any remaining Norwegian/EEA or other applicable legal obligations against these facts; do not claim compliance or non-applicability solely from this summary.
- **Firmware policy:** Should firmware update functionality be supported by ScriptSyncPilot? **Proposed default:** do not implement firmware update/install flows; detect and explain relevant compatibility requirements, then link users to the device manufacturer's official update instructions. Revisit only if a concrete user need and safe vendor-supported interface justify owning this capability.
- **Protocol support:** Which device models, firmware versions, Handy API/HSSP modes, and failure/recovery behaviors are supported, experimental, or unsupported? Which protocol/API version changes require revalidation?
- **Release/support policy:** Who triages issues and security reports, publishes releases, supports older versions, and communicates breaking changes?
- **GUI scope:** Which user workflows and accessibility/usability problems justify a UI redesign or focused polish? Avoid a framework rewrite without evidence.

## Scope

### In scope

- Repository ownership/maintainer transfer planning and post-transfer verification.
- License and third-party notice selection; user-facing disclaimer and privacy documentation, as applicable.
- A data inventory covering connection keys, settings, logs, media/script paths, network requests, retention, and user control.
- Legal/regulatory applicability assessment and a process for updating compliance documentation.
- Protocol/device/firmware compatibility policy and explicit product boundaries.
- Reliability, diagnostics, testing, privacy/security, packaging, and release-readiness work tied to the agreed support promise.
- Evidence-based GUI usability improvements after core workflows and supported behavior are settled.

### Not in scope unless separately approved

- Building a firmware updater or distributing vendor firmware.
- Adding protocols, devices, backends, remote APIs, or platforms solely to broaden feature count.
- Treating a disclaimer as a substitute for safety, privacy, or legal obligations.
- A wholesale UI framework rewrite without user research, acceptance criteria, and a migration case.
- Promising regulatory compliance or device compatibility that has not been independently verified.

## Milestones

### 1. Confirm ownership and operating model

- [x] Select the destination GitHub account/organization: `JamesCase2563`.
- [x] Confirm the recipient can administer the repository.
- [ ] Confirm recipient acceptance and transfer readiness.
- [ ] Inventory repository settings and integrations that may need transfer/reconfiguration: maintainers, branch protections, Actions secrets/variables, Pages, releases, issue/PR templates, security reporting, and any external links.
- [ ] Transfer ownership using GitHub's supported process; verify redirects, clone/fetch, permissions, CI, releases, and links afterward.
- [ ] Record maintainers, release authority, security contact, and issue-triage expectations.

### 2. Establish legal, licensing, and privacy foundations

- [x] Record the intended distribution facts: Norway to worldwide; free source distribution under MIT.
- [x] Record CRA-related operating facts supplied by the maintainer: source-only distribution, no commercial activity, individual publisher, and no service provided. Keep distribution source-only; do not add binary releases without reassessing the scope.
- [x] Record that the maintainer states they do not receive or control personal data.
- [ ] Inventory shipped code, dependencies, assets, and existing attribution obligations.
- [x] Select MIT as the repository license.
- [ ] Add the MIT license text with the appropriate copyright holder/year; include required third-party notices and contribution terms/process if contributions are accepted.
- [x] Document the implemented HandySync flow: the Handy REST requests include the connection key and application identifier in headers; the app uploads a CSV containing only the funscript action timestamps/positions to Handy's Hosting API, receives a temporary download URL, then sends that URL and playback/synchronization timing to HSSP. Video and app settings remain local. See [HandyApiClient](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs) and [Funscript.ToCsv](../../../src/ScriptSyncPilot.Common/Funscript.cs).
- [ ] Verify this data-flow description against runtime behavior and current official docs. The [Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml) describes uploads as temporary URLs but does not specify their exact expiry/deletion period. The [Handy privacy policy](https://www.thehandy.com/privacy-policy/) says that for online third-party synchronization Handy processes the connection key/IP, device state, third-party provider identification, and script download URL; it says session data is deleted within one hour after logoff and the device retains the script in persistent memory until overwritten. Confirm which policy statements apply to ScriptSyncPilot's direct upload/HSSP flow; do not infer a retention guarantee for the uploaded CSV from the policy's session-data statement.
- [ ] Verify that the project does not collect telemetry, crash reports, support diagnostics, or user data, and document this accurately. Distinguish maintainer access/control from data the app sends directly to Handy services.
- [ ] Review secret handling and diagnostic logging; document privacy controls and avoid exposing credentials in logs or support requests.
- [ ] Verify the intended source-only, non-commercial, individual-publisher, no-service operating model and assess its treatment under the CRA, including whether any open-source software steward role or other obligation applies. Reassess before introducing binaries, paid support, sponsorship, or other commercial activity.
- [ ] Assess GDPR and Norwegian/EEA data-protection obligations based on the maintainer's role, any personal data the project itself receives, the app's third-party data flows, and the worldwide audience. Determine whether privacy information, a lawful-basis/role analysis, contact point, or other process is required.
- [ ] Ask qualified counsel whether any additional obligations apply to this free software, including copyright/dependency notices, product liability or safety expectations for software that controls a physical device, security vulnerability handling, and any consumer-facing claims. Disclaimers do not replace applicable duties.
- [ ] Publish only reviewed, accurate disclaimers, privacy information, security reporting instructions, and support contact details.

External service references for the data-flow/privacy review: [Handyverse](https://handyverse.com/#/), [The Handy privacy policy](https://www.thehandy.com/privacy-policy/), [Handy API v3 docs](https://www.handyfeeling.com/api/handy-rest/v3/docs/), and the [Handy Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml). The privacy policy and API docs provide useful facts but do not by themselves establish the project's legal role or the upload endpoint's exact data-retention period.

### 3. Freeze initial product and protocol boundaries

- [ ] Define supported applications, operating systems, device models, firmware versions, API/protocol modes, and required external software.
- [ ] Decide firmware update policy; under the proposed default, document compatibility checks and point to official vendor instructions without downloading or installing firmware.
- [ ] Document behavior for unsupported firmware, protocol errors, outages, device takeover, synchronization loss, and unavailable internet/service.
- [ ] Maintain a protocol compatibility matrix and version-change/revalidation checklist.
- [ ] Review product vision and backlog proposals against these boundaries; label items in-scope, deferred, experimental, or explicitly excluded.

### 4. Stabilize software against the support promise

- [ ] Prioritize the existing recovery, timeout/retry, command coalescing, diagnostics, and capability-validation work against agreed failure scenarios.
- [ ] Establish CI build/test gates and required coverage for lifecycle, protocol handling, persistence, and failure paths.
- [ ] Verify privacy/security controls, user-visible errors, settings behavior, shutdown, and recovery without physical hardware where test doubles allow.
- [ ] Define supported installation/update/rollback steps and troubleshooting guidance for external tools and device connectivity.
- [ ] Set release acceptance criteria, versioning/changelog practice, artifact provenance/signing needs, and a rollback/hotfix path.

### 5. Evaluate GUI quality and release readiness

- [ ] Identify target users and top workflows; gather usability feedback before choosing redesign work.
- [ ] Review consistency, accessibility, keyboard navigation, error recovery, onboarding, and high-DPI behavior across both applications.
- [ ] Add only prioritized UI changes with testable acceptance criteria; preserve established protocol and lifecycle contracts.
- [ ] Run the release checklist, resolve blockers, document known limitations, and decide whether the product is ready for the stated support promise.

## Backlog Overlap and Ownership

Keep implementation details in the existing component backlogs. This plan owns cross-cutting decisions, sequencing, and release gates; it does not duplicate backlog specifications.

| Production-readiness area | Existing backlog/document | Relationship |
| --- | --- | --- |
| CI, automated tests, privacy/operational guidance | [Shared backlog](../../BACKLOG.md) | Direct overlap. Prioritize these items under milestone 4 and add release acceptance criteria; do not copy their test lists here. |
| Session/UI separation | [Shared backlog](../../BACKLOG.md) | Related engineering quality work, but only extract boundaries where reliability or testability warrants it. |
| Device recovery, timeouts/retries, coalescing, events, diagnostics | [HandySync backlog](../../HandySync/BACKLOG.md) | Direct overlap with reliability and support promises. Select and sequence specific entries after protocol support and recovery expectations are defined. |
| Firmware and capabilities | [HandySync backlog](../../HandySync/BACKLOG.md) | Existing partial capability/firmware checks overlap with compatibility policy. Firmware updating itself is not currently an item; proposed default is external vendor instructions only. |
| Playback speed synchronization and local output | [HandySync backlog](../../HandySync/BACKLOG.md) | Product-scope decisions may defer or exclude these proposals; they are not automatic release blockers. |
| Editor markers, live pattern recording, multiple tracks/project format | [Editor backlog](../../FunscriptEditor/BACKLOG.md) | GUI/product possibilities, not prerequisites for production readiness unless prioritized by target-user evidence. |
| Product boundaries and possible extensions | [Product vision](../../VISION.md) | Treat as future direction; reconcile it with the supported protocol/product scope during milestone 3. |
| Current component responsibilities and contracts | [System architecture](../../ARCHITECTURE.md) | Preserve these ownership and lifecycle contracts while stabilizing or changing features. |

## Release Readiness Gate

Do not call the software production-ready until the team can answer and verify:

- Who owns and maintains the software, and where are support/security reports handled?
- What license applies, what third-party notices are required, and what user-facing legal/privacy information has been reviewed?
- What information leaves the user's machine, where credentials are stored, and what appears in logs?
- Which operating systems, devices, firmware, protocols, and external dependencies are supported?
- How does the software behave on network/service loss, invalid credentials, unsupported firmware, protocol changes, and shutdown?
- Can CI build and test the release, are known limitations documented, and can a bad release be withdrawn or superseded?

Record evidence, unresolved risks, and a go/no-go decision in a `VERIFICATION.md` when implementation begins.

## Status Notes

- No repository ownership transfer or legal/compliance review has been performed by this work item.
- No firmware updater or protocol support expansion is authorized by this plan.
- The existing backlog items remain the implementation source of truth until explicitly selected and tracked here.
