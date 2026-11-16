# Source Releases: Obtain, Build, Install, Update, and Roll Back

ScriptSyncPilot is distributed as source code. There is currently no installer, auto-updater, signed binary package, published version tag, or GitHub Release. The steps below describe the supported source-release process; a release is available only after its tag and GitHub Release have actually been published.

## Requirements and release status

- Windows x64. The applications use Windows Forms and target Windows.
- The .NET 10 SDK.
- The .NET 10 Windows Desktop Runtime to run framework-dependent builds.
- mpv for video playback. HandySync also requires internet access and available Handy services. Its initial supported device scope is limited to the models and firmware recorded in [tested environments](../dev/work/production-readiness/TESTED-ENVIRONMENTS.md); vendor minimums do not establish compatibility with an untested device.

The application projects now target `net10.0-windows`. .NET 10 is an LTS release supported through November 14, 2028, subject to installing the latest servicing update. Verify the target in CI and on the documented environment before publishing a stable release. Check [Microsoft's .NET release support dates](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support) and [current .NET downloads](https://dotnet.microsoft.com/download/dotnet) when preparing each release.

The complete Windows CI suite passed on .NET 10 at commit `72a4e5ed2e41cce09bc2657f51f36452ff7b9934` ([run 43565771417](https://github.com/JamesCase2563/ScriptSyncPilot/actions/runs/43565771417)). The repository owner recorded a GO decision for a stable source-only release on 2026-11-16, accepting the remaining evidence limitations documented in [production-readiness verification](../dev/work/production-readiness/VERIFICATION.md). No release tag or GitHub Release has yet been published.

## Obtain source

After a stable version is published:

1. Open the project's [GitHub Releases](https://github.com/JamesCase2563/ScriptSyncPilot/releases) page and select the desired version.
2. Download GitHub's **Source code (zip)** archive, or clone the repository and check out the exact version tag:

   ```powershell
   git clone https://github.com/JamesCase2563/ScriptSyncPilot.git
   Set-Location ScriptSyncPilot
   git switch --detach vX.Y.Z
   ```

Replace `vX.Y.Z` with the tag shown on the release page. The GitHub source archive and the tag contain source, not ready-to-run application binaries.

## Build and verify

From the repository root in PowerShell, restore dependencies, build both applications and run the automated suite:

```powershell
dotnet --info
dotnet restore .\src\ScriptSyncPilot.slnx
dotnet build .\src\ScriptSyncPilot.slnx --configuration Release --no-restore
dotnet test .\src\ScriptSyncPilot.slnx --configuration Release --no-build --no-restore
```

The exact target frameworks are declared in the project files. Do not treat a successful build as a test pass: both the build and complete test command must succeed for a release. The GitHub Actions workflow is [Windows release verification](../.github/workflows/windows-release-verification.yml).

## Install a local build

There is no MSI, setup wizard, or automatic update mechanism. Publish a framework-dependent app folder locally. For the current project layout, from the repository root:

```powershell
$version = 'vX.Y.Z'
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\ScriptSyncPilot\$version"

dotnet publish .\src\ScriptSyncPilot.FunscriptEditor\ScriptSyncPilot.FunscriptEditor.csproj `
  --configuration Release --runtime win-x64 --self-contained false `
  --output (Join-Path $installRoot 'FunscriptEditor')

dotnet publish .\src\ScriptSyncPilot.HandySync\ScriptSyncPilot.HandySync.csproj `
  --configuration Release --runtime win-x64 --self-contained false `
  --output (Join-Path $installRoot 'HandySync')
```

Use a versioned directory and keep the previous known-good directory. Launch `ScriptSyncPilot.FunscriptEditor.exe` or `ScriptSyncPilot.HandySync.exe` from the corresponding folder. These framework-dependent builds require the .NET 10 Windows Desktop Runtime. Install the latest supported runtime from Microsoft's [Windows Desktop Runtime downloads](https://dotnet.microsoft.com/download/dotnet).

FunscriptEditor and HandySync use separate settings. Configure mpv and its IPC pipe in the application as needed. HandySync's connection key is protected for the current Windows user/profile; do not copy protected settings to a different account or machine as a migration method. See [online security and settings locations](online-security.md).

## Update to a newer release

1. Read that version's release notes and known limitations. Confirm the target framework and required .NET Desktop Runtime.
2. Download and verify the new tag as described above. Build and run the full tests before installing a local source build.
3. Close both applications. Before first launch of the newer version, make a private backup of the settings files listed in [online security](online-security.md). Keep these backups under your Windows account; they may contain sensitive data. Do not post or upload them.
4. Publish/install the new version into its own versioned directory. Keep the previous directory until the new version has been opened and its expected workflows checked.
5. Launch the new executables. Settings remain in the user's AppData folders, separate from the application directories; a source update does not itself back up or migrate those files.
6. Remove an older application directory only after the newer version is known good and you no longer need the rollback copy.

The repository does not promise automatic settings migration across arbitrary versions. For a release that changes settings format, follow that release's explicit migration notes and retain a known-good backup before first launch.

## Return to a known-good version

If an update fails:

1. Close both applications and note the version that failed.
2. Launch the previous version from its retained versioned directory. If rebuilding from source, check out its earlier release tag (for example, `git switch --detach vX.Y.Z`) and rebuild it using that tag's instructions.
3. Keep the current settings files initially. If the older version cannot read settings changed by the newer version, close the app and restore the settings backup made before updating. Restore only under the same Windows user/profile; protected connection keys are DPAPI-bound to that profile.
4. Preserve sanitized error details for the maintainer. Do not include connection keys, private media, settings files, or generated Handy script URLs in a public report.

Rollback restores the application source/build; it cannot reverse device or remote-service actions. Do not delete the previous source tag or known-good application folder as part of an update.

## Version tags and release notes

The maintainer publishes releases from a reviewed commit on the default `main` branch, after all release gates are complete and a dated go/no-go decision is recorded in [production-readiness verification](../dev/work/production-readiness/VERIFICATION.md). Do not tag the current candidate while its release gates remain open.

Use annotated SemVer tags prefixed with `v`:

- `vMAJOR.MINOR.PATCH` for stable source releases.
- Use `v1.0.0` for the first stable release; subsequent releases follow the rules below.
- Increase **MAJOR** for an incompatible user-facing or settings change, **MINOR** for a backwards-compatible feature, and **PATCH** for backwards-compatible fixes.
- Use `vMAJOR.MINOR.PATCH-rc.N` only for an explicitly identified pre-release; do not describe it as stable.
- Create each tag once on the exact reviewed `main` commit. Never move or reuse a published tag. Example, after merging and reviewing the intended commit:

  ```powershell
  git switch main
  git pull --ff-only origin main
  git tag --annotate vX.Y.Z --message "ScriptSyncPilot vX.Y.Z"
  git push origin vX.Y.Z
  ```

Create a GitHub Release for that tag and write release notes manually. Include:

- The tag and source commit; state clearly that this is a source-only release and contains no installer or ready-to-run binaries.
- Highlights, fixes, known issues, breaking changes, and any required settings migration.
- Windows, .NET SDK/runtime, mpv, and Handy device/firmware requirements, with tested combinations linked to the environment matrix.
- Build, test, publish, and update instructions; link to this guide.
- The CI run and release verification/go-no-go evidence.
- Privacy and limitations that affect users; publish a support route that external users can access and a verified private security-reporting route. The current repository is private and has neither route for external users.

Do not claim a model, firmware, runtime, workflow, or security property beyond recorded evidence. Keep Handy upload retention and generated-URL lifetime unresolved unless reliable new information becomes available; do not promise a retention or expiry period.

## Withdraw or supersede a faulty release

If a published version is defective:

1. Update its GitHub Release title and notes immediately with **WITHDRAWN — do not use**, the affected tag, known impact, and any safe user action. Do not silently edit the tag's source commit.
2. Publish a clear advisory through the project's support channel and link it from the release page. Tell users whether to stop using the affected version and identify any known-good version, without overstating a workaround.
3. Prepare and verify a corrected commit. Publish it under the next unused SemVer tag (normally increment PATCH, such as `v1.2.4` after faulty `v1.2.3`) and link the superseding release prominently from the withdrawn release.
4. Keep the faulty tag and its release record in place for traceability; do not move, reuse, or delete a published version tag. Mark the corrected release as the latest stable release after its gates pass.
5. Record the incident, affected versions, correction, and verification evidence in the production-readiness record.

For a severe issue, include immediate containment instructions appropriate to the defect. If no safe workaround is known, say so plainly and direct users to stop using the affected version until a corrected release is available.
