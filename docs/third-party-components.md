# Third-Party Components and Attribution Inventory

**Reviewed:** 2026-11-14
**Distribution model reviewed:** source-only; no application binaries or third-party assets are checked into the repository.

This inventory covers the current project manifests and tracked repository content. Recheck it when adding dependencies, bundling assets, or changing the source-only distribution model.

## Application Runtime

- The three application/library projects target .NET 10 (`net10.0` / `net10.0-windows`) and have no NuGet `PackageReference` dependencies.
- The applications reference the in-repository `ScriptSyncPilot.Common` project. This is project code, not a third-party component.
- Windows Forms and the .NET runtime are framework dependencies, not bundled files in this repository. No self-contained .NET runtime is distributed.
- Handy REST/Hosting API calls are remote service integrations, not bundled SDKs.
- mpv is an external program connected over its local IPC interface; no mpv executable or library is bundled here.

## Test-Only NuGet Dependencies

The two test projects declare these direct packages:

| Package | Version | Declared license | Use |
| --- | --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | Test runner |
| MSTest.TestAdapter | 3.6.4 | MIT | Test discovery/adapter |
| MSTest.TestFramework | 3.6.4 | MIT | Test framework |

The resolved transitive test-only package graph is:

| Packages | Resolved version | Declared license |
| --- | --- | --- |
| Microsoft.ApplicationInsights; Microsoft.CodeCoverage; Microsoft.TestPlatform.ObjectModel; Microsoft.TestPlatform.TestHost | 2.22.0; 17.12.0; 17.12.0; 17.12.0 respectively | MIT |
| Microsoft.Testing.Extensions.Telemetry; Microsoft.Testing.Extensions.TrxReport.Abstractions; Microsoft.Testing.Extensions.VSTestBridge; Microsoft.Testing.Platform; Microsoft.Testing.Platform.MSBuild | 1.4.3 | MIT |
| Newtonsoft.Json | 13.0.1 | MIT |
| System.Diagnostics.DiagnosticSource | 5.0.0 | MIT |
| System.Reflection.Metadata | 1.6.0 | MIT (`LICENSE.TXT`); package also includes upstream notices in `THIRD-PARTY-NOTICES.TXT` |

The `Microsoft.Testing.Extensions.Telemetry` package is part of the test-platform graph only; it is not referenced by an application project and is not included in application runtime output. NuGet license metadata was checked for the resolved test graph on the review date. Recheck after package upgrades.

## Tracked Assets and Documentation

- No tracked executable, library, image, font, media, archive, or packaged binary assets were found.
- `dev/FunscriptEditor/PatternInsertionTrial.funscript` is a project-authored test/sample script; its metadata identifies `ScriptSyncPilot` as creator.
- Handy API and product-reference documents in `docs/` are summaries, with source links and extraction dates where applicable.
- `docs/EXTRACTED/BUTTPLUG_NOTES.md` links to Buttplug's official documentation and describes its BSD 3-Clause license; no Buttplug code, SDK, or bundled documentation is included.
- `docs/EXTRACTED/BUTTPLUG_USAGE_FROM_REPO.md` and `docs/EXTRACTED/HANDY_API_EXTRACT.md` are implementation-reference summaries, not bundled upstream source code.

This is an inventory, not a legal opinion. If any test package, runtime, external tool, documentation, or asset is redistributed with a release, review its applicable license and include any required notices with that distribution.
