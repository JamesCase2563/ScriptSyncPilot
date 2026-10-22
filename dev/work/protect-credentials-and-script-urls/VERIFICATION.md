# Protect Credentials and Script URLs — Verification

**Status:** M1.1 characterization and M1.2 protection boundary verified (2026-10-22); settings integration/redaction acceptance checks remain pending.

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

## Automated

- [ ] Protected connection-key serialization does not contain the plaintext key.
- [ ] Protected connection-key load returns the original value for the same Windows user.
- [ ] Multiple labeled keys round-trip independently and preserve their saved order.
- [ ] The connection flow retrieves the first saved key when there is no UI selection.
- [ ] Deleting a key removes only that entry; deleting the first promotes the next key, and deleting the last leaves no active key.
- [ ] Legacy plaintext `ConnectionKey` values are not loaded or migrated; the user can re-enter the key and save it in protected storage.
- [ ] Decryption failure is reported explicitly and does not return an empty/default key or overwrite the original settings.
- [ ] Protection or settings-write failure is reported and does not truncate the prior valid settings.
- [ ] All non-secret settings retain their values when loading a legacy settings file and saving after key re-entry.
- [ ] Successful upload/setup logging does not include the returned hosted URL.
- [ ] Failed upload, malformed upload response, and HSSP setup errors do not expose the URL or raw response body in exception messages or log output.
- [ ] Existing Handy API error diagnostics remain actionable without exposing credentials.

## Build and Tests

- [ ] `dotnet build ScriptSyncPilot.slnx`
- [ ] Run the focused HandySync tests in `tests/ScriptSyncPilot.FunscriptEditor.Tests`.
- [ ] Run all relevant tests after focused verification.

## Manual Windows Checks

- [ ] Back up a legacy plaintext settings file, launch HandySync, confirm the key is not prefilled, re-enter it, save settings, and verify the saved key is protected rather than plaintext.
- [ ] Relaunch under the same Windows account and confirm the saved key works.
- [ ] Confirm decryption failure under an unsupported user/profile results in a clear error and does not replace the original settings file.
- [ ] Exercise a successful script upload and an upload/setup failure; inspect the visible log and persistent log for URL, connection key, or raw response-body disclosure.
- [ ] Confirm HandySync still allows the user to update settings and connect normally after re-entering the key.

## Results

| Check | Result | Evidence / notes |
| --- | --- | --- |
| M1.1 characterization tests | Passed | 14/14 focused cases, including two existing API tests |
| M1.2 protection boundary | Passed | Nine real Windows DPAPI cases; combined focused suite 23/23 |
| Protection/redaction automated tests | Not run | Pending M2/M3 implementation |
| Build | Focused test-project build passed | Includes Common, HandySync, and FunscriptEditor; full solution build remains pending |
| Manual key re-entry | Not run | Pending implementation |
| Manual log/error review | Not run | Pending implementation |

## Limitations and Follow-up

- DPAPI CurrentUser does not protect against malware or processes running as the same user, administrators, or access to an unlocked session.
- The Handy Hosting API's URL expiration/retention behavior is controlled by Handy and is not changed by this work.
- Record any unresolved settings-backup, log-retention, or privacy issues here before closing the work item.
