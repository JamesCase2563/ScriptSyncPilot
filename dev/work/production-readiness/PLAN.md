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

- **GitHub destination:** Which user or organization should own `Frank-Yong/ScriptSyncPilot` after transfer? Confirm the recipient can administer the repository and preserve issues, pull requests, releases, Actions, Pages, and package links as applicable.
- **License:** Which license fits the intended users, contributions, dependencies, and distribution model? Review dependency licenses and required notices before choosing.
- **Legal scope:** Which jurisdictions and distribution model are intended? Obtain qualified advice on required disclaimers/notices and whether GDPR, the Cyber Resilience Act (CRA), or other product/privacy obligations apply. Do not claim compliance until applicability and obligations have been assessed.
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

- [ ] Select the destination GitHub account/organization and confirm recipient acceptance.
- [ ] Inventory repository settings and integrations that may need transfer/reconfiguration: maintainers, branch protections, Actions secrets/variables, Pages, releases, issue/PR templates, security reporting, and any external links.
- [ ] Transfer ownership using GitHub's supported process; verify redirects, clone/fetch, permissions, CI, releases, and links afterward.
- [ ] Record maintainers, release authority, security contact, and issue-triage expectations.

### 2. Establish legal, licensing, and privacy foundations

- [ ] Inventory shipped code, dependencies, assets, and existing attribution obligations.
- [ ] Choose and apply a repository license; include required third-party notices and contribution terms/process if contributions are accepted.
- [ ] Map data flows and local/remote storage, including device connection keys, settings, logs, script/video paths, Handy/cloud API requests, and retention/deletion behavior.
- [ ] Review secret handling and diagnostic logging; document privacy controls and avoid exposing credentials in logs or support requests.
- [ ] Obtain qualified advice on relevant jurisdictions, distribution, GDPR and CRA applicability, obligations, and any required user notices or processes.
- [ ] Publish only reviewed, accurate disclaimers, privacy information, security reporting instructions, and support contact details.

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
