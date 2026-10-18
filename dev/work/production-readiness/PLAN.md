# Production Readiness

**Status:** In progress; core product and operating decisions are recorded, with verification and implementation work remaining.
**Affected projects:** Repository/operations, FunscriptEditor, HandySync, Common, documentation.  
**Goal:** Establish the maintainer, legal, product-scope, security, reliability, and release foundations needed before describing ScriptSyncPilot as production-ready.

This is a coordinating work item, not a commitment to implement every possible feature. It records release decisions and sequences work that remains in the component backlogs. Legal and regulatory applicability must be confirmed with qualified counsel; this plan is not legal advice or a compliance determination.

## Outcomes

- Maintainer responsibilities for repository access, releases, and support are documented.
- Licensing, attribution, user-facing notices, privacy/data handling, and security contact/reporting have been reviewed and published as appropriate.
- Supported use cases, protocols, devices/firmware expectations, and explicit exclusions are documented.
- Existing reliability and privacy gaps are prioritized, implemented, and verified against measurable release criteria.
- A release checklist and support/maintenance expectations exist.

## Confirmed Operating Facts

- Maintained/published from Norway by an individual; intended for worldwide use.
- Free, MIT-licensed, source-only distribution; no commercial activity and no service provided. Keep releases source-only unless the scope is reassessed.
- The maintainer reports not receiving or controlling personal data. Verify this against telemetry, diagnostics, support workflows, and actual data flows before publishing privacy statements.
- Firmware updates are outside ScriptSyncPilot's scope; provide compatibility information and refer users to official manufacturer instructions.

## Open Decisions

- **Legal applicability:** Whether GDPR, the CRA, Norwegian/EEA rules, or other obligations apply to the verified operating model, and what actions/notices are required. Obtain qualified legal advice; do not claim compliance or non-applicability before the assessment.

## Scope

### Current Product and Protocol Baseline

The following describes the current implementation and documented vendor requirements; it is not a certification or guarantee of compatibility with every model:

- HandySync targets Handy REST API v3 and uses HSSP for synchronized script playback. Scripts are uploaded as CSV through Handy Hosting API v2 and referenced by the returned URL.
- Handy's API v3 specification requires firmware v4 or newer. Offset/stroke controls additionally require firmware v4+ with `fw_status` other than update-required and the relevant slider capability.
- HandySync requires an mpv session and internet access to Handy services. Automatic reconnection/recovery is not fully implemented; see the [HandySync backlog](../HandySync/BACKLOG.md).
- Firmware update/install flows, other device vendors, alternate Handy protocols, legacy API v2, local/offline device output, and non-1x device playback are outside the current product scope.
- Exact tested Handy models and firmware combinations have not yet been inventoried; do not make broader compatibility claims until verified.

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
- [ ] Complete the license/attribution inventory, including transitive SDK/dependency notices, bundled assets, and other shipped content.
- [x] Select MIT as the repository license.
- [ ] Confirm the exact copyright holder name and year for the MIT notice with the maintainer; then add the MIT license text and required third-party notices. Decide whether to accept contributions and, if so, document the contribution process/terms.
- [x] Document the implemented HandySync flow: the Handy REST requests include the connection key and application identifier in headers; the app uploads a CSV containing only the funscript action timestamps/positions to Handy's Hosting API, receives a temporary download URL, then sends that URL and playback/synchronization timing to HSSP. Video and app settings remain local. See [HandyApiClient](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs) and [Funscript.ToCsv](../../../src/ScriptSyncPilot.Common/Funscript.cs).
- [x] Verify the implemented request/data flow against the source and current official API/privacy documents. The [Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml) describes uploads as temporary URLs but does not specify their exact expiry/deletion period. The [Handy privacy policy](https://www.thehandy.com/privacy-policy/) describes connection key/IP, device state, third-party provider identification, and script download URL processing for online third-party synchronization; it says session data is deleted within one hour after logoff and the device retains the script until overwritten. That policy does not clearly establish retention for the separately uploaded CSV or the exact applicability to this direct upload/HSSP flow.
- [ ] Ask Handy to clarify the uploaded CSV's expiry/deletion period and which privacy-policy retention statements apply to ScriptSyncPilot's direct upload/HSSP flow; do not infer a retention guarantee from the session-data statement.
- [x] Inspect the application projects and manifests for telemetry/crash-reporting code or SDKs: none were found in the current source or explicit package references. This does not establish whether Handy independently collects data or determine what a user might submit through an external support channel.
- [ ] Document the support/reporting channel and instruct users not to include personal data, connection keys, or private hosted-script URLs in reports; reassess if a project-controlled collection channel is introduced.
- [x] Inspect credential handling and logs: HandySync persists the connection key in its local settings JSON, and logs the temporary hosted-script URL. The connection key was not found in the inspected explicit log messages.
- [ ] Decide and implement appropriate protections for the locally stored connection key and temporary script URL; redact or omit the URL from logs, and document log/settings locations and retention. Review exception and support-report paths for accidental disclosure.
- [ ] Verify the source-only, non-commercial, individual-publisher, no-service operating model and obtain a CRA assessment, including whether any open-source software steward role or other obligation applies. Reassess before introducing binaries, paid support, sponsorship, or other commercial activity.
- [ ] Assess GDPR and Norwegian/EEA data-protection obligations based on the maintainer's role, any personal data the project itself receives, the app's third-party data flows, and the worldwide audience. Determine whether privacy information, a lawful-basis/role analysis, contact point, or other process is required.
- [ ] Ask qualified counsel whether any additional obligations apply to this free software, including copyright/dependency notices, product liability or safety expectations for software that controls a physical device, security vulnerability handling, and any consumer-facing claims. Disclaimers do not replace applicable duties.
- [ ] Publish only reviewed, accurate disclaimers, privacy information, security reporting instructions, and support contact details.

External service references for the data-flow/privacy review: [Handyverse](https://handyverse.com/#/), [The Handy privacy policy](https://www.thehandy.com/privacy-policy/), [Handy API v3 docs](https://www.handyfeeling.com/api/handy-rest/v3/docs/), and the [Handy Hosting API spec](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml). The privacy policy and API docs provide useful facts but do not by themselves establish the project's legal role or the upload endpoint's exact data-retention period.

### 2. Freeze initial product and protocol boundaries

- [ ] Define supported applications, operating systems, device models, firmware versions, API/protocol modes, and required external software.
- [x] Keep firmware updating out of scope across the entire application: apps may report compatibility, and documentation refers users to official manufacturer instructions; ScriptSyncPilot does not download, install, distribute, or initiate firmware updates.
- [ ] Inventory and verify the Handy models/firmware combinations actually tested; maintain the compatibility and protocol revalidation matrix.
- [ ] Document behavior for unsupported firmware, protocol errors, outages, device takeover, synchronization loss, and unavailable internet/service.
- [ ] Maintain a protocol compatibility matrix and version-change/revalidation checklist.
- [ ] Review product vision and backlog proposals against these boundaries; label items in-scope, deferred, experimental, or explicitly excluded.

### 3. Stabilize software against the support promise

- [x] Assign accountability to the GitHub repository owner for issue and security-report triage, publishing releases, setting any older-version support policy, and communicating breaking changes.
- [ ] Prioritize the existing recovery, timeout/retry, command coalescing, diagnostics, and capability-validation work against agreed failure scenarios.
- [ ] Establish CI build/test gates and required coverage for lifecycle, protocol handling, persistence, and failure paths.
- [ ] Verify privacy/security controls, user-visible errors, settings behavior, shutdown, and recovery without physical hardware where test doubles allow.
- [ ] Document how users obtain source, build/install it, update to a later source revision, and return to a prior known-good revision; include troubleshooting guidance for external tools and device connectivity.
- [ ] Define version tags and release notes for source releases, plus how to withdraw or supersede a faulty release. Do not plan binary artifact signing/distribution unless the source-only scope changes.
- [ ] Set measurable release acceptance criteria: required CI build and tests pass from a clean checkout; documented supported environment/protocol requirements match the tested matrix; no known release-blocking defects remain; privacy/security and troubleshooting documentation is reviewed; and limitations are published.

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
| Playback speed synchronization and local output | [HandySync backlog](../../HandySync/BACKLOG.md) | Product-scope decisions may defer or exclude these proposals; they are not automatic release blockers. |
| Product boundaries and possible extensions | [Product vision](../../VISION.md) | Treat as future direction; reconcile it with the supported protocol/product scope during milestone 3. |
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
