# ScriptSyncPilot

ScriptSyncPilot contains two Windows desktop applications: FunscriptEditor for authoring funscripts and HandySync for synchronizing a funscript with mpv and a Handy device.

This repository currently distributes source code only. There is no published version tag or binary installer yet. See the [source-release guide](docs/source-release.md) for how to obtain, build, install, update, and roll back a source build, and for the maintainer's tagging and release policy.

Before using HandySync, review the [supported scope and limitations](dev/work/production-readiness/SCOPE-AND-LIMITATIONS.md), [tested environments](dev/work/production-readiness/TESTED-ENVIRONMENTS.md), and [online security notes](docs/online-security.md). Device compatibility claims are limited to the combinations recorded there.

## Current release status

The repository is not yet marked production-ready. The current release verification record documents an unresolved full-suite test hang, and this source targets .NET 9, which reached end of support on November 11, 2026. The maintainer must resolve these release gates before publishing a stable source release.
