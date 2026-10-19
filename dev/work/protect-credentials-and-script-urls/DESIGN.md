# Protect Credentials and Script URLs — Design

**Status:** Proposed; implementation not started.
**Affected projects:** HandySync, Common logging if required, HandySync tests, user documentation.

## Goal

Reduce exposure of the Handy connection key and temporary hosted-script URL in local files, logs, and error paths without changing the Handy API flow or silently losing saved settings.

## Current Behavior

- `HandySync.AppSettings` stores `ConnectionKey` in plaintext in `%APPDATA%\ScriptSyncPilot.HandySync\settings.json`.
- The connection key is also held in the UI and sent in the `X-Connection-Key` header to Handy REST API v3.
- Script upload returns a temporary download URL. `MainForm.SetupScriptOnDeviceAsync` writes the full URL to both the on-screen log and the persistent application log.
- Upload failures can include the response body in `HandyApiException.Message`; the form logs exception messages. Error bodies must not be assumed safe to persist.
- Application logs are append-only under `%LOCALAPPDATA%\ScriptSyncPilot.HandySync\scriptSyncPilot.handysync.log`; no automatic retention/rotation is currently implemented.
- Other settings include mpv pipe, device offset/stroke values, and the last script path. The script URL is not intentionally saved in settings.

## Security and Compatibility Decisions

### Connection key at rest

- Use Windows Data Protection API (DPAPI) scoped to `CurrentUser` for the stored connection key. Keep the plaintext key available in memory only as required to create the Handy API client.
- Store a versioned protected representation in the settings JSON, e.g. `ConnectionKeyProtected` with an explicit format/version prefix. Never serialize the plaintext `ConnectionKey`.
- Do not support plaintext-key migration. When loading an existing settings file containing only the legacy plaintext `ConnectionKey`, ignore that value; the user can paste the short key into the form again. On the next successful settings save, replace the old plaintext value with protected storage. Do not silently treat a missing key as a successful connection.
- If encrypted-key decryption fails, report a clear settings error and do not connect with an empty/default key or overwrite the settings file. Preserve all other settings and the original ciphertext so the user can recover the correct Windows account/profile or restore a backup.
- Explain that DPAPI CurrentUser protects against casual access by other Windows accounts and offline copying, but not malware or processes running as the same Windows user, administrators, or an already-unlocked user session. Copying the protected settings file to a different Windows user/machine is not a supported migration method.
- Verify the selected .NET/Windows API availability and required framework/package references before implementation; do not add a package if the framework provides the needed API.

### Temporary hosted-script URL and logging

- Treat the URL as a bearer capability: possession may allow downloading the uploaded script until the service expires it.
- Remove the full-URL log entry. Replace it with a non-sensitive status message such as “Script upload complete; configuring HSSP.”
- Do not display the URL in the visible log, persist it in settings, include it in exception messages, or include it in diagnostic/support instructions.
- Do not log raw response bodies from upload failures or malformed upload responses. Preserve useful diagnostics such as operation name and HTTP status code without secret-bearing body text.
- Audit HandySync log calls and exception paths around upload/HSSP setup so the URL cannot leak through wrapped exception details. Keep ordinary API error reporting useful and credential-free.

### Settings writes and failure handling

- Persist settings atomically (write a temporary file in the same directory, then replace/move) so an interrupted save cannot truncate the settings file or leave a partially updated configuration.
- Keep the existing non-secret setting fields and their semantics unchanged.
- Surface inability to protect/write the key explicitly in the UI and prevent a false-success save/connection state. Do not broadly catch and silently default when key protection or unprotection fails.
- Add a short user-facing note in HandySync documentation describing local key protection, that users must re-enter their key after upgrading from plaintext storage, account/machine-bound behavior, and where settings/logs live. Do not promise protection against malware or same-user access.

## Out of Scope

- Changing Handy authentication, connection-key generation, or remote Handy retention behavior.
- Encrypting every settings value or all application logs.
- Adding telemetry, a remote credential vault, or a cloud service.
- Automatically deleting/rotating all historical logs as part of this change; document current log handling and avoid adding new secrets. Log retention policy remains a separate decision if needed.
- Hiding/masking the connection-key input control; the immediate scope is persistence and diagnostics.

## Acceptance Criteria

- Newly saved settings never contain the connection key in plaintext.
- Legacy plaintext-key settings load without using the plaintext key; the user can re-enter it, and subsequent saves store only its DPAPI-protected representation.
- Decryption/protection/write failures are explicit and never cause silent key loss or overwriting the original settings.
- The full temporary upload URL is absent from UI logs, persistent logs, and error messages for success and failure paths.
- Existing non-secret settings round-trip unchanged.
- Tests cover ignoring legacy plaintext keys, protected round-trip after re-entry, decryption failure, write failure/atomicity where testable, and URL/error redaction.
- No physical Handy device is required for automated verification.
