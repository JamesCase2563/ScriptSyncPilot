# Protect Credentials and Script URLs — Verification

**Status:** Not started.

## Automated

- [ ] Protected connection-key serialization does not contain the plaintext key.
- [ ] Protected connection-key load returns the original value for the same Windows user.
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
| Automated tests | Not run | Pending implementation |
| Build | Not run | Pending implementation |
| Manual key re-entry | Not run | Pending implementation |
| Manual log/error review | Not run | Pending implementation |

## Limitations and Follow-up

- DPAPI CurrentUser does not protect against malware or processes running as the same user, administrators, or access to an unlocked session.
- The Handy Hosting API's URL expiration/retention behavior is controlled by Handy and is not changed by this work.
- Record any unresolved settings-backup, log-retention, or privacy issues here before closing the work item.
