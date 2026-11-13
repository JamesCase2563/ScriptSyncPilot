# Initial Support Scope and Known Limitations

**Status:** Approved by the maintainer on 2026-10-24, including limited automatic recovery for the first release. This approves the support scope, not release readiness.

## Release model

ScriptSyncPilot is a free, MIT-licensed project maintained by an individual. The initial release is **source-only**: users obtain the source from a published version tag and build it locally.

Support is best-effort through GitHub issues. There is no guaranteed response time, service availability, or fix schedule. Initially, fixes target the latest published source release; maintaining older release branches is not promised.

## Supported scope

### Requirements at a glance

| Application/use | Requirement |
| --- | --- |
| Both applications | Windows desktop. The recorded environment is Windows 11 Home 25H2, x64; other Windows versions and architectures are not verified in the environment record. |
| FunscriptEditor video playback | An external mpv installation. Local script editing does not require an internet connection or Handy services. |
| HandySync playback synchronization | mpv with an available IPC pipe, an internet connection, and reachable Handy REST/Hosting services. The associated video remains local; HandySync uploads the funscript action data as CSV. |
| HandySync device controls | Handy REST API v3 requires vendor firmware v4 or newer. Offset/stroke controls additionally require supported, reported device capabilities. This minimum is not a compatibility guarantee. |
| Runtime for current source | The project currently targets `net9.0-windows`; [.NET 9 support ended on 2026-11-11](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support). A stable release requires retargeting to a supported .NET version and re-verification. |

The [environment record](TESTED-ENVIRONMENTS.md) is the only recorded combination. It reports physical checks on two H01 devices (model number 0, variant 0), with firmware `4.3.0+dcee7e58` and `4.2.2+44874492`. It does not establish compatibility with every H01 or other device/firmware combinations.

### FunscriptEditor
- Local funscript authoring and editing on Windows.
- Video playback through an external mpv installation.
- Existing implemented editing features, including pattern insertion, undo/redo, numeric point entry, and playback-speed controls.
- Local processing of video and script files; the editor does not upload them to Handy services.

### HandySync
- Synchronization of a local funscript with mpv playback through Handy REST API v3 and HSSP.
- Script hosting through Handy Hosting API v2.
- **Normal-speed (1x) device playback only.** At other or unknown playback rates, device playback is stopped and synchronization is suspended.
- Offset and stroke controls where the device’s firmware and reported capabilities support them.
- Windows-user-bound protection of saved connection keys.

The release documentation must list the Windows, .NET SDK/runtime, mpv, and Handy model/firmware combinations actually tested. Vendor requirements—such as firmware v4 or newer for API v3—are not equivalent to verified compatibility.

## External requirements and boundaries

HandySync requires a persistent internet connection to reach Handy's REST and Hosting services. FunscriptEditor can edit local scripts without Handy services; video playback in either application uses an external mpv installation. Uploaded CSV files contain funscript action timestamps and positions; the associated video is not uploaded. The lifetime of hosted files and generated URLs is unknown; no retention or expiry period is promised.

ScriptSyncPilot does not install or update device firmware. Users must use the manufacturer’s official tools and instructions.

Other device vendors, alternative protocols, local/offline device output, and non-Windows platforms are outside the initial supported scope.

## Known limitations

- **Recovery is limited.** Automatic recovery from network/service outages, device reboots, or takeover by another application is not guaranteed. Manual reconnection may be necessary.
- **Concurrent device control is unsupported.** Avoid using another application to control the same device while HandySync is connected.
- **Timing is best-effort.** Network conditions, device behavior, and service availability can affect synchronization.
- **Device compatibility is limited to recorded tests.** Physical checks were reported on two H01 devices (model number 0, variant 0) with firmware `4.3.0+dcee7e58` and `4.2.2+44874492`; untested models or firmware versions are not claimed as supported. The test record describes its source/build qualifications.
- **Unsupported hardware/firmware is untested and not guaranteed to work.** Physical testing is limited to the devices available to the maintainer; acquiring unsupported devices or installing older firmware is not a release requirement. Firmware compatibility checks do not establish successful operation on an untested device.
- **Failure and recovery scenarios have limited physical coverage.** Automated tests simulate service outages, protocol errors, and local mpv IPC loss; these tests do not establish behavior during a real Handy/network interruption, device takeover, or physical synchronization loss. Automatic recovery is not guaranteed. If communication is lost, the app cannot guarantee a physical device stop; stop the device using its physical controls if necessary. See the [failure-path verification record](VERIFICATION.md#broader-failure-path-evidence) for the simulated cases.
- **Saved keys are account/profile-bound.** Copying protected settings to another machine or Windows profile may make them unreadable. Keep the original connection key available separately.
- **Historical files are not cleaned automatically.** Older settings backups or logs may contain plaintext keys or hosted-script URLs.
- **Provider retention remains unconfirmed.** Handy did not reply to the maintainer's 2026-10-22 question as of 2026-11-13; no further follow-up is planned. The exact lifetime of hosted uploads and generated download URLs is unknown. Do not promise a retention or expiry period.
- Features marked proposed, experimental, or deferred in the backlogs are not release promises.

These limitations do not excuse known defects that could disclose credentials, corrupt user files, or prevent controlled shutdown.

## Reporting problems

Report reproducible defects through GitHub issues, including:
- Source version/tag and relevant software/firmware versions.
- Steps to reproduce, expected behavior, and actual behavior.
- Sanitized diagnostics where useful.

Do not include connection keys, generated script URLs, private media, or unreviewed settings/log files in public reports. Security vulnerabilities should use the repository’s published security-reporting channel once established.

## Initial release gate

Before labeling a source release production-ready:

- A clean checkout builds and the required automated tests pass in CI.
- The tested environment and device compatibility matrix is published.
- Core editing and synchronization workflows have recorded manual checks.
- Failure-path checks cover invalid credentials, unavailable services, malformed responses, settings failures, and shutdown. Use test doubles or simulated responses where practical; unsupported physical hardware is not required. Record simulated and physical coverage separately, and document remaining untested scenarios.
- No known release-blocking credential-disclosure, data-loss, or device-stop/shutdown defects remain.
- Build/update/rollback instructions, privacy information, support/security contacts, and known limitations are reviewed and published.
- Remaining risks are explicitly accepted by the maintainer in the release verification record.

Release-task status is tracked in the [production-readiness plan](PLAN.md#milestones), not in a separate checklist here. Credential protection is recorded under milestone 1; error reporting, file preservation, and stop/shutdown verification are tracked under milestone 3.
