# Protect Credentials and Script URLs — Verification

**Status:** Complete and archived on 2026-10-23, as confirmed by the maintainer. M1-M3 automatically verified (2026-10-22); M4.1/M4.2 complete. Recorded M4.2 results: 112/112 focused tests, non-incremental solution build with zero warnings/errors, and 238/238 full solution tests. Manual Windows/device checks are recorded below as completed on 2026-10-22, except the upload/setup failure case, which is automatically verified but was not exercised manually. The previously pending M4.3 final review has no separate execution result recorded; task closure does not assert an additional review or test run.

## M1.1 Characterization Baseline

- Added `HandySettingsCharacterizationTests` and `HandyScriptTransferCharacterizationTests` to the existing Windows MSTest project: 12 new test cases.
- Settings coverage: missing-field defaults, non-secret field round-trip, current legacy plaintext-key loading/serialization with other fields, and invalid JSON/typed values. Tests use in-memory serialization and do not touch the user's settings file.
- Transfer coverage: multipart CSV upload and subsequent HSSP setup payload, separation of hosting and authenticated device headers, HTTP upload failure, missing URL, malformed JSON, setup HTTP/device errors, and upload cancellation.
- Added an optional hosting `HttpMessageHandler` to the existing internal client constructor. Both transports are faked; the public constructor and production endpoints/behavior are unchanged. No real network requests or device are required.
- The plaintext-key and raw-upload-body assertions intentionally characterize insecure current behavior, not desired acceptance criteria. Replace them with rejection/protection and redaction assertions during M2/M3. Setup tests currently verify exclusion of response message details, not sanitization of every possible error field.
- File-system load/save failures, atomic persistence, and UI/persistent-log assertions remain for later milestones; this baseline does not establish credential or URL protection.

Validation command (VSTest/MSTest, .NET SDK 10.0.401):

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m11-verification\bin\ --filter "FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests" --verbosity minimal
```

Result: **14 passed, 0 failed, 0 skipped** (12 new characterization cases and two existing API regression tests). The normal output build was blocked by a running FunscriptEditor process; isolated build output succeeded without stopping the application. VS Code's test tool did not discover the requested files, so validation used `dotnet test`.

## M1.2 DPAPI Availability and Protection Boundary

- Located `System.Security.Cryptography.ProtectedData.dll` in the installed Windows desktop reference pack, `C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.11\ref\net9.0`. The existing project framework references compile the implementation without adding a NuGet package or changing project files.
- Added internal `IConnectionKeyProtector` and `DpapiConnectionKeyProtector`. They have no persistence/logging/UI effects; settings will consume the injectable boundary in M2.
- Nine new test cases exercise a same-user round-trip across instances, direct framework CurrentUser/no-entropy interoperability, corrupted ciphertext rejection, invalid Base64 rejection, empty/blank input rejection, and rejection of a valid DPAPI blob containing an empty key.
- Failure tests verify that malformed/corrupted input is not echoed in exception details and that no fallback key is returned. Synthetic test values only; no user settings or real credentials are accessed.
- Plaintext byte buffers are cleared; returned/input managed strings cannot be securely erased. Cross-account/profile behavior has not been tested.

Validation command (VSTest/MSTest, .NET SDK 10.0.401):

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m12-verification\bin\ --filter "FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests" --verbosity minimal
```

Result: **23 passed, 0 failed, 0 skipped** (nine DPAPI cases plus the 14 M1.1/regression cases). Compilation and Windows runtime DPAPI both succeeded without additional dependencies. Isolated output avoided the prior running-editor file lock. VS Code's test tool still did not discover the requested file, so validation used `dotnet test`.

## M2.1 Versioned Entry Representation

- Added immutable internal `HandySync.ProtectedConnectionKey` with `Id`, `Label`, `Version`, and `ProtectedValue`. No Common changes or dependency changes.
- Fourteen new cases verify exact serialized fields, absence of the synthetic plaintext key, real DPAPI entry round-trip, stable identifiers and ordered array round-trips, empty-array representation, missing fields, unsupported versions, blank/null values, empty identifiers, and invalid Base64.
- Validation errors do not interpolate the supplied protected value. The representation validates structure/encoding, not DPAPI authenticity; decryption remains the protector's responsibility.
- This verifies the standalone entry model only. `AppSettings` still uses the legacy plaintext field; runtime integration, legacy-key rejection, collection uniqueness/operations, and safe writes are not yet implemented. No user's settings are read or written by these tests.

Validation command (same VSTest/MSTest project):

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m21-verification\bin\ --filter "FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests" --verbosity minimal
```

Result: **37 passed, 0 failed, 0 skipped** (14 new entry cases plus 23 prior focused cases). Isolated output avoids running-app file locks. The VS Code test tool did not discover the requested files, so validation used `dotnet test`.

## M2.2 Protected Settings Save/Load

- Integrated the private persisted settings DTO and real CurrentUser DPAPI. Only `ConnectionKeys` is written; the runtime plaintext field is ignored by JSON serialization. Every entry is validated/decrypted before exposing the first key to the form.
- Added 14 cases in `ProtectedSettingsTests`: real protected file round-trip and first-entry update with stable ID, legacy rejection/re-entry, injected protection/decryption failures, malformed collections, preservation of later entries, rejection when a later entry fails, duplicate IDs, missing settings, invalid file contents, safe public JSON serialization, blank-key save rejection, and an unwritable destination.
- Changed M1.1 plaintext-key characterization into a rejection/omission assertion; malformed settings now raise `SettingsException` rather than leaking raw parser details. Tests verify non-secret fields retain values and protection failures happen before the destination is opened.
- Startup errors are surfaced and abort application startup without replacing settings. Save errors keep the form open for retry. Legacy files trigger a re-entry notice; blank keys cannot start a connection. These UI behaviors have compiled but still require manual checking.
- Initial regression run found that legacy-offset tests expect root JSON `null` to produce defaults. Preserved that established behavior; the final run passes those compatibility checks.
- No actual user settings or credentials are used by the new file tests. Test-created files/directories are cleaned up.

Validation command:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m22-verification\bin\ --filter "FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~DeviceSliderControlTests" --verbosity minimal
```

Result: **68 passed, 0 failed, 0 skipped**. Focused test-project build includes both apps and Common. Isolated output avoids running-app locks; VS Code test discovery returned no matching files, so validation used the existing VSTest runner through `dotnet test`.

Remaining: collection add/retrieve/delete API, full first-key UI verification, atomic persistence and mid-write failure preservation, cross-account check, full solution validation, and URL/log redaction. Do not infer atomicity or completed security acceptance from this save/load milestone.

## M2.3 Ordered Key Collection API

- Added internal add/first-key retrieval/delete-by-ID operations and a read-only entry view to HandySync settings. Loading and retrieval share the same secret-free decryption error handling.
- Thirteen new `ConnectionKeyCollectionTests` cases cover independently protected labeled entries and ordered/stable-ID round-trips; middle deletion; first deletion/promotion; last deletion/empty retrieval without resurrection on save; adding after load and duplicate labels; blank label/key rejection; invalid/unknown IDs; and protection/decryption failures without state mutation or sensitive exception details.
- Actual temporary-file save/load verifies promotion and last-key removal persist. Other cases use synthetic keys in memory. Temporary test files are cleaned up; no user settings, network, device, or Common changes are needed.
- UI selection/deletion remains absent. First-key device connection/manual UI verification and atomic writes are still separate work.

Validation command:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m23-verification\bin\ --filter "FullyQualifiedName~ConnectionKeyCollectionTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~DeviceSliderControlTests" --verbosity minimal
```

Result: **81 passed, 0 failed, 0 skipped** (13 new collection cases plus 68 previous focused cases). Build used isolated output to avoid running-app locks. VS Code's test tool did not discover the new file, so validation used `dotnet test`.

## M2.5 First-Key Connection Behavior

- Form initialization explicitly retrieves the first stored protected key. Added an internal settings/client-factory constructor; the public application path remains unchanged apart from explicit first-key retrieval.
- Five `FirstKeyConnectionTests` STA cases instantiate the actual form with isolated settings and synthetic keys. Four invoke the real connection method and assert the actual first connectivity request header: first of two saved entries; promoted successor after deletion/reload; edited textbox input; and manual entry without saved keys. The fifth confirms an empty form input after last-key deletion/reload.
- The fake transport reports disconnected, ending setup through existing error handling before any device setup/upload. Tests verify the connection button returns enabled. No actual network/device or user settings access occurs in these new tests.
- The edited-input case separately verifies settings serialization updates only the first entry and preserves the second key; it does not exercise the actual form-closing save handler. No manual UI/device acceptance is claimed.

Validation command:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m25-verification\bin\ --filter "FullyQualifiedName~FirstKeyConnectionTests|FullyQualifiedName~ConnectionKeyCollectionTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~DeviceSliderControlTests" --verbosity minimal
```

Result: **86 passed, 0 failed, 0 skipped** (five new form cases plus 81 previous focused cases). Isolated output avoids running-app locks; VS Code test discovery found no tests in the new file, so validation used `dotnet test`.

## M2.6 Failure Preservation (Including M2.7 Atomic-Write Prerequisite)

- Save stages candidate JSON/entries, writes and flushes a unique same-directory file, then replaces/moves it into place. No direct destination truncation is used. Failed saves do not commit candidate protected entries; edited runtime settings remain available for retry.
- Eight new `SettingsFailurePreservationTests` cases exercise partial staged writes for existing/absent destinations, successful replacement with inspection while half-written, protection failure before file I/O, actual Windows replacement denial using a locked destination, unavailable-key/corrupt-file loads, an unreadable locked settings file, and the actual form close handler's repeated save failure/retry behavior.
- Preservation assertions compare original file bytes, original in-memory entries, retained edit values, and removal of owned staging files. The successful replacement test checks same-directory staging and confirms the old file stays visible until staging finishes. Subsequent saves verify retry and reload.
- Expected errors expose no injected sensitive details/inner exceptions. Form tests inject safe save/error-display delegates, verify closing remains cancelled and retry is possible, and confirm lifetime cancellation has not started. No real user settings, devices, or network are needed.
- M2.7 is included because leaving direct writes in place cannot satisfy M2.6's original-file preservation requirement.

Validation command:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m26-verification\bin\ --filter "FullyQualifiedName~SettingsFailurePreservationTests|FullyQualifiedName~FirstKeyConnectionTests|FullyQualifiedName~ConnectionKeyCollectionTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~DeviceSliderControlTests" --verbosity minimal
```

Result: **94 passed, 0 failed, 0 skipped** (eight new cases plus 86 earlier focused cases). Isolated build output avoids running-app locks. VS Code test discovery did not find the new tests; validation used the existing VSTest runner.

Not tested: abrupt process termination/power loss, disk hardware failures, a different Windows account/profile, cleanup failure simulation, and final manual UI/release checks. A crash before replacement may leave protected staging files; there is no broad automatic cleanup.

## M3 Secret-Free Transfer and Synchronization Diagnostics

- Replaced the hosted-URL log line with a status message. Upload HTTP errors no longer read/log failure bodies; missing/blank URL and invalid JSON become explicit safe exceptions.
- Audited upload, setup, script replacement, stop-before-setup, connection clock sampling, and play/stop/seek/synctime paths. They all use the shared safe Handy transport/error helpers, and the form logs their safe messages without adding URLs. No Common logger changes or remote protocol changes were needed.
- Shared HTTP diagnostics omit untrusted reason phrases and device error names/descriptions; numeric HTTP status and device code remain. Existing `Name` assertions were updated to expect null. This is an intentional diagnostic change to avoid arbitrary remote text, not a loss of the numeric error code.
- Nonempty malformed PUT response JSON now produces an explicit safe failure; empty successful responses remain supported. Transport/read exception chains are replaced, not merely wrapped. Cancellations remain cancellation exceptions with safe text and the caller token.
- Updated upload characterization tests to assert no body/URL disclosure instead of documenting insecure baseline behavior.
- Added 18 `ScriptDiagnosticsTests` cases covering transport HTTP/I/O/cancellation failures, malformed/missing/typed/blank upload URLs, arbitrary device names and HTTP reason phrases, malformed setup roots, and five actual STA form scenarios (initial setup followed by successful replacement, upload HTTP error, malformed upload body, setup device error, or setup transport failure).
- The five form scenarios assert both actual textbox logs and the persistent `AppLog` file remain free of the synthetic hosted URL/token/connection key. Logs retain upload/setup status on success and explicit replacement failure messages on failure. Tests isolate and clean up a uniquely named log directory and restore the prior application log destination.

Validation:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m3-verification\bin\ --filter "FullyQualifiedName~ScriptDiagnosticsTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests" --verbosity minimal
dotnet test ScriptSyncPilot.slnx --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m3-verification\bin\ --verbosity minimal
```

The targeted run passed 25 cases before two final transport cases were added. Final full solution run: **238 passed, 0 failed, 0 skipped** (88 Common; 150 Windows/editor/HandySync). The first full run found a syntax error in the new test's throw expression; fixed it and reran successfully. Isolated output avoids running-app locks.

No real network/device or real secret was used in the new diagnostic cases. Historical logs are not sanitized; user-facing documentation warns about reviewing old logs before sharing. Cross-account, hardware/device playback, crash/power-loss, and final manual UI checks remain unverified.

## M4.1 User Documentation

- Expanded [local protection and diagnostics guidance](../../../docs/online-security.md#scriptsyncpilot-local-protection-and-diagnostics) with upgrade/re-entry instructions, the successful-close save trigger, first-key behavior, unmasked input/memory limits, DPAPI account/profile binding, and explicit load/save failure recovery.
- Confirmed settings/log paths against `AppSettings.FilePath`, `Program` logger initialization, and `AppLog.BuildPath`. Documented non-encrypted metadata, protected staging-file limitations, sensitive legacy backups, and historical logs that are not sanitized.
- Distinguished application-maintained guidance from the vendor-background attribution. No code changes or new tests were needed for this documentation-only step; no manual device/Windows acceptance is claimed.

## M4.2 Final Focused Tests, Solution Build, and Regression Suite

Validated on 2026-10-22 with .NET SDK 10.0.401, MSTest 3.6.4 and VSTest (`Microsoft.NET.Test.Sdk` 17.12.0). No `global.json`, `Directory.Build.props`, `Directory.Packages.props`, or VS Code task overrides were present in the repository root/task location. No dependency changes or restores were needed.

Executed in order:

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m42-verification\bin\ --filter "FullyQualifiedName~SettingsFailurePreservationTests|FullyQualifiedName~FirstKeyConnectionTests|FullyQualifiedName~ConnectionKeyCollectionTests|FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~ProtectedConnectionKeyTests|FullyQualifiedName~ConnectionKeyProtectorTests|FullyQualifiedName~HandySettingsCharacterizationTests|FullyQualifiedName~HandyScriptTransferCharacterizationTests|FullyQualifiedName~HandyApiDeviceControlTests|FullyQualifiedName~DeviceSliderControlTests|FullyQualifiedName~ScriptDiagnosticsTests" --verbosity minimal
dotnet build ScriptSyncPilot.slnx --no-restore --no-incremental -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m42-verification\bin\ --verbosity minimal
dotnet test ScriptSyncPilot.slnx --no-build --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m42-verification\bin\ --verbosity minimal
```

- Focused HandySync tests: **112 passed, 0 failed, 0 skipped**.
- Non-incremental full solution build: **succeeded, 0 warnings, 0 errors**, compiling both apps, Common, and both test projects.
- Full suite against those rebuilt assemblies: **238 passed, 0 failed, 0 skipped** (88 Common and 150 Windows/editor/HandySync).
- Isolated output avoids running-application file locks. Existing restored assets/intermediate directories were reused; this is a non-incremental compilation, not a fresh-machine dependency-restore test.
- No application processes were stopped, no real device acceptance was performed, and no manual Windows/cross-account checks are implied.

### Legacy Non-Secret Field Preservation Follow-up

Added `ProtectedSettingsTests.LegacyFile_KeyReentryPreservesAllNonSecretSettingsThroughSaveAndReload` to verify the exact outstanding criterion with a temporary on-disk legacy file. It checks every current non-secret field (`MpvIpcPipe`, `DeviceOffsetMs`, `StrokeMinPercent`, `StrokeMaxPercent`, `LastScriptPath`) against the saved JSON and reloads their non-default values. It also verifies load does not alter the legacy file, the legacy key is ignored, re-entry produces one protected entry, neither plaintext key appears in saved JSON, and the re-entered key decrypts on reload.

```powershell
dotnet test tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore -p:BaseOutputPath=F:\Users\email\repos\ScriptSyncPilot\artifacts\credential-m42-verification\bin\ --filter "FullyQualifiedName~ProtectedSettingsTests|FullyQualifiedName~HandySettingsCharacterizationTests" --verbosity minimal
```

Result: **20 passed, 0 failed, 0 skipped**, including the new end-to-end case. VS Code test discovery returned no tests; validation used `dotnet test`. This is a targeted follow-up after M4.2, not a new full-solution run. Unknown future fields and obsolete `OffsetMs` are not current non-secret settings; the latter remains intentionally discarded.

## Automated

- [x] Protected connection-key serialization does not contain the plaintext key.
- [x] Protected connection-key load returns the original value for the same Windows user.
- [x] Multiple labeled keys round-trip independently and preserve their saved order.
- [x] The connection flow retrieves the first saved key when there is no UI selection.
- [x] Deleting a key removes only that entry; deleting the first promotes the next key, and deleting the last leaves no active key.
- [x] Legacy plaintext `ConnectionKey` values are not loaded or migrated; the user can re-enter the key and save it in protected storage.
- [x] Decryption failure is reported explicitly and does not return an empty/default key or overwrite the original settings.
- [x] Protection or settings-write failure is reported and does not truncate the prior valid settings.
- [x] All non-secret settings retain their values when loading a legacy settings file and saving after key re-entry. Exact temporary-file load/re-entry/save/reload case passes for all five current fields; see follow-up above.
- [x] Successful upload/setup logging does not include the returned hosted URL.
- [x] Failed upload, malformed upload response, and HSSP setup errors do not expose the URL or raw response body in exception messages or log output.
- [x] Existing Handy API error diagnostics remain actionable without exposing credentials. Operation/status/code retained; remote error names intentionally omitted.

## Build and Tests

- [x] `dotnet build ScriptSyncPilot.slnx` (M4.2: non-incremental, isolated output, zero warnings/errors).
- [x] Run the focused HandySync tests in `tests/ScriptSyncPilot.FunscriptEditor.Tests`.
- [x] Run all relevant tests after focused verification.

## Manual Windows Checks

User confirmation (2026-10-22): both the legacy backup/re-entry/protected-save check and the same-account relaunch/saved-key functionality check pass. Results are user-verified; the attached real settings/backup files were deliberately not opened or copied into diagnostics.

- [x] Back up a legacy plaintext settings file, launch HandySync, confirm the key is not prefilled, re-enter it, save settings, and verify the saved key is protected rather than plaintext. User-confirmed (2026-10-22).
- [x] Relaunch under the same Windows account and confirm the saved key works. User-confirmed (2026-10-22).
- [x] Confirm decryption failure under an unsupported user/profile results in a clear error and does not replace the original settings file. User tested the protected settings on another Windows machine (2026-10-22): the supplied screenshot shows a clear decryption error; user confirms the main window does not open and the settings remain unchanged. This is cross-machine/profile evidence, not a separate-account test on the original machine.
- [x] Exercise a successful script upload and inspect the visible log and persistent log for URL, connection key, or raw response-body disclosure. User-confirmed (2026-10-22).
- Upload/setup failure logging: **automatically verified; manual failure not exercised**. User agreed (2026-10-22) to rely on the automated failure tests of both actual log surfaces rather than deliberately disrupting the device/network. This is not a manual failure-test pass.
- [x] Confirm HandySync still allows the user to update settings and connect normally after re-entering the key. User-confirmed (2026-10-22).

## Results

| Check | Result | Evidence / notes |
| --- | --- | --- |
| M1.1 characterization tests | Passed | 14/14 focused cases, including two existing API tests |
| M1.2 protection boundary | Passed | Nine real Windows DPAPI cases; combined focused suite 23/23 |
| M2.1 versioned entry representation | Passed | 14 new cases; combined focused suite 37/37 |
| M2.2 protected save/load | Passed | 14 new integration cases; combined settings/device regression suite 68/68 |
| M2.3 collection operations | Passed | 13 new cases; combined focused suite 81/81 |
| M2.5 first-key connection | Passed | Five STA form cases; combined focused suite 86/86 |
| M2.6/M2.7 failure preservation/atomic writes | Passed | Eight new cases; combined focused suite 94/94 |
| M3 diagnostics | Passed | 18 new diagnostic cases; actual UI/persistent logs checked |
| Full solution tests | Passed | 238/238 (88 Common, 150 Windows) |
| M4.1 user documentation | Complete | Upgrade, protection limits, recovery, local paths, and legacy copies/logs documented |
| M4.2 final focused tests | Passed | 112/112 before solution rebuild |
| Closure and verification limitations | Closed by maintainer on 2026-10-23 | No separate M4.3 final-review execution result recorded; manual upload/setup failure not exercised by agreement, covered by automated tests |
| Build | Passed | M4.2 non-incremental solution build, zero warnings/errors; full suite 238/238 against rebuilt assemblies |
| Manual key re-entry / protected save / relaunch | Passed (user-confirmed) | Both full manual checks confirmed on 2026-10-22; real credential files not opened by the assistant. |
| Manual cross-machine decryption failure | Passed (user-confirmed) | Clear decryption error shown; main window does not open; copied settings unchanged (2026-10-22). No credential values copied into this record. |
| Log/error verification | Success manually passed; failures automatically verified | User confirms secret-free visible and persistent success logs (2026-10-22). Supplied persistent-log excerpt also reviewed. Manual upload/setup failure not exercised by agreement; automated tests verify both actual log surfaces for failures. |
| Manual settings update / connection after re-entry | Passed (user-confirmed) | User confirms settings updates and normal connection after key re-entry (2026-10-22). |

## Limitations and Follow-up

- DPAPI CurrentUser does not protect against malware or processes running as the same user, administrators, or access to an unlocked session.
- The Handy Hosting API's URL expiration/retention behavior is controlled by Handy and is not changed by this work.
- Manual upload/setup failure was not exercised; relying on passing automated failure-log tests was explicitly accepted by the user (2026-10-22).
- Record any unresolved settings-backup, log-retention, or privacy issues here before closing the work item.
