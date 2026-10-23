# Protect Credentials and Script URLs — Plan

**Status:** In progress; design approved (2026-10-22), M1-M3 implemented and automatically verified; M4.1/M4.2 complete. M2.7 atomic writes were implemented with M2.6. Final focused tests pass (112/112), non-incremental solution build has zero warnings/errors, and full solution tests pass (238/238). Manual Windows/device checks are recorded in [VERIFICATION.md](VERIFICATION.md) as completed except the upload/setup failure case, which is automatically verified but was not exercised manually. M4.3 final review remains pending.
**Affected projects:** HandySync, Common logging if needed, HandySync tests, documentation.

## Goal

Protect the Handy connection key at rest and prevent temporary hosted-script URLs from appearing in UI/persistent logs or error messages, preserving existing settings and making failures visible.

## Scope

- Apply Windows CurrentUser-scoped DPAPI protection to a collection of Handy connection keys, each with a stable identifier and user-facing label.
- Do not load or migrate legacy plaintext keys; tell users to re-enter their short key after upgrading.
- Support adding, retrieving, and deleting saved keys in the settings/storage layer. Until a future selection UI exists, the connection flow uses the first key in saved order.
- Preserve settings and report errors explicitly when decryption, encryption, or persistence fails.
- Remove temporary hosted-script URLs and unsafe response bodies from logs and error paths.
- Audit related exception handling and document the protection limits and local file locations.

Out of scope: Handy API behavior, remote data retention, wholesale log-retention changes, encrypting all settings, telemetry, and input-control masking.

## Decisions

- Protect every saved connection key independently using Windows DPAPI `CurrentUser`; validate API availability and avoid unnecessary dependencies.
- Ignore legacy plaintext `ConnectionKey` values; the user can re-paste the key, which is then stored only in protected form.
- Preserve saved-key order and labels; expose operations for future key selection and deletion.
- Fail closed on protected-key decryption errors: do not connect with an empty key and do not overwrite the source settings.
- Treat temporary upload URLs as bearer secrets and omit them from all user-visible/persistent diagnostics.
- Preserve other setting values and keep unrelated log messages useful.
- Dropdown, key-management dialogs, and other GUI changes are deferred; current runtime behavior selects the first saved key.
- Keep Handy-specific protection, key representation, and persistence in HandySync. Common stays device- and format-independent; no shared abstraction is needed for the local editor.

## Milestones

### 1. Characterize existing behavior

- [x] Add focused tests around current settings serialization/loading and Handy upload/setup error paths as needed. Twelve characterization cases plus two existing API regression tests pass; see [verification](VERIFICATION.md). Unsafe baseline assertions will be replaced in M2/M3.
- [x] Confirm DPAPI API availability for the target framework and define a testable protection boundary without exposing secret values. Existing Windows desktop framework supplies `ProtectedData`; added internal per-key protector interface/DPAPI implementation with nine passing cases. No dependency changes; storage integration remains M2. See [design](DESIGN.md#verified-protection-boundary-m12) and [verification](VERIFICATION.md).

### 2. Protect persisted connection key

- [x] Introduce a versioned protected-key setting representation. Immutable HandySync entry with stable GUID, label, version 1, and Base64 protected value; explicit malformed/unsupported-entry rejection. Fourteen representation cases pass; see [design](DESIGN.md#versioned-entry-representation-m21) and [verification](VERIFICATION.md). Runtime settings integration remains M2.2.
- [x] Implement protected save and protected load using CurrentUser DPAPI. Settings now write `ConnectionKeys`, never the runtime plaintext field; validate/decrypt all entries on load and use the first. Injected protection failures and real DPAPI file round-trips are verified; combined focused suite 68/68. See [verification](VERIFICATION.md).
- [x] Store multiple labeled entries in a stable order and expose add, first-key retrieval, and delete-by-ID operations. Internal HandySync settings API adds independently protected entries with stable IDs, retrieves the first or returns null when empty, and deletes by ID with first-key promotion/last-key clearing. Thirteen new cases; combined focused suite 81/81. No key-management UI added. See [verification](VERIFICATION.md).
- [x] Confirm a legacy plaintext key is not used or migrated; allow the user to re-enter it and save it in protected form without changing unrelated settings. Implemented alongside M2.2 to avoid plaintext migration in the new serializer. Startup prompts for re-entry; automated persistence tests pass, manual UI check remains pending.
- [x] Keep the current connection behavior on the first stored key; leave selection and deletion controls for a future GUI item. Form initialization explicitly retrieves the first protected entry; existing editable input remains the connection source. Five STA form tests verify first/promoted-key request headers, manual edits/entry, and an empty input after last-key removal; combined focused suite 86/86. No key-management controls added.
- [x] Make corrupt/unavailable protected-key, encryption, and file-write failures explicit; preserve the original settings file. Payload-free errors, no default-settings fallback on load failure, no committed entry updates on failed save, partial-write/locked-file preservation, and close-cancellation/retry verified. Eight new cases; combined focused suite 94/94. See [verification](VERIFICATION.md).
- [x] Use atomic settings-file replacement so an interrupted save cannot truncate the only copy. Implemented as the prerequisite for M2.6: same-directory create-new staging file, flush to disk, replace existing file or move for first save, cleanup on handled failure. No direct-write fallback. Abrupt power-loss/process-kill recovery is not manually tested.

M2.2 surfaces secret-free settings errors, aborts startup on load failure, and keeps the form open on save failure. M2.6/M2.7 now stage writes without opening/truncating the destination and commit protected-entry state only after successful replacement. Manual cross-account, crash/power-loss, and full application checks remain release-verification work.

### 3. Remove temporary URL disclosure

- [x] Replace the URL log line with a non-sensitive upload/setup status. Both logging surfaces now receive "Script upload complete; configuring HSSP."
- [x] Remove raw response bodies and secret-bearing URLs from upload/setup exception messages and logs while retaining status/code diagnostics. Upload JSON/errors, transport/read failures, HTTP reason phrases, and device error text are handled without echoing untrusted values or retaining unsafe inner exceptions.
- [x] Audit HandySync exception wrapping and visible/persistent logging around script upload, setup, and resynchronization. All Handy HTTP sends use the safe transport boundary; setup/play/stop/synctime share safe status/device error handling. Actual replacement success/failure logs tested on both surfaces; 18 new diagnostic cases, full solution 238/238. See [verification](VERIFICATION.md).

### 4. Document and verify

- [x] Document local key protection, the need to re-enter the key after upgrading from plaintext storage, DPAPI limitations/account binding, and settings/log locations. [User guidance](../../../docs/online-security.md#scriptsyncpilot-local-protection-and-diagnostics) covers re-entry, saving on successful close, first-key behavior, unmasked input, error recovery, protected-file limitations, old plaintext backups/logs, and exact local paths.
- [x] Run focused HandySync tests, then the full relevant test suite/build. M4.2 rerun: 112/112 focused cases, non-incremental full solution build with zero warnings/errors, then 238/238 tests against the freshly rebuilt assemblies. Used isolated output without changing running applications; see [verification](VERIFICATION.md#m42-final-focused-tests-solution-build-and-regression-suite).
- [ ] Complete [verification](VERIFICATION.md), review the diff for accidental secret values, and update permanent architecture documentation if behavior warrants it.

## Verification Targets

- Unit tests cover protected round-trip after key re-entry, ignoring legacy plaintext key values, ordered multi-key storage and retrieval, deletion (including first/last entry), decryption failure without overwrite, write failure without data loss, unchanged non-secret fields, and URL redaction in success/failure paths.
- A clean build and relevant test projects pass.
- Manual Windows check: after upgrade from plaintext settings, the user re-enters the key, it is protected after save, and it can be read by the same Windows user; copied ciphertext does not silently become a blank key under another user. Automated storage tests cover adding multiple labeled keys and deleting selected entries; no key-management UI is required here.
- Manual log inspection confirms that no complete hosted URL, connection key, or upload response body is written to either log surface.

Record outcomes and limitations in [VERIFICATION.md](VERIFICATION.md).
