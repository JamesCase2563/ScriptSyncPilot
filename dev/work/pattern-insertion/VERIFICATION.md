# Pattern Insertion Verification

**Status:** Acceptance checks drafted; implementation and behavior tests not run.

See [plan](PLAN.md) and [design](DESIGN.md). Proposed behaviors must be confirmed before their tests become release requirements.

## Acceptance Criteria

- [ ] Enter observation count/time and see correct cycles per minute, explicitly distinguished from directional strokes.
- [ ] Select all three profiles and fit them to the same measured cadence and movement range.
- [ ] Choose duration or cycle count independently of observation duration.
- [ ] Preview without changing actions, selection, metadata, or dirty state.
- [ ] Apply at the captured timestamp; require no mpv/Handy connection.
- [ ] Ask Stop/Replace/Merge only on conflicts; otherwise apply without asking.
- [ ] Preserve outside-section actions and root JSON properties after edit/save.
- [ ] Refresh timeline extent/zoom/scroll limits and avoid stale selection/drag indices.
- [ ] Generate the complete requested section beyond the current timeline/script endpoint, including a new or partly authored script.
- [ ] Load an unrelated video shorter than the requested section and verify insertion is not rejected or truncated by its duration.

## Generation Tests

- [ ] 12 observed cycles in 30 seconds produces 24 cycles/minute and 2500 ms periods.
- [ ] Measurement intervals of a few seconds and several minutes use the same units/formula.
- [ ] Full-cycle mode ends at the calculated boundary; extrema hit the requested position range.
- [ ] Triangle timing is symmetric; fast-up has a shorter rising phase; eased samples slow near reversals.
- [ ] Fractional periods over many cycles do not accumulate rounding drift; timestamps remain ordered and unique.
- [ ] Shared cycle boundaries are emitted once, including nonzero insertion timestamps.
- [ ] Duration-mode partial-cycle behavior matches the confirmed design.
- [ ] Reject zero/negative/nonfinite inputs, invalid position ranges, timestamp overflow, excessive output size, and sub-millisecond point collisions without mutation.

## Conflict and Persistence Tests

- [ ] Empty script and conflict-free target apply with no prompt.
- [ ] Conflicting interior/boundary values trigger the prompt according to the agreed detection rules.
- [ ] Existing segments spanning the target with no interior action follow the agreed interpolation policy.
- [ ] Stop/dialog cancellation leave the script and dirty state unchanged.
- [ ] Replace changes only the agreed target interval, retaining exact outside timestamps/positions.
- [ ] Merge combines unique timestamps; equal/different positions at identical timestamps follow the confirmed policy without duplicate timestamps.
- [ ] Any changed document invalidates or refreshes preview/conflict results before Apply.
- [ ] Save/reload retains lowercase actions, edited values, and metadata/version/range/inverted/custom root properties.
- [ ] If single-operation undo is included, it restores the complete prior action state without discarding metadata; intervening edits follow the agreed undo contract.

## Manual Editor Checks

- [ ] Generate into a new document and a loaded script, with and without mpv attached.
- [ ] Move the video playhead while previewing; the captured insertion time stays explicit and stable.
- [ ] Try each conflict choice and verify visible results match preview.
- [ ] Insert beyond both timeline and media endpoints; verify Fit All and high-zoom scrolling reach the complete generated section while the original media duration remains unchanged.
- [ ] Repeated Apply/Cancel operations do not leave stale selection, drag state, or duplicate actions.
- [ ] Controls fit at ordinary/resized Windows window sizes and supported DPI scales; invalid input gives actionable feedback.

## Required Gates

Run focused generator/application tests and build the affected projects before declaring completion. If Common changes, include shared persistence regression checks and build both consumers. Record commands, results, and any manual checks actually performed; a successful build alone does not verify profile timing or overwrite behavior.

## Results and Limitations

No feature verification results yet. Test framework/location, confirmed design choices, test commands/results, manual environment, unresolved limitations, and PR reference will be recorded during implementation. Do not mark unchecked criteria as passed.
