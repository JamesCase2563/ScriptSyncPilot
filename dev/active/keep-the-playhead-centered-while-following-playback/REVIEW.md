# Code Review — Adjustable Playhead Follow Anchor

Reviewed branch `issue/#52-keep-the-playhead-centered-while-following-playback` at `b4f82dc`, compared with `origin/main`.

## Findings

- **[P3] Keep the extended scrollbar range within `int` limits** — `src/ScriptSyncPilot.FunscriptEditor/TimelineControl.cs:501`. The new range converts `(DurationSeconds + 2 * viewport) * 1000` to `int`; `LargeChange` and `SmallChange` are converted similarly. For sufficiently long media or scripts, these values exceed the `HScrollBar` integer range. The conversion can produce an invalid range, causing the scrollbar update (and timeline load/duration update) to fail. Scale the scrollbar units to the duration or otherwise bound all three values before assigning them.
- **[P3] Reconcile the verification checklist with its complete status** — `dev/active/keep-the-playhead-centered-while-following-playback/VERIFICATION.md`. The document says **Complete** and records successful automated/manual verification, but every acceptance criterion remains unchecked. Mark verified criteria or clarify which were not evaluated.

## Verification

- `dotnet test src\tests\ScriptSyncPilot.FunscriptEditor.Tests\ScriptSyncPilot.FunscriptEditor.Tests.csproj --no-restore --verbosity quiet` — **288 passed, 0 failed, 0 skipped**.
- `git diff --check origin/main...HEAD` — clean.
