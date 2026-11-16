# Tested Environments

**Status:** ENV-001 records a historical .NET 9 build with local changes. ENV-002 preserves an earlier positive manual-test report with incomplete metadata. ENV-003 records full .NET 10 automated runs locally and in GitHub Actions. ENV-004 records the successful manual .NET 10 trial and its log-supported workflow checklist; the exact SDK patch was not recorded and cannot be recovered. ENV-005 records successful Release `win-x64` framework-dependent publishes for both applications. ENV-006 records a partial second-machine check; HandySync's device connection was not verified.

The current source targets .NET 10. ENV-003 records automated .NET 10 build/test environments. ENV-004 adds a manually exercised Debug publish on .NET 10, mpv IPC, and one H01/firmware combination. ENV-001 remains the detailed baseline for broader physical workflow claims; do not generalize ENV-004 beyond its recorded device and workflows.

## ENV-006 — separate-machine manual check (partial)

The maintainer reports testing both applications on a separate Windows machine
on 2026-11-16. The two application logs are under the maintainer-provided
logs folder in the personal checkout. The logs were inspected without copying
the machine name, local script paths, connection keys, or URLs into this record.
The maintainer later reported that `dotnet --list-sdks` on that machine showed
SDK `10.0.404`. The app source revision, build configuration, OS build,
installed .NET runtime, and mpv version were not recorded, so this record does
not claim a clean-machine validation of the specific Release outputs.

| Application/workflow | Evidence and result |
| --- | --- |
| .NET SDK inventory | Maintainer-reported output: `10.0.404 [C:\Program Files\dotnet\sdk]`. This identifies an installed SDK, not the SDK or configuration used to build the tested application binaries. |
| FunscriptEditor mpv launch | At 08:53, launching the configured command `mpv` failed because Windows could not find that executable. |
| FunscriptEditor mpv IPC and script save | At 11:14, the app later attached to an mpv IPC pipe; at 11:15 and 11:19 it logged saves of 0 and 4 actions. This corroborates IPC attachment and save activity, but not which build was tested or a reopen/round-trip check. |
| HandySync settings and connection key | At 11:23, settings could not be loaded because a saved DPAPI-protected connection key could not be decrypted. At 11:25, saving settings was refused because the existing settings could not be loaded. This is consistent with using settings copied from another Windows profile/machine; the log does not establish how the file arrived there. |
| HandySync script and mpv IPC | The app loaded a 4-action script and attached to mpv IPC. |
| Handy device connection | **Untested on this machine by decision.** No successful connection event appears in the inspected log; the maintainer has directed that this machine's device test remain untested. ENV-004 separately records the manual device workflow on its own environment. |
| Overall result | **Partial.** The editor and mpv integration have useful evidence. This environment provides no device-connectivity result and does not identify the exact app build or Release configuration. |

The log's DPAPI decryption/settings-save errors are retained as environment
context only. No further device-connection attempt is planned on this machine.
DPAPI-protected keys are bound to the Windows user/profile; do not share the
settings file or connection key.

## ENV-005 — Release framework-dependent publish commands

On 2026-11-16, both publish commands documented in [`docs/source-release.md`](../../docs/source-release.md) completed successfully from the repository root. The output directory used a verification-specific name under `.verification` to avoid placing test artifacts in the user's normal application install directory. The command options match the guide: Release configuration, `win-x64`, and `--self-contained false`.

| Field | Value |
| --- | --- |
| Source revision | `919b80439435b1a5687a2f2278333cddb3ab9cc5` (`src/` matched the successful CI build at `0847b3d7d7bf5646c38672f470bac26b287d419b`; only readiness documentation was locally modified) |
| Environment | Windows 11 x64, OS build `26300`; .NET SDK `11.0.100`; MSBuild `18.12.2`; installed SDKs also include `9.0.308` |
| FunscriptEditor | `dotnet publish .\src\ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj --configuration Release --runtime win-x64 --self-contained false --output <verification-root>\FunscriptEditor` — **passed**, exit code 0 |
| HandySync | `dotnet publish .\src\ScriptSyncPilot.HandySync\ScriptSyncPilot.HandySync.csproj --configuration Release --runtime win-x64 --self-contained false --output <verification-root>\HandySync` — **passed**, exit code 0 |
| Output root | `.verification\source-release\verification-2026-11-16` (ignored verification artifacts) |
| Scope | Confirms both documented publish commands build and emit application folders with the installed .NET 11 SDK. Does not verify launching the Release outputs, runtime installation on a clean machine, hardware workflows, or the exact .NET 10 SDK version used in ENV-004. |

## What ENV-001 establishes

- One environment was recorded: Windows 11 Home 25H2, x64; .NET SDK `10.0.401`; Microsoft Windows Desktop Runtime `9.0.11`; and mpv `v0.41.0-244-gaf9c81fa1`.
- Physical device checks were reported for two Handy H01 devices (model number 0, variant 0), using firmware `4.3.0+dcee7e58` and `4.2.2+44874492`. These results do not certify every H01 or other device/firmware versions.
- The recorded build used Debug configuration at commit `b21834c64d818c3a54c47e0dca455e8411ac93f2`, with uncommitted HandySync changes. It is not a clean build of that commit alone and does not validate the current release candidate.
- The target framework was .NET 9. Its support ended [November 11, 2026](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support); the recorded runtime version is historical and does not represent a currently supported runtime.

See [initial support scope and known limitations](SCOPE-AND-LIMITATIONS.md) for the requirements and boundaries that apply to users. Vendor firmware/API minimums are not equivalent to tested compatibility.

Copy the record below for each tested combination. Record exact versions rather than "latest"; use "not checked" for unknown values. A passing check applies only to the recorded source revision, environment, and workflow, not every model or release.

## ENV-004 — manual .NET 10 trial (maintainer-reported pass; record completed)

On 2026-11-15, the maintainer reported that the recommended manual .NET 10
trial had been performed successfully. The local logs and published artifacts
were reviewed without reproducing their raw contents; paths, hosted URLs, and
credentials were not copied into this record. On 2026-11-16, the maintainer
instructed that the offset-poll timeout and two later stop errors be attributed
to pressing the device's manual emergency-stop button. The logs do not record
button activity; this is the maintainer's assumed explanation, not independent
log evidence. The app artifacts identify the tested code revision and target,
and the HandySync log provides the workflow evidence below.

| Field | Value |
| --- | --- |
| Report date and tester | 2026-11-15; Frank Yong (maintainer report) |
| Trial time window | HandySync activity from 09:29 to 10:25 local time; FunscriptEditor script load and mpv attachment at 10:25 |
| ScriptSyncPilot source revision | `e456b094689593f6054e5fdf827d3f6fbd588478`, recovered from both published app assemblies' `AssemblyInformationalVersion`; no `src/` changes are present between that revision and the current checkout |
| Build configuration / target | Debug `dotnet publish`; app runtime configs target `net10.0` / Windows Desktop `10.0.0` minimum |
| Windows edition/version/build and architecture | Windows 11 x64, OS build `26300` (local machine environment); exact edition not rechecked during the trial |
| .NET SDK | .NET 10 SDK was used, but its patch version was not recorded and cannot be recovered from the app logs or published artifacts. The current machine's SDK inventory does not establish which SDK was active at trial time. |
| .NET Desktop Runtime | `10.0.15` was installed before the trial; runtime configuration targets .NET 10 and the highest installed .NET 10 Windows Desktop runtime was `10.0.15` |
| mpv version/configuration | `v0.41.0-244-gaf9c81fa1`; the mpv process was running before the trial, and HandySync/FunscriptEditor logs record successful IPC attachment |
| Handy model / hardware revision | H01, model number 0, model variant 0; hardware revision not reported |
| Handy firmware | `4.2.2+44874492`, firmware status 1 |
| Protocols evidenced | Handy REST API, Hosting API v2 upload, HSSP setup; exact connection/network conditions not recorded |
| Applications and workflows evidenced | See the workflow checklist below. |
| Release-guide publish commands | Debug trial artifacts remain as described; the documented Release `win-x64 --self-contained false` commands were subsequently run successfully and are recorded in ENV-005 |
| Outcome | **Pass (maintainer-reported).** Logs show successful operations and several successful pause/stop events, with the last logged success at 10:21:51. At 10:23:34 offset polling timed out; at 10:25:12 and 10:25:15 HSSP stop requests returned device error 1001. Per the maintainer's instruction, this record assumes those errors resulted from the physical emergency-stop button being pressed. The logs do not capture the button press, and the two final stop requests themselves did not receive successful acknowledgments. |

### Workflow checklist

| Application/workflow | Result |
| --- | --- |
| FunscriptEditor launched from its local Debug publish; opened a funscript and attached to the running mpv instance over IPC | Pass; corroborated by app log |
| HandySync launched from its local Debug publish; opened a funscript and attached to mpv over IPC | Pass; corroborated by app log |
| HandySync connected to the H01 and read firmware/capability information | Pass; corroborated by app log |
| Uploaded action data as CSV and completed HSSP setup | Pass; corroborated by app log |
| Wrote stroke/control values and synchronized playback; seek/resync events were observed | Pass; corroborated by app log |
| Paused and stopped playback | Multiple successful pause/stop events logged. The final two HSSP stop requests returned error 1001 and are attributed to the manual emergency stop under the maintainer's assumption above. |
| FunscriptEditor editing/save workflows | Not exercised in this trial. |

The exact .NET SDK patch is unavailable; it is recorded as unknown rather than
reconstructed from the current machine state. The documented Release publish
commands were verified separately in ENV-005; this trial used Debug outputs.

## ENV-003 — .NET 10 Release build and automated tests

This record covers the current .NET 10 source tree. The local test run was on
`0d21dc889a8cb8d91b690fb3403cf0c0f0af251d`; GitHub Actions then ran successfully
on `72a4e5ed2e41cce09bc2657f51f36452ff7b9934`, which adds only the CI-trigger
workflow change on top of that source. These are automated build/test results,
not a manual production trial.

| Field | Local run | GitHub Actions run |
| --- | --- | --- |
| Date | 2026-11-14 | 2026-11-14 |
| Source revision | `0d21dc889a8cb8d91b690fb3403cf0c0f0af251d` | `72a4e5ed2e41cce09bc2657f51f36452ff7b9934` |
| Run/evidence | `dotnet test src/ScriptSyncPilot.slnx --configuration Release --verbosity minimal` | [Run 43565771417](https://github.com/JamesCase2563/ScriptSyncPilot/actions/runs/43565771417) |
| Operating system | Windows 11 x64, OS build `26300` | Windows Server 2025 x64, OS build `10.0.26100`; runner image `windows-2025-vs2026`, image version `20261103.298.1` |
| .NET SDK | `10.0.401` | `10.0.404` |
| .NET runtime | `Microsoft.NETCore.App 10.0.12`; `Microsoft.WindowsDesktop.App 10.0.12` | .NET runtime `10.0.15` installed by setup-dotnet; exact Windows Desktop runtime version not recorded in the job log |
| Build configuration / target | Release; `net10.0` and `net10.0-windows` | Release; `net10.0` and `net10.0-windows` |
| Restore and build | Passed as part of `dotnet test`; no build errors reported | Restore passed; Release build passed |
| Automated test result | **341/341 passed** (Common 90/90; FunscriptEditor 251/251) | **341/341 passed** (Common 90/90; FunscriptEditor 251/251) |
| mpv / physical device | Not exercised | Not exercised |
| Conclusion | Full local .NET 10 suite passes on this Windows environment. | Full CI suite passes on the exact CI commit using the .NET 10.0.15 runtime. |

ENV-003 verifies compilation and automated behavior after retargeting to .NET 10.
It does not establish desktop playback integration, Handy REST/Hosting behavior,
or physical device compatibility on .NET 10. Keep the local runtime updated; the
CI run used the current .NET 10 runtime available on its runner.

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
