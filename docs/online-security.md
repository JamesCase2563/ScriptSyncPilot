# Online Security and the Handy

## Application network requirements

FunscriptEditor works with local script files. Video playback in FunscriptEditor uses an external mpv installation; editing scripts does not require Handy services.

HandySync requires a persistent internet connection to reach Handy's REST API v3 and Hosting API v2 services. It uploads funscript action data as CSV for device playback; the associated video stays local. The retention period for hosted files and the lifetime of generated URLs are unknown, so no expiry period is promised. See the [approved support scope and known limitations](../dev/work/production-readiness/SCOPE-AND-LIMITATIONS.md) and [tested environment record](../dev/work/production-readiness/TESTED-ENVIRONMENTS.md) for the actual compatibility boundaries.

## Security Measures
- **End-to-end encryption**: All communication between the Handy and servers is encrypted via HTTPS.
- **Connection key system**: Each device uses a unique connection key, which can be changed at any time. Changing the key at the hardware level invalidates the old key.
- **No historic key records**: There are no records of previous keys, making tracking difficult for third parties.
- **Third-party access**: Third-party services can only identify your device by the connection key you provide. They do not receive your device's IP address.
- **Local configuration**: Local configuration is done via Bluetooth (approx. 10m range).

## Data Handling
- **Minimal data uploaded**: Only essential data is transmitted. The system does not upload your browser history, payment info, or other personal data.
- **No direct device access**: Third-party apps interact with the Handy via the API and do not have direct access to your device.

## Key Security Details
- **Key generation**: The connection key is generated using true random noise from the Wi-Fi antenna.
- **Key length**: Variable from 5 to 32 characters, using lower case, upper case, and numbers (62 possible characters per position).
- **Brute force resistance**: Even the shortest key (5 chars) has over 916 million combinations; the longest (32 chars) has over 2.2e57 combinations.
- **Key compromise**: If a key is brute-forced and stolen, the attacker can only control the device while it is online. Changing the key immediately revokes access.

## Device Operation
- **Offline by default**: The Handy is offline by default. Wi-Fi must be manually activated each time the device is powered on.
- **Firmware and API**: The Handy's online services may provide firmware updates and API access. ScriptSyncPilot does not download or install firmware; use the manufacturer's [official firmware guidance](https://links.handyfeeling.com/docs-api-handy-rest-v3-fw3) for updates. If you wish to revoke third-party access, change your connection key.

## Privacy Philosophy
- **Privacy by default**: The Handy is designed with privacy as a priority. There is little incentive to collect user data, reducing future data collection risks.

## CTO Statement
> “If it's online, it can be hacked or manipulated. ... Generating a new key renders the old one useless. ... Our Handy-server connection system is fire and forget. ... Privacy by default is very important for us.”

## ScriptSyncPilot Local Protection and Diagnostics

### Upgrading and Saving Your Key

HandySync protects saved connection keys using Windows DPAPI scoped to the current user. After upgrading from plaintext settings, the old key is ignored, not automatically migrated. Enter your Handy connection key again in the existing connection-key box. The key is protected when you close the application and settings are successfully saved; connecting alone does not save settings.

Other settings, including the mpv pipe, script path, device offset, and stroke range, retain their values. The application uses the first saved key; key-selection and key-management controls are not provided yet. The connection-key input is not masked, and the key exists in memory while in use.

### Protection Limits and Recovery

Protection is bound to the Windows user/profile. Copying protected settings to another account/machine is not supported migration; re-enter the key in the new environment instead. DPAPI does not protect against same-user malware/processes, administrators, or access to an unlocked session. Labels, identifiers, and non-secret settings are not encrypted.

If settings cannot be read or any saved key cannot be decrypted, HandySync reports an error and does not open a usable application or overwrite the file with defaults. Keep the original file; check that you are using the correct Windows account/profile and recover a known-good backup if necessary. Do not replace it with an empty file just to suppress the error.

If protection or saving fails while closing, an error is shown and closing is cancelled. Correct the reported access/storage problem and retry closing. Settings writes use a flushed temporary file followed by replacement rather than truncating the original. A crash can leave a protected temporary file; this is not a guarantee against hardware failure or power loss.

### Settings and Logs

| Local file | Location |
| --- | --- |
| HandySync settings | `%APPDATA%\ScriptSyncPilot.HandySync\settings.json` |
| HandySync persistent log | `%LOCALAPPDATA%\ScriptSyncPilot.HandySync\scriptsyncpilot.handysync.log` |

You can paste either directory into Windows File Explorer. Treat old plaintext settings backups as sensitive: upgrading rewrites the active settings on successful save, not copies or backups elsewhere.

Uploaded scripts receive a temporary hosted URL that must be treated as a bearer secret. HandySync passes it to the device's setup API but does not include it in upload/setup logs or settings. Errors retain operation names and numeric status/device codes, not raw response bodies or transport details.

Persistent logs are append-only. Existing logs from earlier versions may still contain hosted URLs; this update does not sanitize old files or add retention/rotation. Review old logs before sharing them, and do not include connection keys, temporary URLs, or raw API bodies in support reports. Remote hosting expiry/retention remains controlled by Handy.

## Additional Links

- [thehandy.com](https://thehandy.com/)
- [Developer Docs](https://intercom.help/ohdoki/en/collections/5757680-developer)

---
*Handy service background above was extracted and summarized from [Ohdoki Help Center](https://intercom.help/ohdoki/en/articles/9034256-online-security-and-the-handy) on May 28, 2026. The ScriptSyncPilot local-protection section documents this application's behavior, not a vendor guarantee.*
