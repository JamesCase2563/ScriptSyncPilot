# Shared Backlog

Project-specific features belong in the [HandySync backlog](HandySync/BACKLOG.md) or [editor backlog](FunscriptEditor/BACKLOG.md).

## Automated Tests and CI

**Priority:** High. **Status:** Proposed.

Establish focused tests and CI build/test gates for the existing projects, preserving the useful testing goals of the former controller plan without creating its nonexistent projects.

- Funscript round-trips, lowercase JSON fields, metadata preservation, sorted actions, and empty documents.
- mpv attachment failures, cancellation, observations, and disposal using a controlled local pipe server.
- Session replacement and stale queued notifications in both applications.
- Handy ordering, script selection during setup, draining requests, and stop-before-dispose with fake HTTP transport rather than hardware.
- Timeline zoom/scroll limits, deletion during drag, and keyboard routing.

## UI and Session Logic Boundaries

**Priority:** Medium. **Status:** Proposed.

Extract session/device orchestration from forms where this reduces lifecycle complexity or improves testing. Keep UI updates on the UI thread and services' ownership/cancellation explicit. Common should contain genuinely shared facilities, not become a catch-all controller.

## Privacy and Operational Guidance

**Priority:** Medium. **Status:** Proposed.

Add quickstart/troubleshooting guidance for launchers, mpv pipes, device firmware, connection failures, and log locations. Review credential persistence and logging; diagnostics must not expose connection or application keys. Do not introduce remote APIs merely to satisfy the superseded controller plan.