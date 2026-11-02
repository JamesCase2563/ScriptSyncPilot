# Error Message Inventory

**Status:** Not characterized. Rows identify failure paths to inspect; they do not assert current messages or behavior.

Use one row per distinct observable failure message/path. Record exact wording only after checking the current code or test. If no UI message or reliable next step exists, write “none” or “not applicable” rather than inferring one. Never copy credentials, hosted-script URLs, response bodies, private media paths, or unsafe exception text into this file.

## Connect and setup

| Failure path | User-visible signal / exact text | Resulting state and follow-on work | Reliable next step | Diagnostic safety / evidence |
| --- | --- | --- | --- | --- |
| Connectivity or credential check fails | To inspect | To inspect | To inspect | To inspect |
| Device info, firmware, or capability read fails | To inspect | To inspect | To inspect | To inspect |
| Server-time sample or clock estimate fails | To inspect | To inspect | To inspect | To inspect |
| Script upload fails | To inspect | To inspect | To inspect | Prior safe-response/log tests: [credential/URL verification](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md); timeout handling: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md). Ordinary error presentation still to inspect. |
| HSSP setup fails | To inspect | To inspect | To inspect | Prior safe-response/log tests: [credential/URL verification](../../archive/2026-10-protect-credentials-and-script-urls/VERIFICATION.md). Ordinary error presentation still to inspect. |
| Initial play/stop command fails | To inspect | To inspect | To inspect | Timeout state behavior: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); non-timeout presentation still to inspect. |

## Playback and synchronization

| Failure path | User-visible signal / exact text | Resulting state and follow-on work | Reliable next step | Diagnostic safety / evidence |
| --- | --- | --- | --- | --- |
| Play or resume fails | To inspect | To inspect | To inspect | Timeout state behavior: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); non-timeout presentation still to inspect. |
| Pause/stop fails | To inspect | To inspect | To inspect | Disconnect/close stop behavior: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); ordinary playback-stop presentation still to inspect. |
| Seek/resynchronization fails | To inspect | To inspect | To inspect | Stale/timeout lifecycle cases: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); ordinary failure presentation still to inspect. |
| Periodic `synctime` fails or times out | To inspect | To inspect | To inspect | Timeout state behavior: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); non-timeout presentation still to inspect. |

## Device reads and controls

| Failure path | User-visible signal / exact text | Resulting state and follow-on work | Reliable next step | Diagnostic safety / evidence |
| --- | --- | --- | --- | --- |
| Offset read or polling fails | To inspect | To inspect | To inspect | To inspect |
| Stroke read or polling fails | To inspect | To inspect | To inspect | To inspect |
| Offset write fails or times out | To inspect | To inspect | To inspect | Disconnect cancellation of pending writes: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); actual failed-write presentation still to inspect. |
| Stroke write fails or times out | To inspect | To inspect | To inspect | Disconnect cancellation of pending writes: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md); actual failed-write presentation still to inspect. |
| Temperature/state read fails, if surfaced | To inspect | To inspect | To inspect | To inspect |

## Disconnect and application close

| Failure path | User-visible signal / exact text | Resulting state and follow-on work | Reliable next step | Diagnostic safety / evidence |
| --- | --- | --- | --- | --- |
| Disconnect stop fails or times out | To inspect | To inspect | To inspect | Prior safe log and cleanup coverage: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md). |
| Command drain remains pending | To inspect | To inspect | To inspect | Deferred safe teardown coverage: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md). |
| Application close encounters teardown failure | To inspect | To inspect | To inspect | Close-without-dialog coverage: [timeout/shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md). |

## Review key

- **User-visible signal:** exact status text, dialog, control state, or “none; log only.” Identify where it appears.
- **Resulting state and follow-on work:** whether readiness/connected state is cleared or marked unknown, and whether dependent commands are suppressed.
- **Reliable next step:** include only an action supported by evidence; otherwise record “none known.”
- **Diagnostic safety / evidence:** record whether text contains secrets, URLs, response content, arbitrary exception text, or unnecessary paths, and cite a source location or test name without copying sensitive values.

This inventory supports the [design](DESIGN.md) and [verification record](VERIFICATION.md). Keep detailed test commands and outcomes in `VERIFICATION.md` rather than duplicating them here.
