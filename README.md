# ScriptSyncPilot

ScriptSyncPilot contains two Windows desktop applications: FunscriptEditor for authoring funscripts and HandySync for synchronizing a funscript with mpv and a Handy device.

This repository currently distributes source code only. There is no published version tag or binary installer yet. See the [source-release guide](docs/source-release.md) for how to obtain, build, install, update, and roll back a source build, and for the maintainer's tagging and release policy.

## Requirements and support

- **Platform:** Windows desktop. The only recorded physical test environment is Windows 11 Home 25H2, x64; other Windows versions and architectures have not been verified.
- **FunscriptEditor:** Install mpv separately for video playback. Local script editing does not require Handy services or upload the video.
- **HandySync:** Requires mpv with its IPC interface for playback synchronization, plus an internet connection to the Handy REST and Hosting services. It uploads funscript action data as a CSV; the associated video is not uploaded.
- **Handy firmware and controls:** Handy API v3 requires firmware v4 or newer, but that vendor requirement does not guarantee compatibility. Offset/stroke controls also depend on reported device capabilities.
- **Runtime:** The projects target .NET 10 for Windows. .NET 10 is an LTS release supported through November 14, 2028; install the latest servicing update.

## Important limitations

- HandySync supports normal-speed (1x) device playback only. At other or unknown playback rates, it stops device playback and suspends synchronization.
- Automatic recovery from network/service outages, device restarts, or control by another app is not guaranteed. If communication is lost, the app cannot guarantee that the physical device has stopped.
- Physical compatibility is documented only for two Handy H01 devices (model number 0, variant 0) with firmware `4.3.0+dcee7e58` and `4.2.2+44874492`. Other models and firmware are unverified; these are test results, not a guarantee for all devices of the same family.
- Saved Handy connection keys are protected for the current Windows user/profile. Hosted-script retention and generated-URL lifetime are unknown; do not assume a specific expiry period.

See the approved [support scope and known limitations](dev/archive/2026-11-production-readiness/SCOPE-AND-LIMITATIONS.md), the detailed [tested-environment record](dev/archive/2026-11-production-readiness/TESTED-ENVIRONMENTS.md), and [privacy and security information](docs/online-security.md). The tested record includes a historical build with local changes and does not validate the current release candidate.

## Support and security reports

Report ordinary support requests and reproducible defects through [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues), which are publicly accessible. Include the app/source version, Windows and relevant mpv/device versions, reproduction steps, and sanitized diagnostics.

**Do not include personal data, Handy connection keys, generated/private script URLs, private media, or unreviewed settings and log files.** Do not report vulnerabilities in a public issue. GitHub private vulnerability reporting is enabled for this public repository; use **Security and quality → Advisories → Report a vulnerability** to report a security issue privately.

## Current release status

The repository owner has authorized a stable source-only release within the documented support scope. No version tag or GitHub Release has yet been published. See the [verification record](dev/archive/2026-11-production-readiness/VERIFICATION.md) for the decision basis and accepted limitations.
