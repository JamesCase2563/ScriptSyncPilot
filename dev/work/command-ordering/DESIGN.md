# Command Ordering Design

**Status:** Proposed; investigation and verification not started.
**Affected projects:** HandySync, with Common/mpv session integration only where needed to characterize target replacement.
**Production-readiness priority:** 3, command ordering.

## Goal

Verify that queued or in-flight work cannot issue a stale play/seek/synchronization command after Handy disconnect begins or after the mpv session/Handy client target changes. Preserve serialized device commands and existing lifecycle behavior.

This work verifies the supported lifecycle baseline. Automatic recovery, session restoration, and command coalescing remain deferred.

## Existing baseline and inputs

- HandySync documents a single serialized device-command gate, captured-client identity checks, session transitions, and awaited teardown in [architecture](../../HandySync/ARCHITECTURE.md).
- The archived [timeout-shutdown verification](../../archive/2026-11-timeout-shutdown/VERIFICATION.md) covers disconnect admission, pending writes, in-flight play cancellation, queued seek suppression, late transport completion, gate drain, and deferred teardown.
- The [HandySync backlog](../../HandySync/BACKLOG.md) defers desired-state/coalescing and automatic takeover/recovery. This work must not silently expand either scope.
- Inspect actual current `MainForm` transitions and tests; prior evidence may not cover mpv session replacement while commands are queued/in flight.

## Proposed approach

1. Map command producers and the command gate: setup, play/stop, seek, drift correction, offset/stroke writes, polling, disconnect, close, and mpv attach/detach/replacement.
2. Define stale-target cases separately: Handy client disconnect/replacement and mpv session replacement. Identify which commands belong to which captured identities.
3. Use deterministic fake transports and controlled tasks to hold a request while a transition occurs; inspect request order and ensure late completions cannot update the new target's UI/readiness.
4. Reuse timeout-shutdown tests as baseline evidence, then add only missing tests or fixes.
5. Keep the single gate and manual reconnect semantics; do not introduce a new queue policy or recovery controller.

## Invariants to verify

- Commands for one Handy client remain serialized.
- Once disconnect starts, queued writes/play/seek work for that client sends no new request; in-flight work is canceled or safely observed before teardown as defined by the existing lifecycle contract.
- A late completion for a replaced client or mpv session cannot mutate or report success for the current target.
- Transitions do not allow an old mpv session's position/play state to drive a new session's device commands.
- Cleanup retains the client until in-flight work has completed or is safely deferred; no disposal race is introduced.

## Constraints

- Do not add command coalescing, automatic reconnect, takeover detection/restoration, or expanded retry behavior.
- Do not promise physical stop during communication loss.
- Treat session replacement coverage as a verification question; do not assume existing checks cover every transition.

## Completion criteria

- A command/transition matrix documents verified and unverified cases.
- Disconnect and target-replacement races meet the invariants above, with focused automated tests for newly covered behavior.
- Exact evidence and limitations are recorded in [VERIFICATION.md](VERIFICATION.md) and linked to the top-level release gate.
