# Protect Credentials and Script URLs — Design

**Status:** Approved (2026-10-22); M1-M3 implemented and automatically verified, including full solution tests. M2.7 atomic writes were implemented alongside M2.6. M4.1/M4.2 complete. Manual Windows/device checks are recorded in [VERIFICATION.md](VERIFICATION.md) as completed except the upload/setup failure case, which is automatically verified but was not exercised manually. M4.3 final review remains pending.
**Affected projects:** HandySync, Common logging if required, HandySync tests, user documentation.

## Goal

Reduce exposure of the Handy connection key and temporary hosted-script URL in local files, logs, and error paths without changing the Handy API flow or silently losing saved settings.

## Original Behavior (before this work)

- `HandySync.AppSettings` stores `ConnectionKey` in plaintext in `%APPDATA%\ScriptSyncPilot.HandySync\settings.json`.
- The connection key is also held in the UI and sent in the `X-Connection-Key` header to Handy REST API v3.
- Script upload returns a temporary download URL. `MainForm.SetupScriptOnDeviceAsync` writes the full URL to both the on-screen log and the persistent application log.
- Upload failures can include the response body in `HandyApiException.Message`; the form logs exception messages. Error bodies must not be assumed safe to persist.
- Application logs are append-only under `%LOCALAPPDATA%\ScriptSyncPilot.HandySync\scriptSyncPilot.handysync.log`; no automatic retention/rotation is currently implemented.
- Other settings include mpv pipe, device offset/stroke values, and the last script path. The script URL is not intentionally saved in settings.

## Security and Compatibility Decisions

### Connection keys at rest

- Use Windows Data Protection API (DPAPI) scoped to `CurrentUser` to protect every stored Handy connection key. Keep each plaintext key in memory only as required to create the Handy API client.
- Store keys as an ordered collection of entries, each with a stable identifier, a user-facing label, and a versioned protected value. Protect each key independently; never serialize a plaintext key. Labels and identifiers are not secret.
- The current connection flow retrieves the first saved key in collection order. Keep this behavior until a future UI allows the user to select another saved key. Preserve insertion order across load/save.
- The storage/API layer must support adding a labeled key, retrieving the selected/first key, and deleting a key by stable identifier. Deletion removes that entry from future settings saves; it must not affect other entries. If the first entry is deleted, the next entry becomes first. Deleting the last entry leaves no active key and must not silently connect with an empty/default key.
- The existing single-key form creates a first entry with a default label (for example, `Default`) when no keys exist, and updates that first entry on save rather than creating duplicates. The future key-management UI can add entries with user-chosen labels.
- Do not support plaintext-key migration. When loading an existing settings file containing only the legacy plaintext `ConnectionKey`, ignore that value; the user can paste the short key into the form again. Save it as a protected key entry on the next successful settings save. Do not silently treat a missing key as a successful connection.
- If any encrypted-key decryption fails, report a clear settings error and do not connect with an empty/default key or overwrite the settings file. Preserve other settings and protected entries so the user can recover the correct Windows account/profile or restore a backup.
- Explain that DPAPI CurrentUser protects against casual access by other Windows accounts and offline copying, but not malware or processes running as the same Windows user, administrators, or an already-unlocked user session. Copying the protected settings file to a different Windows user/machine is not a supported migration method.
- Verify the selected .NET/Windows API availability and required framework/package references before implementation; do not add a package if the framework provides the needed API.
- Do not add the dropdown, key labels editor, or key deletion controls to the UI in this work item. The existing form continues to use the first stored key; a future UI change can expose selection and deletion through the storage/API operations above.

### Verified protection boundary (M1.2)

- `System.Security.Cryptography.ProtectedData` is available through the existing `Microsoft.WindowsDesktop.App` framework reference (`net9.0-windows`, `UseWindowsForms=true`). Compilation and real Windows DPAPI tests succeed without adding a package, reference, or native interop.
- Internal `IConnectionKeyProtector` exposes `Protect(string connectionKey)` and `Unprotect(string protectedValue)`. `DpapiConnectionKeyProtector` implements it with strict UTF-8, DPAPI `CurrentUser`, no additional entropy, and Base64-encoded ciphertext. Base64 is an encoding of the DPAPI blob, not the protection itself.
- The boundary protects one key independently and has no logging, file access, UI, or settings dependencies. M2 will inject it into storage so tests can substitute deterministic protection and simulated protection/decryption failures. Collection order, IDs, labels, format versioning, and atomic writes belong to settings/storage, not this primitive.
- Null/blank inputs are rejected explicitly. Malformed Base64, DPAPI errors, and invalid UTF-8 propagate as exceptions; a decrypted blank key is also rejected. There is no empty/default-key fallback and exception messages do not interpolate the supplied plaintext or ciphertext. M2 must convert failures into secret-free user-facing settings errors and block connection/save without losing the source file.
- Temporary plaintext byte buffers are cleared in `finally` blocks. Managed input/output strings cannot be reliably erased; this does not promise protection against same-user process/memory access.
- Tests use synthetic keys in memory only and exercise actual CurrentUser DPAPI, interoperability with the framework API, corrupted ciphertext, malformed Base64, and empty input/decrypted values. Different-account/profile rejection remains a manual check; same-account tests cannot establish it.
- At M1.2 this boundary was not called by `AppSettings` or the form; the integration is now implemented in M2.2 below.

### Versioned entry representation (M2.1)

- Internal immutable `HandySync.ProtectedConnectionKey` defines the serialized entry: `Id` (non-empty GUID), `Label` (non-blank string), `Version` (integer), and `ProtectedValue` (non-blank Base64 ciphertext). There is no plaintext-key member or generated record `ToString` exposing the payload.
- Version `1` means strict UTF-8 protected using Windows DPAPI `CurrentUser` with no additional entropy, then Base64-encoded, as implemented by the M1.2 protector. Versioning is per entry so future formats can be distinguished without guessing or reinterpreting ciphertext.
- A JSON constructor validates both direct construction and deserialization. Missing required values, empty identifiers/labels, unsupported versions, and malformed Base64 fail explicitly with payload-free `JsonException` messages. Syntactically valid Base64 is not evidence of valid DPAPI data; authentication/decryption remains the protector's responsibility.
- IDs are supplied when creating entries and preserved during serialization/deserialization, never regenerated on load. An ordered JSON array holds multiple entries; collection-level uniqueness and add/update/delete operations belong to the later storage integration.

Example entry (illustrative ID; `AQID` is only a Base64 shape placeholder, not a usable DPAPI key):

```json
{
  "Id": "90c219cc-1754-42ed-b21c-2fb74185a526",
  "Label": "Default",
  "Version": 1,
  "ProtectedValue": "AQID"
}
```

M2.1 introduced and verified the representation independently, without changing runtime settings. M2.2 below now integrates it into save/load; collection operations and atomic writes remain tracked separately.

### Protected settings integration (M2.2)

- `AppSettings` now serializes a separate private settings DTO with an ordered `ConnectionKeys` array and the existing non-secret fields. The runtime `ConnectionKey` is excluded from public JSON serialization as well. Missing collections default to empty for legacy files; explicit null collections, null entries, duplicate IDs, unsupported versions, and decryption failures are rejected.
- Loading decrypts every entry before returning any usable settings, and exposes the first key to the existing form. No entry is skipped on failure. Protected entries retain IDs, labels, order, and ciphertext for subsequent saves.
- Saving protects the form's runtime key into a `Default` first entry when empty, or replaces the first entry while retaining its ID/label and preserving later entries. A blank runtime key cannot silently remove an existing saved entry; explicit deletion operations remain M2.3.
- Legacy `ConnectionKey` JSON is ignored rather than migrated. The form prompts for re-entry when legacy settings have no protected entries. Re-entry persists only protected data and preserves non-secret fields. Connection attempts with blank keys are rejected before creating a client.
- Internal path/protector overloads permit isolated file tests and simulated cryptographic failures. Expected format/cryptographic/I/O errors become payload-free `SettingsException` messages without retaining unsafe inner exceptions. Missing files retain default behavior; malformed files no longer silently become defaults. The existing root JSON `null` default behavior and discarded legacy `OffsetMs` behavior are preserved.
- Startup reports a settings error and exits without constructing a usable form after failed loading; this avoids an automatic default save over corrupt/unreadable credentials. Save failures are logged and shown, cancel closing, and leave the form available for retry.
- At M2.2, protection/serialization finished before the destination was opened, but writes were non-atomic. M2.6/M2.7 below now replace that direct-write path. No key-management UI/API or Common changes were included in M2.2.

### Ordered collection operations (M2.3)

- Internal `AppSettings.AddConnectionKey(label, connectionKey)` validates non-blank arguments, protects the supplied key before mutating state, appends the entry, and returns its new stable GUID. Labels need not be unique; identity is by GUID. Adding the first entry updates the runtime key and clears the legacy re-entry flag. Adding later entries leaves the active first key unchanged.
- `GetFirstConnectionKey()` decrypts the first protected entry through the same helper as settings loading, returning null only when the collection is empty. It never substitutes an empty key on decryption failure. The runtime `ConnectionKey` property remains the form's editable value; unsaved edits are committed to the first entry by serialization, not by retrieval.
- `DeleteConnectionKey(id)` removes only the matching entry. Removing the first decrypts the successor before any mutation and updates the runtime key, preventing the old key from overwriting the promoted entry on save. Removing the last clears the runtime key, so a later save cannot recreate it. Removing a non-first entry preserves the runtime key and survivor order.
- Empty/unknown deletion IDs raise explicit argument/not-found exceptions. Protection/decryption failures raise payload-free `SettingsException` without changing the collection or active key. `ConnectionKeys` exposes a read-only view of immutable protected entries for future in-app management, not a mutable list or plaintext-key collection.
- Operations change in-memory settings only; call `Save` to persist. There are still no selection, label-editing, or deletion controls in the UI. These APIs belong solely to HandySync, not Common.

### First-key form connection (M2.5)

- The public form constructor loads settings and uses the normal Handy client. An internal constructor accepts settings and a client factory so form connection behavior can be tested without user files, network, or device access.
- Initialization explicitly calls `GetFirstConnectionKey`; only an empty collection falls back to the runtime/manual value. Decryption errors still propagate rather than silently selecting another key.
- The existing textbox remains editable: connection uses its trimmed contents, preserving key re-entry and intentional changes rather than rereading stored ciphertext and ignoring user edits. Closing still saves that input into the first entry. No selection/deletion controls are introduced.
- STA form tests capture the actual `X-Connection-Key` header on the initial connectivity request. A fake disconnected response deliberately stops setup before firmware, clock sampling, uploads, or device actions. Tests cover the first of multiple saved keys, promoted-key reload, edited/manual input, and empty input after deleting the last key. Settings assertions additionally verify first-entry update does not change the second key.

### Failure preservation and atomic writes (M2.6/M2.7)

- File-write preservation requires the atomic-write milestone, so both are implemented together. Save builds JSON and candidate protected entries without committing them to the in-memory collection. Protection failure therefore starts no file I/O and leaves entries unchanged.
- A uniquely named `.settings-<GUID>.tmp` file is created with `CreateNew` in the destination directory, written as UTF-8 without BOM, flushed to disk, and closed. Only then does `File.Replace` replace an existing destination; a first save uses `File.Move` without overwrite. No delete-first, truncate-in-place, cross-directory staging, or direct-write fallback exists.
- Only successful replacement/move commits candidate entries and clears the legacy re-entry flag. Failed writes preserve prior protected entries while keeping the user's editable values for retry. Existing files remain unchanged during staged writes; replacement denied by Windows file sharing is reported without replacing them.
- Expected write/access/path errors produce a secret-free `SettingsException`; underlying exception details are not retained. Owned staging files are removed on handled failure. Cleanup failures are explicitly logged without paths or contents. No automatic cleanup of unknown/other processes' temporary files is attempted.
- A process crash before replacement can leave a protected temporary file, but does not truncate the destination. Atomic replacement is not a guarantee against every filesystem/hardware/power-loss failure; destructive crash/power-loss tests remain manual follow-up.
- Internal form save/error-display delegates enable testing the real close handler without modal dialogs or user settings access. Production defaults still save settings and display the standard error dialog. Failure cancels closing, resets `_isClosing`, and leaves lifetime cancellation/teardown untouched so the user can retry.

### Temporary hosted-script URL and logging

- Implemented in M3: the hosted URL exists only in the upload result/local setup flow and outgoing HSSP payload, never in the upload-completion log. Actual UI and persistent-log tests cover initial installation and replacement success/failure.
- Upload failures report numeric HTTP status without reading/logging failure bodies. Missing/blank URL or malformed/incorrectly typed JSON raises an explicit payload-free `HandyApiException`; no parser/transport inner exception is retained.
- All Handy HTTP sends and body reads replace transport/I/O/cancellation exception text with operation-specific diagnostics. HTTP exceptions retain numeric status when available; cancellation remains `TaskCanceledException` with the caller token and no unsafe inner exception.
- Shared device error handling retains numeric codes but deliberately omits server-supplied error names as well as descriptions. `HandyApiException.Name` remains for API compatibility but parsed device errors no longer populate it. Arbitrary names/reason phrases cannot be safely treated as diagnostics: they may contain a URL, credential, or body fragment. No speculative name allowlist or regex-based secret guessing is used.
- Malformed nonempty PUT JSON or an invalid root shape now reports an explicit error instead of silently treating malformed setup/stop/sync responses as success. Existing empty successful responses remain supported.
- Clock sampling/connectivity JSON errors also use safe parse diagnostics; missing server-time responses no longer echo the body. The request payloads, endpoints, upload URL passed into setup, and success flow are unchanged.
- Treat the URL as a bearer capability: possession may allow downloading the uploaded script until the service expires it.
- Remove the full-URL log entry. Replace it with a non-sensitive status message such as “Script upload complete; configuring HSSP.”
- Do not display the URL in the visible log, persist it in settings, include it in exception messages, or include it in diagnostic/support instructions.
- Do not log raw response bodies from upload failures or malformed upload responses. Preserve useful diagnostics such as operation name and HTTP status code without secret-bearing body text.
- Audit HandySync log calls and exception paths around upload/HSSP setup so the URL cannot leak through wrapped exception details. Keep ordinary API error reporting useful and credential-free.

### Settings writes and failure handling

- Persist settings atomically (write a temporary file in the same directory, then replace/move) so an interrupted save cannot truncate the settings file or leave a partially updated configuration.
- Keep the existing non-secret setting fields and their semantics unchanged. When saving after a user re-enters a legacy key, replace the plaintext `ConnectionKey` field with the protected collection.
- While legacy key re-entry is required, reject saves with an empty or whitespace-only replacement key and leave the original settings file unchanged.
- Reject explicit JSON `null` for `MpvIpcPipe` and `LastScriptPath` with a `SettingsException` before constructing runtime settings; omitted fields retain their defaults and empty strings remain allowed.
- Surface inability to protect/write the key explicitly in the UI and prevent a false-success save/connection state. Do not broadly catch and silently default when key protection or unprotection fails.
- Add a short user-facing note in HandySync documentation describing local key protection, that users must re-enter their key after upgrading from plaintext storage, account/machine-bound behavior, and where settings/logs live. Do not promise protection against malware or same-user access.

## Out of Scope

- Changing Handy authentication, connection-key generation, or remote Handy retention behavior.
- Encrypting every settings value or all application logs.
- Adding telemetry, a remote credential vault, or a cloud service.
- Automatically deleting/rotating all historical logs as part of this change; document current log handling and avoid adding new secrets. Log retention policy remains a separate decision if needed.
- Hiding/masking the connection-key input control; the immediate scope is persistence and diagnostics.
- A dropdown or other UI for choosing, labeling, adding, or deleting saved keys; the data model and operations must support that later.

## Acceptance Criteria

- Newly saved settings never contain any connection key in plaintext and can hold multiple labeled, independently protected keys.
- Without a selection UI, the current connection flow uses the first saved key; deleting it makes the next saved key first.
- Legacy plaintext-key settings load without using the plaintext key; the user can re-enter it, and subsequent saves store only its DPAPI-protected representation.
- Storage/API tests cover adding, ordered retrieval, and deletion of labeled keys, including first-key and last-key deletion behavior.
- Decryption/protection/write failures are explicit and never cause silent key loss or overwriting the original settings.
- The full temporary upload URL is absent from UI logs, persistent logs, and error messages for success and failure paths.
- Existing non-secret settings round-trip unchanged.
- Tests cover ignoring legacy plaintext keys, protected round-trip after re-entry, decryption failure, write failure/atomicity where testable, and URL/error redaction.
- No physical Handy device is required for automated verification.
