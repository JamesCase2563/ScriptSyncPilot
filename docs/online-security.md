# Privacy, data flows, and security

This notice describes ScriptSyncPilot's application behavior. It does not make promises on behalf of Handy or mpv.

## What the applications do with your data

### FunscriptEditor

FunscriptEditor edits script files on your computer. Video playback uses your separately installed mpv player. The editor does not send video or script files to ScriptSyncPilot services; this project does not operate a cloud service for them.

### HandySync and Handy services

HandySync reads the selected funscript locally and sends its action timestamps and positions as a CSV file to Handy's Hosting API. It does not upload the associated video. HandySync receives a hosted script URL and sends that URL to the device through Handy's REST API for HSSP playback. Treat that URL as a secret that grants access to the hosted script.

HandySync also sends the connection key to Handy's REST API to authenticate device-control requests. Requests include the selected control operation and values such as playback time, offset, or stroke range. The app does not send the connection key to ScriptSyncPilot: the project has no ScriptSyncPilot server. The third-party service's handling of these requests and associated network metadata is governed by that provider, not this project.

The CSV retention period and generated URL lifetime have not been confirmed by Handy. The URL is described by the Hosting API as temporary, but no exact expiration or deletion time is known. Do not rely on an assumed expiry period; no retention duration is promised here.

## Data stored on your computer

HandySync stores settings in `%APPDATA%\ScriptSyncPilot.HandySync\settings.json`. Saved connection keys are protected with Windows DPAPI for the current Windows user/profile. The settings also include the mpv IPC pipe, device offset, stroke range, and last script path. The connection key is present in application memory while in use, and the entry field is not masked. FunscriptEditor stores its settings, including the last script path, in `%APPDATA%\ScriptSyncPilot.FunscriptEditor\settings.json`. Old plaintext HandySync settings backups are not migrated or erased by the application.

Both applications write append-only logs under `%LOCALAPPDATA%`:

- FunscriptEditor: `%LOCALAPPDATA%\ScriptSyncPilot.FunscriptEditor\scriptsyncpilot.funscripteditor.log`
- HandySync: `%LOCALAPPDATA%\ScriptSyncPilot.HandySync\scriptsyncpilot.handysync.log`

Logs can contain local paths, operation status, and diagnostic details. Current upload/setup logging avoids writing the connection key and hosted URL. Older log files may contain generated hosted URLs; logs are not automatically sanitized, rotated, or deleted. Review and redact files before sharing them. We found no analytics, telemetry, or crash-reporting integration in the application source or project dependencies reviewed for this notice.

## Reporting a problem safely

Ordinary support and bug reports belong in [GitHub Issues](https://github.com/JamesCase2563/ScriptSyncPilot/issues), which are publicly accessible. Include the app version, Windows version, relevant mpv/device/firmware versions, steps to reproduce, and a sanitized error message.

**Do not include personal data, Handy connection keys, generated/private script URLs, private media, or unreviewed settings and log files.** These may expose account/device access or private content.

### Security reports

GitHub private vulnerability reporting is enabled for this public repository. Use **Security and quality → Advisories → Report a vulnerability** on the repository to submit a report privately. Do not post vulnerability details or secrets in a public issue.

## Protection limits

DPAPI protects saved connection keys for the current Windows profile. It does not protect against malware or processes running as the same user, administrators, or access to an unlocked session. Other settings and identifiers are not encrypted. Copying protected settings to another Windows account or machine is not a supported migration method.

If settings cannot be decrypted or saved, preserve the original file and use the application's error message to recover; do not replace it with an empty file. See the [source-release guide](source-release.md) for update and rollback guidance.
