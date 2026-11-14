# Tested Environments

**Status:** ENV-001 records a historical build with local changes. ENV-002 records the maintainer's later positive production-test report, but the exact tested revision, environment, and workflow list were not captured. Neither record validates a clean release build or the current release candidate.

The current source targets .NET 10. The environment and device details below remain historical .NET 9 evidence; retargeting does not extend their compatibility claims.

## What ENV-001 establishes

- One environment was recorded: Windows 11 Home 25H2, x64; .NET SDK `10.0.401`; Microsoft Windows Desktop Runtime `9.0.11`; and mpv `v0.41.0-244-gaf9c81fa1`.
- Physical device checks were reported for two Handy H01 devices (model number 0, variant 0), using firmware `4.3.0+dcee7e58` and `4.2.2+44874492`. These results do not certify every H01 or other device/firmware versions.
- The recorded build used Debug configuration at commit `b21834c64d818c3a54c47e0dca455e8411ac93f2`, with uncommitted HandySync changes. It is not a clean build of that commit alone and does not validate the current release candidate.
- The target framework was .NET 9. Its support ended [November 11, 2026](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support); the recorded runtime version is historical and does not represent a currently supported runtime.

See [initial support scope and known limitations](SCOPE-AND-LIMITATIONS.md) for the requirements and boundaries that apply to users. Vendor firmware/API minimums are not equivalent to tested compatibility.

Copy the record below for each tested combination. Record exact versions rather than "latest"; use "not checked" for unknown values. A passing check applies only to the recorded source revision, environment, and workflow, not every model or release.

## ENV-002 — firmware-capabilities production retest (incomplete metadata)

This entry preserves the maintainer's final production-test outcome reported after
the firmware-capabilities milestone and before PR #13 was merged on 2026-11-13.
The report was: **“It works perfect.”** The conversation did not record which
individual workflows were exercised or the machine/device versions used. Do not
interpret this as a pass for every ENV-001 workflow or as release-candidate
validation.

| Field | Value |
| --- | --- |
| Test date and tester | Reported before PR #13 merge on 2026-11-13; exact test date/time not recorded. Tester: Frank Yong (maintainer report). |
| ScriptSyncPilot source branch | `firmware-capabilities` |
| Exact source revision used by tested build | Not recorded. The branch history includes `f9694d902779f23c833cd272007df289562797bf` (code changes) and `b221e396b3c72a39eaef2b04f80ca3cee9718263` (review documentation), but there is no build manifest or confirmation tying the tested binary to either SHA. |
| Local source changes | Not recorded for the tested binary. |
| Application(s) tested | Not specified in the final-test report. |
| Build configuration | Not recorded. |
| Windows edition/version/build and architecture | Not recorded for this test. ENV-001's Windows details are not assumed to apply. |
| .NET SDK and Desktop Runtime | Not recorded for this test. ENV-001's versions are not assumed to apply. |
| mpv version/configuration | Not recorded for this test. ENV-001's mpv details are not assumed to apply. |
| Handy model/hardware revision and firmware | Not recorded for this test. ENV-001's device details are not assumed to apply. |
| Protocols exercised | Not specified in the final-test report. |
| Connection conditions | Not recorded. |
| Overall outcome | Pass as reported by the maintainer (“It works perfect”); no logs, checklist, or workflow-level results were attached to that report. |

### ENV-002 workflow results

| Workflow | Result | Notes / evidence |
| --- | --- | --- |
| Final production test | Pass (maintainer-reported) | Overall positive outcome only; test scope and individual workflows were not listed. |
| Build from exact recorded source | Not established | Exact source SHA, local changes, build configuration, SDK, and runtime were not recorded. |
| Editor and mpv workflows | Not established | No workflow-specific result was recorded. |
| Handy connection, upload, HSSP playback, and device controls | Not established | No workflow-specific result or device/firmware details were recorded. |

ENV-002 is useful as a dated maintainer-reported outcome, but does not meet the
environment-record standard for claiming reproducibility or compatibility. If
the maintainer still has the build notes or can identify the tested setup, replace
the unknown fields with those exact values and split the outcome into the actual
workflows performed. Do not infer them from ENV-001.

## ENV-001

| Field | Value |
| --- | --- |
| Environment recorded | 2026-10-24; Windows details supplied by maintainer screenshot |
| Test date and tester | 2026-10-24; Frank Yong |
| Application(s) tested | FunscriptEditor and HandySync |
| Build configuration | Debug |
| mpv version | v0.41.0-244-gaf9c81fa1; built 2026-03-02 (queried from the maintainer-specified installation using `mpv.exe --version`) |
| mpv launch/configuration | Running process verified: `--input-ipc-server=\\.\pipe\vrheadtrack`, with an explicit `--config-dir` pointing to its installation directory (private path omitted). Configuration-file contents and effective playback settings not checked. |
| .NET SDK used to build | 10.0.401 (current repository SDK, verified with `dotnet --version`) |
| .NET Desktop Runtime used | Microsoft.WindowsDesktop.App 9.0.11 (installed runtime matching the apps' net9.0-windows target; running-process version not independently checked) |
| CPU architecture | x64 (PROCESSOR_ARCHITECTURE=AMD64; Intel processor) |
| ScriptSyncPilot source tag/commit | `b21834c64d818c3a54c47e0dca455e8411ac93f2` for both FunscriptEditor and HandySync; local changes described below |
| Windows edition, version, OS build | Windows 11 Home, version 25H2, OS build 26200.9711 |
| Local source changes | Uncommitted HandySync hardware diagnostics in `HandyApiClient.cs` and `MainForm.cs`, with related tests/documentation. HandySync DLL built at 10:38:40 and the running process started at 10:38:41 on 2026-10-24; subsequent logs demonstrate the added diagnostics. Embedded commit metadata does not identify uncommitted changes; this is not a clean-checkout build of the base commit alone. |
| Handy model / hardware revision | Both devices report model name H01, model number 0, model variant 0. Separate manufacturer and hardware revision not reported by the API; model variant is not a confirmed hardware revision. |
| Handy firmware version | Device A: 4.3.0+dcee7e58, firmware status 0. Device B: 4.2.2+44874492, firmware status 1. |
| Protocols exercised | Handy REST API v3, Hosting API v2 and HSSP; connection/setup corroborated by logs, playback synchronization reported successful by maintainer |
| Connection conditions | Home Wi-Fi (confirmed by maintainer for both device connections on 2026-10-24); no SSID, IP address or credentials recorded |
| Other environment fields | Not checked |
| Workflow test results | All listed workflows passed per maintainer confirmation on 2026-10-24; connection, upload and HSSP setup additionally corroborated by logs |

### HandySync device connection checks

The maintainer reports connecting two different physical devices after restarting HandySync.
Device A/B are record-local labels, not device identifiers. Evidence was reviewed from
the local HandySync log on 2026-10-24; only non-sensitive diagnostic fields are transcribed here.

| Field / workflow | Device A | Device B |
| --- | --- | --- |
| Connection timestamp (local log time) | 2026-10-24 10:39:25.799 | 2026-10-24 10:41:02.263 |
| Hardware model name / number / variant | H01 / 0 / 0 | H01 / 0 / 0 |
| Firmware version / status | 4.3.0+dcee7e58 / 0 | 4.2.2+44874492 / 1 |
| Handy connection, script upload and HSSP setup | Pass: connected; HSSP mode set; upload completed; HSSP setup completed at 10:39:28.740 | Pass: connected; HSSP mode set; upload completed; HSSP setup completed at 10:41:04.794 |
| Initial offset read | 16 ms | 0 ms |
| Initial stroke read | 0%-73% (5.0-78.5 mm) | 0%-97% (5.0-102.0 mm) |
| Playback, pause, seek and physical synchronization | Pass (maintainer-reported) | Pass (maintainer-reported) |
| Offset/stroke writes and physical response | Pass (maintainer-reported) | Pass (maintainer-reported) |

Two earlier attempts at 10:39:17.954 and 10:39:22.896 reported
`Device reports not connected. Check the connection key.` The next attempt connected
successfully. The cause of the initial failures was not established; a wrong key is
not confirmed by this message alone.

Successful initial offset/stroke reads alone do not establish successful writes or
physical response; the passes above rely on the maintainer's separate confirmation.
These results do not establish compatibility with other models, firmware builds or
source revisions.

### ENV-001 checks performed

On 2026-10-24, Frank Yong confirmed that all listed tests had been performed
successfully. Manual outcomes below are maintainer-reported, not independently
observed by the assistant, except for the reviewed connection/setup log evidence.

| Workflow | Result | Notes / evidence |
| --- | --- | --- |
| Build from recorded source | Pass | Maintainer confirms both applications used the recorded commit, with local changes described above. Not a clean-checkout validation. |
| Editor launch and mpv attachment | Pass | Maintainer-reported |
| Script open, edit, undo/redo, save and reload | Pass | Maintainer-reported |
| Editor playback-speed controls | Pass | Maintainer-reported |
| HandySync launch, key entry/protected save and relaunch | Pass | Maintainer-reported; restarted HandySync process also observed |
| Handy connection, script upload and HSSP setup | Pass | Maintainer-reported; logs corroborate success for both devices as detailed above |
| 1x play, pause and seek synchronization | Pass | Maintainer-reported |
| Offset/stroke controls, where supported | Pass | Maintainer-reported physical response; logs additionally confirm initial reads |
| Non-1x playback stops/suppresses device synchronization | Pass | Maintainer-reported |
| Disconnect and application shutdown | Pass | Maintainer-reported |

### ENV-001 limitations and failures

- Workflows not exercised: none of the listed workflows, per maintainer confirmation.
- Observed failures: two initial connection attempts returned not connected before a subsequent successful connection; cause and reproducibility not established.
- Workaround: a later connection attempt succeeded; no specific corrective action verified.
- Build limitations: local Debug builds from the recorded commit, including uncommitted HandySync changes; not a clean-checkout validation.
- Revalidation: repeat against the intended clean release revision before claiming that revision is validated.

## Compatibility and protocol matrix

This matrix summarizes ENV-001 only; vendor minimum requirements are not test results.

| Application / integration | Recorded coverage | Result / boundary |
| --- | --- | --- |
| FunscriptEditor / mpv IPC | ENV-001 Windows x64, .NET and mpv versions above | Pass for the listed editor workflows; no other platform or version coverage claimed |
| HandySync / Handy REST API v3 | H01 / model number 0 / variant 0; firmware `4.3.0+dcee7e58` (status 0) and `4.2.2+44874492` (status 1) | Pass for the listed workflows on both devices; vendor minimum is firmware v4 |
| HandySync / Hosting API v2 | CSV upload and returned URL used for HSSP setup on both devices | Pass; retention question sent 2026-10-22 received no reply as of 2026-11-13; file retention and URL lifetime remain unknown |
| HandySync / HSSP | Normal-speed playback, pause and seek on both recorded firmware builds | Pass (maintainer-reported); non-1x device playback is excluded |
| HandySync / offset and stroke | Both recorded devices/firmware builds | Pass (maintainer-reported); requires supported firmware and relevant device capabilities |
| HandySync / non-1x safety behavior | Both recorded devices/firmware builds | Pass (maintainer-reported): device synchronization stops/is suppressed rather than following non-1x playback |
| Other models, firmware builds or dependency versions | No recorded coverage | Not validated; add an environment record before claiming compatibility |
| Legacy API v2, alternate movement protocols, other vendors and offline output | Outside initial support scope | Not supported by this release scope; Hosting API v2 is separate from legacy device REST API v2 |

### Version-change and release revalidation checklist

For each intended source release and whenever a supported environment or integration
changes:

- [ ] Record the exact source revision, local changes (if any), application build configuration, Windows/architecture, .NET, mpv, and device model/firmware in a new or explicitly dated environment record.
- [ ] Build and run required automated tests from the intended clean release checkout.
- [ ] Repeat the affected workflows from the environment template; for a release, repeat all core editor and synchronization workflows.
- [ ] For mpv changes, verify IPC attachment, playback controls, play/pause/seek events, and playback-rate safety behavior.
- [ ] For device firmware, Handy API or protocol changes, review current official API requirements and verify connectivity, info/capabilities, upload/setup, playback timing, offset/stroke controls and device-stop attempts.
- [ ] Exercise relevant failure paths, including invalid credentials, unavailable services, malformed responses, settings/save failures and shutdown; record outcomes separately from normal-path passes.
- [ ] Record failures, known limitations and retest evidence; update this matrix and the support scope without extending claims to untested combinations.

These unchecked items are a reusable checklist for the next revalidation, not
unfinished fields in ENV-001. ENV-001 does not replace clean-release or failure-path
verification.

## Environment record template

| Field | Value |
| --- | --- |
| Record ID | e.g. ENV-001 |
| Test date and tester | |
| ScriptSyncPilot source tag/commit | |
| Local source changes | None, or describe relevant changes |
| Application(s) tested | FunscriptEditor / HandySync |
| Build configuration | Debug / Release |
| Windows edition, version, OS build | From `winver` |
| CPU architecture | x64 / ARM64 / other |
| .NET SDK used to build | From `dotnet --version` in the repository |
| .NET Desktop Runtime used | From `dotnet --list-runtimes`; record Microsoft.WindowsDesktop.App version |
| mpv version | From `mpv --version` for the executable actually used |
| mpv launch/configuration | Relevant IPC and playback options; omit private paths |
| Handy model / hardware revision | Manufacturer/model designation; no serial number or connection key |
| Handy firmware version | Exact version from device information or official interface |
| Protocols exercised | Handy REST API v3 / HSSP / Hosting API v2, as applicable |
| Connection conditions | e.g. normal home Wi-Fi; no SSID, IP address, or credentials |

### Checks performed

Use Pass / Fail / Not tested / Not applicable. Add the actual outcome and evidence; do not infer a pass from a version number.

| Workflow | Result | Notes / issue or evidence link |
| --- | --- | --- |
| Build from recorded source | Not tested | |
| Editor launch and mpv attachment | Not tested | |
| Script open, edit, undo/redo, save and reload | Not tested | |
| Editor playback-speed controls | Not tested | |
| HandySync launch, key entry/protected save and relaunch | Not tested | |
| Handy connection, script upload and HSSP setup | Not tested | |
| 1x play, pause and seek synchronization | Not tested | |
| Offset/stroke controls, where supported | Not tested | |
| Non-1x playback stops/suppresses device synchronization | Not tested | |
| Disconnect and application shutdown | Not tested | |

### Limitations and failures

- Workflows not exercised:
- Reproducible failures: link GitHub issues with reproduction steps, expected/actual behavior, frequency, and this environment ID.
- Workaround, if verified:
- Retest date/source revision and outcome, if applicable:

Do not include connection keys, generated script URLs, private media, or unreviewed settings/log files. Record security-sensitive defects through the repository's private reporting channel when available, not a public issue.
