# Protect Credentials and Script URLs — Plan

**Status:** Proposed; implementation not started.
**Affected projects:** HandySync, Common logging if needed, HandySync tests, documentation.

## Goal

Protect the Handy connection key at rest and prevent temporary hosted-script URLs from appearing in UI/persistent logs or error messages, preserving existing settings and making failures visible.

## Scope

- Apply Windows CurrentUser-scoped DPAPI protection to the persisted Handy connection key.
- Do not load or migrate legacy plaintext keys; tell users to re-enter their short key after upgrading.
- Preserve settings and report errors explicitly when decryption, encryption, or persistence fails.
- Remove temporary hosted-script URLs and unsafe response bodies from logs and error paths.
- Audit related exception handling and document the protection limits and local file locations.

Out of scope: Handy API behavior, remote data retention, wholesale log-retention changes, encrypting all settings, telemetry, and input-control masking.

## Decisions

- Protect the connection key using Windows DPAPI `CurrentUser`; validate API availability and avoid unnecessary dependencies.
- Ignore legacy plaintext `ConnectionKey` values; the user can re-paste the key, which is then stored only in protected form.
- Fail closed on protected-key decryption errors: do not connect with an empty key and do not overwrite the source settings.
- Treat temporary upload URLs as bearer secrets and omit them from all user-visible/persistent diagnostics.
- Preserve other setting values and keep unrelated log messages useful.

## Milestones

### 1. Characterize existing behavior

- [ ] Add focused tests around current settings serialization/loading and Handy upload/setup error paths as needed.
- [ ] Confirm DPAPI API availability for the target framework and define a testable protection boundary without exposing secret values.

### 2. Protect persisted connection key

- [ ] Introduce a versioned protected-key setting representation.
- [ ] Implement protected save and protected load using CurrentUser DPAPI.
- [ ] Confirm a legacy plaintext key is not used or migrated; allow the user to re-enter it and save it in protected form without changing unrelated settings.
- [ ] Make corrupt/unavailable protected-key, encryption, and file-write failures explicit; preserve the original settings file.
- [ ] Use atomic settings-file replacement so an interrupted save cannot truncate the only copy.

### 3. Remove temporary URL disclosure

- [ ] Replace the URL log line with a non-sensitive upload/setup status.
- [ ] Remove raw response bodies and secret-bearing URLs from upload/setup exception messages and logs while retaining status/code diagnostics.
- [ ] Audit HandySync exception wrapping and visible/persistent logging around script upload, setup, and resynchronization.

### 4. Document and verify

- [ ] Document local key protection, the need to re-enter the key after upgrading from plaintext storage, DPAPI limitations/account binding, and settings/log locations.
- [ ] Run focused HandySync tests, then the full relevant test suite/build.
- [ ] Complete [verification](VERIFICATION.md), review the diff for accidental secret values, and update permanent architecture documentation if behavior warrants it.

## Verification Targets

- Unit tests cover protected round-trip after key re-entry, ignoring legacy plaintext key values, decryption failure without overwrite, write failure without data loss, unchanged non-secret fields, and URL redaction in success/failure paths.
- A clean build and relevant test projects pass.
- Manual Windows check: after upgrade from plaintext settings, the user re-enters the key, it is protected after save, and it can be read by the same Windows user; copied ciphertext does not silently become a blank key under another user.
- Manual log inspection confirms that no complete hosted URL, connection key, or upload response body is written to either log surface.

Record outcomes and limitations in [VERIFICATION.md](VERIFICATION.md).
